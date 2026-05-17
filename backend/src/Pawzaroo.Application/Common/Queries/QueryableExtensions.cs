using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Shared.Paging;

namespace Pawzaroo.Application.Common.Queries;

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query,
        PageRequest page, CancellationToken ct = default)
    {
        var total = await query.LongCountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.Take).ToListAsync(ct);
        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }

    public static async Task<PagedResult<TResult>> ToPagedResultAsync<T, TResult>(this IQueryable<T> query,
        PageRequest page, Func<T, TResult> map, CancellationToken ct = default)
    {
        var total = await query.LongCountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.Take).ToListAsync(ct);
        return new PagedResult<TResult>(items.Select(map).ToList(), page.Page, page.PageSize, total);
    }

    /// <summary>Conditional Where to keep filter chains readable.</summary>
    public static IQueryable<T> WhereIf<T>(this IQueryable<T> q, bool condition, Expression<Func<T, bool>> predicate)
        => condition ? q.Where(predicate) : q;

    /// <summary>
    /// Dynamic OrderBy from a "field" or "-field" / "field:desc" string. Whitelist
    /// of allowed property names is required to prevent reflection-based abuse.
    /// </summary>
    public static IQueryable<T> OrderBy<T>(this IQueryable<T> q, string? sort, HashSet<string> allowed, string defaultField)
    {
        string field = defaultField;
        bool desc = true;

        if (!string.IsNullOrWhiteSpace(sort))
        {
            string raw = sort.Trim();
            if (raw.StartsWith('-')) { desc = true; raw = raw[1..]; }
            else if (raw.Contains(':'))
            {
                var parts = raw.Split(':', 2);
                raw = parts[0];
                desc = string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase);
            }
            else { desc = false; }
            if (allowed.Contains(raw)) field = raw;
        }

        var param = Expression.Parameter(typeof(T), "x");
        var prop = typeof(T).GetProperty(field, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)
            ?? typeof(T).GetProperty(defaultField, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)!;
        var access = Expression.MakeMemberAccess(param, prop);
        var lambda = Expression.Lambda(access, param);
        var method = (desc ? "OrderByDescending" : "OrderBy");
        var call = Expression.Call(typeof(Queryable), method, new[] { typeof(T), prop.PropertyType },
            q.Expression, Expression.Quote(lambda));
        return q.Provider.CreateQuery<T>(call);
    }
}
