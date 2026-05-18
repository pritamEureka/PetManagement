using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Events;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Marketplace;

public class StoreOwnerProfileService : IStoreOwnerProfileService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;
    private readonly INotificationService _notify;

    public StoreOwnerProfileService(ApplicationDbContext db, ICurrentUserService current,
        IKafkaProducer kafka, IAuditLogger audit, INotificationService notify)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _audit = audit;
        _notify = notify;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();
    private bool CanModerate() => _current.Permissions.Contains(Permissions.Sellers.Approve);

    public async Task<StoreOwnerProfileDto?> GetMineAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        return await _db.StoreOwnerProfiles.AsNoTracking()
            .Where(p => p.UserId == uid)
            .Select(p => ToDto(p))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<StoreOwnerProfileDto> SubmitAsync(SubmitStoreOwnerProfileInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var existing = await _db.StoreOwnerProfiles.FirstOrDefaultAsync(p => p.UserId == uid, ct);

        if (existing is null)
        {
            existing = new StoreOwnerProfile { UserId = uid };
            _db.StoreOwnerProfiles.Add(existing);
        }
        else if (existing.KycStatus == ApprovalStatus.Approved)
        {
            throw new ConflictException("KYC already approved.");
        }

        existing.LegalName = input.LegalName;
        existing.BusinessName = input.BusinessName;
        existing.TradeLicenseNumber = input.TradeLicenseNumber;
        existing.NationalIdNumber = input.NationalIdNumber;
        existing.TaxId = input.TaxId;
        existing.TradeLicenseDocUrl = input.TradeLicenseDocUrl;
        existing.NationalIdDocUrl = input.NationalIdDocUrl;
        existing.AddressProofDocUrl = input.AddressProofDocUrl;
        existing.KycStatus = ApprovalStatus.Pending;
        existing.SubmittedAt = DateTime.UtcNow;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = uid;

        await _db.SaveChangesAsync(ct);

        if (input.AdditionalDocuments is { Count: > 0 })
        {
            foreach (var d in input.AdditionalDocuments)
            {
                _db.StoreDocuments.Add(new StoreDocument
                {
                    StoreOwnerProfileId = existing.Id,
                    Type = d.Type,
                    FileName = d.FileName,
                    Url = d.Url,
                    Notes = d.Notes
                });
            }
            await _db.SaveChangesAsync(ct);
        }

        await _kafka.PublishAsync(MarketplaceTopics.StoreEvents,
            new StoreOwnerKycSubmitted(existing.Id, uid, DateTime.UtcNow), uid.ToString(), ct);
        await _audit.LogAsync("kyc.submit", "StoreOwnerProfile", existing.Id.ToString(), ct: ct);

        return ToDto(existing);
    }

    public async Task<PageResult<StoreOwnerProfileDto>> ListForAdminAsync(ApprovalStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        if (!CanModerate()) throw new ForbiddenException();
        var q = _db.StoreOwnerProfiles.AsNoTracking();
        if (status.HasValue) q = q.Where(p => p.KycStatus == status);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(p => p.SubmittedAt ?? p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => ToDto(p))
            .ToListAsync(ct);
        return new PageResult<StoreOwnerProfileDto>(items, total, page, pageSize);
    }

    public Task ApproveAsync(Guid profileId, string? notes, CancellationToken ct = default) =>
        DecideAsync(profileId, ApprovalStatus.Approved, notes, ct);
    public Task RejectAsync(Guid profileId, string? notes, CancellationToken ct = default) =>
        DecideAsync(profileId, ApprovalStatus.Rejected, notes, ct);

    private async Task DecideAsync(Guid profileId, ApprovalStatus status, string? notes, CancellationToken ct)
    {
        if (!CanModerate()) throw new ForbiddenException();
        var p = await _db.StoreOwnerProfiles.FirstOrDefaultAsync(x => x.Id == profileId, ct)
                ?? throw new NotFoundException("StoreOwnerProfile", profileId);
        p.KycStatus = status;
        p.AdminNotes = notes;
        p.DecidedAt = DateTime.UtcNow;
        p.UpdatedAt = DateTime.UtcNow;
        p.UpdatedBy = _current.UserId;
        await _db.SaveChangesAsync(ct);

        var by = _current.UserId ?? Guid.Empty;
        object evt = status == ApprovalStatus.Approved
            ? new StoreOwnerKycApproved(p.Id, p.UserId, by, DateTime.UtcNow)
            : new StoreOwnerKycRejected(p.Id, p.UserId, by, notes, DateTime.UtcNow);
        await _kafka.PublishAsync(MarketplaceTopics.StoreEvents, evt, p.UserId.ToString(), ct);

        await _notify.NotifyUserAsync(p.UserId,
            status == ApprovalStatus.Approved ? "Store KYC approved" : "Store KYC rejected",
            notes ?? string.Empty, new { profileId = p.Id }, ct);
        await _audit.LogAsync($"kyc.{status.ToString().ToLowerInvariant()}", "StoreOwnerProfile", p.Id.ToString(), notes, ct: ct);
    }

    private static StoreOwnerProfileDto ToDto(StoreOwnerProfile p) => new(
        p.Id, p.UserId, p.LegalName, p.BusinessName,
        p.TradeLicenseNumber, p.NationalIdNumber, p.TaxId,
        p.TradeLicenseDocUrl, p.NationalIdDocUrl, p.AddressProofDocUrl,
        p.KycStatus, p.AdminNotes, p.SubmittedAt, p.DecidedAt);
}
