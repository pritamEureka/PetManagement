using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Modules.Marketplace;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>
/// Logistics contribution to the delivery promise: matched zone transit hours plus the store's SLA
/// processing time (worst case only, so the lower bound stays optimistic). Returns null when no zone matches.
/// </summary>
public class LogisticsEtaContributor : IDeliveryEtaContributor
{
    private readonly ApplicationDbContext _db;
    public LogisticsEtaContributor(ApplicationDbContext db) { _db = db; }

    public async Task<DeliveryEtaContribution?> ContributeAsync(Guid storeId, string? destinationCity, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(destinationCity)) return null;
        var zones = await _db.Set<DeliveryZone>().AsNoTracking()
            .Where(z => z.IsActive && (z.StoreId == null || z.StoreId == storeId)).ToListAsync(ct);
        var zone = zones.Where(z => ShippingQuoteMath.CityMatches(z.Cities, destinationCity))
            .OrderByDescending(z => z.StoreId != null).ThenBy(z => z.BaseFee).FirstOrDefault();
        if (zone is null) return null;

        var slas = await SlaEvaluator.LoadSlasAsync(_db, ct);
        var (ship, _) = SlaEvaluator.Targets(slas, storeId);
        var min = zone.EstimatedMinHours / 24.0;
        var max = zone.EstimatedMaxHours / 24.0 + ship / 24.0;
        return new DeliveryEtaContribution(min, max,
            $"Includes {zone.Name} zone transit ({zone.EstimatedMinHours}-{zone.EstimatedMaxHours}h) and up to {ship}h processing.");
    }
}
