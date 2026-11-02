using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Pets;

public class PetAccessService : IPetAccessService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    // Scoped service — memoise per request so a controller action that checks
    // access and then loads data doesn't hit the DB twice for the same pet.
    private readonly Dictionary<Guid, PetAccessLevel> _cache = new();

    public PetAccessService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    private bool CanReadAny => _current.Permissions.Contains(Permissions.Pets.ReadAny);

    public async Task<PetAccessLevel> GetLevelAsync(Guid petId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(petId, out var cached)) return cached;
        var uid = _current.UserId ?? throw new ForbiddenException();

        var ownerId = await _db.Pets.AsNoTracking().Where(p => p.Id == petId)
            .Select(p => (Guid?)p.OwnerId).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Pet", petId);

        PetAccessLevel level;
        if (ownerId == uid)
        {
            level = PetAccessLevel.Owner;
        }
        else
        {
            var now = DateTime.UtcNow;
            var role = await _db.PetCaretakers.AsNoTracking()
                .Where(c => c.PetId == petId && c.UserId == uid && (c.ExpiresAt == null || c.ExpiresAt > now))
                .Select(c => (CaretakerRole?)c.Role).FirstOrDefaultAsync(ct);

            level = role switch
            {
                CaretakerRole.CoOwner => PetAccessLevel.CoOwner,
                CaretakerRole.Caretaker => PetAccessLevel.Caretaker,
                CaretakerRole.Viewer => PetAccessLevel.Viewer,
                _ => PetAccessLevel.None
            };

            if (level == PetAccessLevel.None)
            {
                // Vets see the record of any pet they have (had) a live appointment with.
                var isTreatingVet = await _db.Appointments.AsNoTracking().AnyAsync(a =>
                    a.PetId == petId && a.Doctor.UserId == uid
                    && a.Status != AppointmentStatus.CancelledByUser
                    && a.Status != AppointmentStatus.CancelledByDoctor
                    && a.Status != AppointmentStatus.Draft, ct);
                if (isTreatingVet) level = PetAccessLevel.Vet;
            }
        }

        _cache[petId] = level;
        return level;
    }

    public async Task EnsureCanReadAsync(Guid petId, CancellationToken ct = default)
    {
        var level = await GetLevelAsync(petId, ct);
        // Hide existence from strangers: 404 rather than 403.
        if (level == PetAccessLevel.None && !CanReadAny) throw new NotFoundException("Pet", petId);
    }

    public async Task EnsureCanLogCareAsync(Guid petId, CancellationToken ct = default)
    {
        await EnsureCanReadAsync(petId, ct);
        if (await GetLevelAsync(petId, ct) < PetAccessLevel.Caretaker)
            throw new ForbiddenException("You don't have caretaker access to this pet.");
    }

    public async Task EnsureCanEditClinicalAsync(Guid petId, CancellationToken ct = default)
    {
        await EnsureCanReadAsync(petId, ct);
        var level = await GetLevelAsync(petId, ct);
        if (level != PetAccessLevel.Vet && level < PetAccessLevel.CoOwner)
            throw new ForbiddenException("Only the owner, a co-owner or the treating vet can change health records.");
    }

    public async Task EnsureCanEditAsync(Guid petId, CancellationToken ct = default)
    {
        await EnsureCanReadAsync(petId, ct);
        if (await GetLevelAsync(petId, ct) < PetAccessLevel.CoOwner)
            throw new ForbiddenException("Only the owner or a co-owner can change this pet.");
    }

    public async Task EnsureOwnerAsync(Guid petId, CancellationToken ct = default)
    {
        await EnsureCanReadAsync(petId, ct);
        if (await GetLevelAsync(petId, ct) != PetAccessLevel.Owner)
            throw new ForbiddenException("Only the owner can do this.");
    }

    /// <summary>Ids of every pet the current user can at least view via ownership or caretaking.</summary>
    internal static IQueryable<Pet> AccessiblePets(ApplicationDbContext db, Guid uid, PetAccessLevel minimum = PetAccessLevel.Viewer)
    {
        var now = DateTime.UtcNow;
        var minRole = minimum switch
        {
            PetAccessLevel.CoOwner or PetAccessLevel.Owner => CaretakerRole.CoOwner,
            PetAccessLevel.Caretaker => CaretakerRole.Caretaker,
            _ => CaretakerRole.Viewer
        };
        return db.Pets.Where(p => p.OwnerId == uid
            || (minimum != PetAccessLevel.Owner && p.Caretakers.Any(c =>
                    c.UserId == uid && !c.IsDeleted && c.Role >= minRole && (c.ExpiresAt == null || c.ExpiresAt > now))));
    }
}
