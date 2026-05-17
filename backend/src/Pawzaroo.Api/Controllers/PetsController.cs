using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Api.Controllers;

public record UpsertPetRequest(string Name, AnimalType AnimalType, string? Breed, Gender Gender, DateTime? BirthDate, decimal? WeightKg, string? Color, string? TagNumber, string? PrimaryPhotoUrl, string? Allergies, string? DietNotes, bool IsAvailableForAdoption);

[ApiController]
[Route("api/pets")]
[Authorize]
public class PetsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public PetsController(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new UnauthorizedAccessException();
        var pets = await _db.Pets.AsNoTracking().Where(p => p.OwnerId == uid).ToListAsync(ct);
        return Ok(pets);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var pet = await _db.Pets.AsNoTracking()
            .Include(p => p.Photos)
            .Include(p => p.Vaccinations)
            .Include(p => p.MedicalRecords)
            .Include(p => p.GroomingRecords)
            .SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Pet not found.");
        return Ok(pet);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertPetRequest req, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new UnauthorizedAccessException();
        var pet = new Pet
        {
            OwnerId = uid,
            Name = req.Name,
            AnimalType = req.AnimalType,
            Breed = req.Breed,
            Gender = req.Gender,
            BirthDate = req.BirthDate,
            WeightKg = req.WeightKg,
            Color = req.Color,
            TagNumber = req.TagNumber,
            PrimaryPhotoUrl = req.PrimaryPhotoUrl,
            Allergies = req.Allergies,
            DietNotes = req.DietNotes,
            IsAvailableForAdoption = req.IsAvailableForAdoption
        };
        _db.Pets.Add(pet);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = pet.Id }, pet);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertPetRequest req, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new UnauthorizedAccessException();
        var pet = await _db.Pets.SingleOrDefaultAsync(p => p.Id == id && p.OwnerId == uid, ct)
            ?? throw new KeyNotFoundException("Pet not found.");
        pet.Name = req.Name; pet.AnimalType = req.AnimalType; pet.Breed = req.Breed;
        pet.Gender = req.Gender; pet.BirthDate = req.BirthDate; pet.WeightKg = req.WeightKg;
        pet.Color = req.Color; pet.TagNumber = req.TagNumber; pet.PrimaryPhotoUrl = req.PrimaryPhotoUrl;
        pet.Allergies = req.Allergies; pet.DietNotes = req.DietNotes;
        pet.IsAvailableForAdoption = req.IsAvailableForAdoption;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new UnauthorizedAccessException();
        var pet = await _db.Pets.SingleOrDefaultAsync(p => p.Id == id && p.OwnerId == uid, ct)
            ?? throw new KeyNotFoundException("Pet not found.");
        _db.Pets.Remove(pet);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
