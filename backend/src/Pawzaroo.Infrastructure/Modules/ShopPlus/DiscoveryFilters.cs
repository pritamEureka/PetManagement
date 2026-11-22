using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.ShopPlus;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Modules.ShopPlus;

/// <summary>Shared product filtering used by search, facets and the saved-search alert job.</summary>
public static class DiscoveryFilters
{
    public static IQueryable<Product> PublicProducts(ApplicationDbContext db) =>
        db.Products.AsNoTracking().Where(p => p.IsActive && p.Store.ApprovalStatus == ApprovalStatus.Approved);

    /// <param name="skip">facet name whose own filter is ignored: category|brand|store|price|rating|stock</param>
    public static IQueryable<Product> Apply(IQueryable<Product> q, FacetSearchRequest r, IReadOnlyList<string> tokens, string? skip = null)
    {
        foreach (var t in tokens)
        {
            var pat = "%" + t.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            q = q.Where(p => EF.Functions.ILike(p.Name, pat)
                             || EF.Functions.ILike(p.Sku, pat)
                             || (p.Description != null && EF.Functions.ILike(p.Description, pat))
                             || (p.Brand != null && EF.Functions.ILike(p.Brand.Name, pat))
                             || (p.Category != null && EF.Functions.ILike(p.Category.Name, pat)));
        }
        if (skip != "category" && r.CategoryId.HasValue) q = q.Where(p => p.CategoryId == r.CategoryId);
        if (skip != "brand" && r.BrandId.HasValue) q = q.Where(p => p.BrandId == r.BrandId);
        if (skip != "store" && r.StoreId.HasValue) q = q.Where(p => p.StoreId == r.StoreId);
        if (skip != "price")
        {
            if (r.MinPrice.HasValue) q = q.Where(p => (p.DiscountPrice ?? p.Price) >= r.MinPrice);
            if (r.MaxPrice.HasValue) q = q.Where(p => (p.DiscountPrice ?? p.Price) <= r.MaxPrice);
        }
        if (skip != "rating" && r.MinRating.HasValue) q = q.Where(p => p.RatingAverage >= r.MinRating);
        if (skip != "stock" && r.InStockOnly == true) q = q.Where(p => p.StockQuantity > 0);
        return q;
    }

    public static readonly Expression<Func<Product, ProductSummaryDto>> Summary = p => new ProductSummaryDto(
        p.Id, p.Name, p.Sku, p.Price, p.DiscountPrice,
        p.StockQuantity, p.IsActive, p.IsFeatured,
        p.RatingAverage, p.RatingCount,
        p.StoreId, p.Store.Name,
        p.CategoryId, p.Category != null ? p.Category.Name : null,
        p.BrandId, p.Brand != null ? p.Brand.Name : null,
        p.Images.OrderBy(i => i.OrderIndex).Select(i => i.Url).ToList(),
        p.CreatedAt);

    public static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371.0;
        double ToRad(double d) => d * Math.PI / 180.0;
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}
