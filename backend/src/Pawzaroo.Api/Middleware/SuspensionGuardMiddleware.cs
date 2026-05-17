using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Api.Middleware;

/// <summary>
/// Final fence against actions by suspended / banned users — runs *after*
/// authentication so we have the user id. A stale JWT cannot bypass this
/// because suspension state lives in the DB / Redis cache, not the token.
///
/// Whitelist paths the suspended user *must* still hit:
///   - /api/v1/auth/me, /auth/logout — so they can see what happened and sign out
///   - /api/v1/auth/refresh        — would otherwise loop the SPA into a logout
///   - /api/v1/security/warnings/.../ack — let them clear strikes
/// </summary>
public class SuspensionGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRedisCacheService _cache;
    private static readonly string[] AllowedPaths =
    {
        "/api/v1/auth/me",
        "/api/v1/auth/logout",
        "/api/v1/auth/refresh",
        "/api/v1/security/me",
        "/api/v1/security/warnings/ack",
    };

    public SuspensionGuardMiddleware(RequestDelegate next, IRedisCacheService cache)
    {
        _next = next;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext ctx, ApplicationDbContext db)
    {
        // Skip if unauthenticated, on an allowlisted path, or non-mutating GET to public assets.
        if (ctx.User?.Identity?.IsAuthenticated != true)
        {
            await _next(ctx); return;
        }
        var path = ctx.Request.Path.Value ?? "";
        if (AllowedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(ctx); return;
        }

        var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var uid))
        {
            await _next(ctx); return;
        }

        // Redis cache key: nullable bool. Avoids a DB hit on every request.
        var cacheKey = $"auth:suspended:{uid}";
        var cached = await _cache.GetAsync<bool?>(cacheKey, ctx.RequestAborted);
        bool suspended;
        if (cached is null)
        {
            suspended = await db.UserSuspensions.AsNoTracking()
                .AnyAsync(s => s.UserId == uid && s.Status == SuspensionStatus.Active
                                && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow), ctx.RequestAborted);
            await _cache.SetAsync(cacheKey, (bool?)suspended, TimeSpan.FromMinutes(1), ctx.RequestAborted);
        }
        else suspended = cached.Value;

        if (suspended)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = new
                {
                    code = "account_suspended",
                    message = "Your account is suspended. Visit /account/suspended for details."
                }
            }));
            return;
        }

        await _next(ctx);
    }
}
