using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Modules.ShopPlus;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Modules.ShopPlus;

/// <summary>Worker-side processing for saved-search alerts and price-drop alerts (writes in-app notifications).</summary>
public class DiscoveryAlertProcessor : IDiscoveryAlertProcessor
{
    /// <summary>A saved search alerts at most once per this window.</summary>
    public static readonly TimeSpan SavedSearchMinInterval = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    public DiscoveryAlertProcessor(ApplicationDbContext db) => _db = db;

    public async Task<int> RunSavedSearchAlertsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var cutoff = now - SavedSearchMinInterval;
        var searches = await _db.Set<SavedSearch>()
            .Where(s => s.AlertEnabled && s.LastCheckedAt <= cutoff)
            .OrderBy(s => s.LastCheckedAt).Take(200).ToListAsync(ct);

        int notified = 0;
        foreach (var s in searches)
        {
            FacetSearchRequest req;
            try { req = JsonSerializer.Deserialize<FacetSearchRequest>(s.QueryJson, Json) ?? new FacetSearchRequest(); }
            catch (JsonException) { req = new FacetSearchRequest(); }

            var since = s.LastCheckedAt;
            var tokens = TextSimilarity.Tokenize(req.Q);
            var count = await DiscoveryFilters.Apply(DiscoveryFilters.PublicProducts(_db), req, tokens)
                .CountAsync(p => p.CreatedAt > since, ct);

            s.LastCheckedAt = now;
            if (count > 0)
            {
                _db.Set<InAppNotification>().Add(new InAppNotification
                {
                    UserId = s.UserId,
                    Title = $"New matches for \"{s.Name}\"",
                    Body = count == 1 ? "1 new item matches your saved search." : $"{count} new items match your saved search.",
                    Url = "/store"
                });
                notified++;
            }
        }
        if (searches.Count > 0) await _db.SaveChangesAsync(ct);
        return notified;
    }

    public async Task<int> RunPriceDropsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var watches = await _db.Set<PriceWatch>().Include(w => w.Product)
            .Where(w => w.IsActive && w.Product.IsActive).Take(1000).ToListAsync(ct);

        int notified = 0;
        foreach (var w in watches)
        {
            var current = w.Product.DiscountPrice ?? w.Product.Price;
            if (current > w.BaselinePrice)
            {
                w.BaselinePrice = current; // price rose: track the new high so a later drop alerts again
                continue;
            }
            if (current < w.BaselinePrice && (w.TargetPrice is null || current <= w.TargetPrice))
            {
                _db.Set<InAppNotification>().Add(new InAppNotification
                {
                    UserId = w.UserId,
                    Title = "Price drop",
                    Body = $"{w.Product.Name} dropped from {w.BaselinePrice:0.00} to {current:0.00}.",
                    Url = $"/store/products/{w.ProductId}"
                });
                w.BaselinePrice = current;
                w.LastNotifiedAt = now;
                notified++;
            }
        }
        if (watches.Count > 0) await _db.SaveChangesAsync(ct);
        return notified;
    }
}
