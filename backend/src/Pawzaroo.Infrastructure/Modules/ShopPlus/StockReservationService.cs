using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.ShopPlus;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.ShopPlus;

public class StockReservationService : IStockReservationService
{
    /// <summary>How long stock is held once checkout starts.</summary>
    public static readonly TimeSpan HoldWindow = TimeSpan.FromMinutes(15);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public StockReservationService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public async Task<CheckoutReservationDto> StartCheckoutAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var now = DateTime.UtcNow;

        // Replace this user's previous holds so re-entering checkout doesn't double-reserve.
        var mine = await _db.Set<StockReservation>()
            .Where(r => r.UserId == uid && r.Status == ReservationStatus.Active).ToListAsync(ct);
        foreach (var r in mine) { r.Status = ReservationStatus.Released; r.UpdatedAt = now; }
        await _db.SaveChangesAsync(ct);

        var lines = await _db.CartItems.AsNoTracking().Include(c => c.Product).Where(c => c.UserId == uid).ToListAsync(ct);
        if (lines.Count == 0)
            throw new ValidationException(new Dictionary<string, string[]> { ["cart"] = new[] { "Cart is empty." } });

        var lineIds = lines.Select(l => l.Id).ToList();
        var links = await _db.Set<CartItemVariant>().AsNoTracking()
            .Where(l => lineIds.Contains(l.CartItemId)).ToDictionaryAsync(l => l.CartItemId, l => l.VariantId, ct);

        var created = new List<StockReservation>();
        foreach (var line in lines)
        {
            Guid? variantId = links.TryGetValue(line.Id, out var vid) ? vid : null;
            int stock;
            if (variantId is { } v)
            {
                stock = await _db.Set<ProductVariant>().AsNoTracking().Where(x => x.Id == v)
                    .Select(x => x.StockQuantity).FirstOrDefaultAsync(ct);
            }
            else stock = line.Product.StockQuantity;

            var held = await _db.Set<StockReservation>().AsNoTracking()
                .Where(r => r.ProductId == line.ProductId && r.VariantId == variantId
                            && r.Status == ReservationStatus.Active && r.ExpiresAt > now)
                .SumAsync(r => (int?)r.Quantity, ct) ?? 0;

            if (stock - held < line.Quantity)
                throw new ConflictException($"'{line.Product.Name}' no longer has enough available stock.");

            created.Add(new StockReservation
            {
                UserId = uid, ProductId = line.ProductId, VariantId = variantId,
                Quantity = line.Quantity, ExpiresAt = now + HoldWindow, CreatedBy = uid
            });
        }

        _db.Set<StockReservation>().AddRange(created);
        await _db.SaveChangesAsync(ct);
        return new CheckoutReservationDto(created.Select(ToDto).ToList(), created.Max(c => (DateTime?)c.ExpiresAt));
    }

    public async Task<IReadOnlyList<ReservationDto>> ListMineAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var rows = await _db.Set<StockReservation>().AsNoTracking()
            .Where(r => r.UserId == uid && r.Status == ReservationStatus.Active && r.ExpiresAt > now)
            .OrderBy(r => r.ExpiresAt).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task ReleaseMineAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        await _db.Set<StockReservation>()
            .Where(r => r.UserId == uid && r.Status == ReservationStatus.Active)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.Released)
                                      .SetProperty(r => r.UpdatedAt, (DateTime?)now), ct);
    }

    public async Task<int> ExpireDueAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.Set<StockReservation>()
            .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.Expired)
                                      .SetProperty(r => r.UpdatedAt, (DateTime?)now), ct);
    }

    private static ReservationDto ToDto(StockReservation r) =>
        new(r.Id, r.ProductId, r.VariantId, r.Quantity, r.ExpiresAt, r.Status);
}
