using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Feed;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Domain.Social;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Feed;

/// <summary>Followable pet profiles and albums. Album visibility is enforced here, never in the client.</summary>
public class PetSocialService : IPetSocialService
{
    private const int MaxAlbumsPerPet = 20;
    private const int MaxPhotosPerAlbum = 200;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPetAccessService _access;

    public PetSocialService(ApplicationDbContext db, ICurrentUserService current, IPetAccessService access)
    {
        _db = db;
        _current = current;
        _access = access;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    private static Exception Invalid(string field, string msg) =>
        new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { [field] = new[] { msg } });

    private static PetAlbumPhotoDto Map(PetAlbumPhoto p) => new(p.Id, p.Url, p.Caption, p.TakenAt, p.SortOrder);

    private async Task<Pet> RequirePetAsync(Guid petId, CancellationToken ct) =>
        await _db.Pets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == petId, ct) ?? throw new NotFoundException("Pet", petId);

    public async Task<PetSocialProfileDto> GetProfileAsync(Guid petId, CancellationToken ct = default)
    {
        var pet = await RequirePetAsync(petId, ct);
        var uid = _current.UserId;
        var isOwner = uid is { } u && pet.OwnerId == u;
        if (!pet.PublicProfileEnabled && !isOwner) throw new NotFoundException("Pet", petId);
        return await BuildAsync(pet, uid, isOwner, ct);
    }

    public async Task<PetSocialProfileDto?> GetProfileByCodeAsync(string code, CancellationToken ct = default)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        var pet = await _db.Pets.AsNoTracking().FirstOrDefaultAsync(p => p.PublicCode == code && p.PublicProfileEnabled, ct);
        if (pet is null) return null;
        var uid = _current.UserId;
        return await BuildAsync(pet, uid, uid is { } u && pet.OwnerId == u, ct);
    }

    private async Task<PetSocialProfileDto> BuildAsync(Pet pet, Guid? uid, bool isOwner, CancellationToken ct)
    {
        var followers = await _db.Set<PetFollow>().CountAsync(f => f.PetId == pet.Id, ct);
        var following = uid is { } u && await _db.Set<PetFollow>().AnyAsync(f => f.PetId == pet.Id && f.UserId == u, ct);
        var albums = _db.Set<PetAlbum>().AsNoTracking().Include(a => a.Photos).Where(a => a.PetId == pet.Id);
        if (!isOwner)
        {
            albums = following
                ? albums.Where(a => a.Visibility == AlbumVisibility.Public || a.Visibility == AlbumVisibility.Followers)
                : albums.Where(a => a.Visibility == AlbumVisibility.Public);
        }
        var list = await albums.OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
        var dtos = list.Select(a =>
        {
            var photos = a.Photos.OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt).ToList();
            return new PetAlbumDto(a.Id, a.PetId, a.Title, a.Visibility, a.CoverUrl ?? photos.FirstOrDefault()?.Url,
                photos.Count, photos.Select(Map).ToList());
        }).ToList();
        return new PetSocialProfileDto(pet.Id, pet.Name, pet.PrimaryPhotoUrl, pet.AnimalType.ToString(), pet.Breed,
            followers, following, isOwner, dtos);
    }

    public async Task FollowAsync(Guid petId, CancellationToken ct = default)
    {
        var uid = Uid();
        var pet = await RequirePetAsync(petId, ct);
        if (!pet.PublicProfileEnabled && pet.OwnerId != uid) throw new NotFoundException("Pet", petId);
        if (await _db.Set<PetFollow>().AnyAsync(f => f.PetId == petId && f.UserId == uid, ct)) return;
        _db.Set<PetFollow>().Add(new PetFollow { PetId = petId, UserId = uid });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { /* concurrent duplicate follow: already following */ }
    }

    public async Task UnfollowAsync(Guid petId, CancellationToken ct = default)
    {
        var uid = Uid();
        await _db.Set<PetFollow>().Where(f => f.PetId == petId && f.UserId == uid).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<FollowedPetPhotoDto>> FollowedPhotosAsync(int take, CancellationToken ct = default)
    {
        var uid = Uid();
        take = Math.Clamp(take, 1, 50);
        var rows = await (from f in _db.Set<PetFollow>().AsNoTracking()
                          where f.UserId == uid
                          join a in _db.Set<PetAlbum>().AsNoTracking() on f.PetId equals a.PetId
                          where a.Visibility != AlbumVisibility.Private
                          join p in _db.Set<PetAlbumPhoto>().AsNoTracking() on a.Id equals p.AlbumId
                          join pet in _db.Pets.AsNoTracking() on a.PetId equals pet.Id
                          orderby p.CreatedAt descending
                          select new { pet.Id, pet.Name, AlbumId = a.Id, a.Title, Photo = p })
            .Take(take).ToListAsync(ct);
        return rows.Select(r => new FollowedPetPhotoDto(r.Id, r.Name, r.AlbumId, r.Title, Map(r.Photo))).ToList();
    }

    // ---------------------------------------------------------------- owner-managed albums

    private async Task<PetAlbum> OwnedAlbumAsync(Guid albumId, CancellationToken ct)
    {
        var album = await _db.Set<PetAlbum>().Include(a => a.Photos).FirstOrDefaultAsync(a => a.Id == albumId, ct)
            ?? throw new NotFoundException("Album", albumId);
        await _access.EnsureOwnerAsync(album.PetId, ct);
        return album;
    }

    private static string? CleanUrl(string? u)
    {
        if (string.IsNullOrWhiteSpace(u)) return null;
        if (u.Length > 1000 || !Uri.TryCreate(u.Trim(), UriKind.RelativeOrAbsolute, out _)) throw Invalid("url", "Invalid URL.");
        return u.Trim();
    }

    private static PetAlbumDto ToDto(PetAlbum a)
    {
        var photos = a.Photos.OrderBy(p => p.SortOrder).ToList();
        return new PetAlbumDto(a.Id, a.PetId, a.Title, a.Visibility, a.CoverUrl ?? photos.FirstOrDefault()?.Url, photos.Count,
            photos.Select(Map).ToList());
    }

    public async Task<PetAlbumDto> CreateAlbumAsync(Guid petId, CreatePetAlbumRequest req, CancellationToken ct = default)
    {
        await _access.EnsureOwnerAsync(petId, ct);
        var title = req.Title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 120) throw Invalid("title", "Title is required (max 120 characters).");
        if (!Enum.IsDefined(req.Visibility)) throw Invalid("visibility", "Invalid visibility.");
        if (await _db.Set<PetAlbum>().CountAsync(a => a.PetId == petId, ct) >= MaxAlbumsPerPet)
            throw new ConflictException("Album limit reached.");
        var a = new PetAlbum { PetId = petId, Title = title, Visibility = req.Visibility, CoverUrl = CleanUrl(req.CoverUrl) };
        _db.Set<PetAlbum>().Add(a);
        await _db.SaveChangesAsync(ct);
        return ToDto(a);
    }

    public async Task<PetAlbumDto> UpdateAlbumAsync(Guid albumId, UpdatePetAlbumRequest req, CancellationToken ct = default)
    {
        var a = await OwnedAlbumAsync(albumId, ct);
        if (req.Title is { } t)
        {
            t = t.Trim();
            if (t.Length is 0 or > 120) throw Invalid("title", "Title is required (max 120 characters).");
            a.Title = t;
        }
        if (req.Visibility is { } v)
        {
            if (!Enum.IsDefined(v)) throw Invalid("visibility", "Invalid visibility.");
            a.Visibility = v;
        }
        if (req.CoverUrl is not null) a.CoverUrl = CleanUrl(req.CoverUrl);
        await _db.SaveChangesAsync(ct);
        return ToDto(a);
    }

    public async Task DeleteAlbumAsync(Guid albumId, CancellationToken ct = default)
    {
        var a = await OwnedAlbumAsync(albumId, ct);
        _db.Set<PetAlbum>().Remove(a);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PetAlbumPhotoDto> AddPhotoAsync(Guid albumId, AddAlbumPhotoRequest req, CancellationToken ct = default)
    {
        var a = await OwnedAlbumAsync(albumId, ct);
        var url = CleanUrl(req.Url) ?? throw Invalid("url", "Photo URL is required.");
        if (a.Photos.Count >= MaxPhotosPerAlbum) throw new ConflictException("Photo limit reached for this album.");
        var caption = string.IsNullOrWhiteSpace(req.Caption) ? null : req.Caption.Trim();
        if (caption is { Length: > 300 }) throw Invalid("caption", "Caption is too long.");
        var photo = new PetAlbumPhoto
        {
            AlbumId = albumId, Url = url, Caption = caption,
            TakenAt = req.TakenAt is { } d ? (d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime()) : null,
            SortOrder = a.Photos.Count == 0 ? 0 : a.Photos.Max(p => p.SortOrder) + 1
        };
        _db.Set<PetAlbumPhoto>().Add(photo);
        await _db.SaveChangesAsync(ct);
        return Map(photo);
    }

    public async Task DeletePhotoAsync(Guid albumId, Guid photoId, CancellationToken ct = default)
    {
        await OwnedAlbumAsync(albumId, ct);
        await _db.Set<PetAlbumPhoto>().Where(p => p.Id == photoId && p.AlbumId == albumId).ExecuteDeleteAsync(ct);
    }
}
