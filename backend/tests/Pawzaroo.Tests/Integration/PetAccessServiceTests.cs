using FluentAssertions;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Modules.Pets;
using Pawzaroo.Shared.Exceptions;
using Xunit;

namespace Pawzaroo.Tests.Integration;

/// <summary>
/// The pet record access policy against a real Postgres (Testcontainers).
/// Regression guard for the old GET /api/pets/{id} that returned any pet's
/// medical history to any signed-in user.
/// </summary>
public class PetAccessServiceTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fx;

    public PetAccessServiceTests(PostgresFixture fx) => _fx = fx;

    private sealed class FakeUser : ICurrentUserService
    {
        public FakeUser(Guid? id, params string[] permissions) { UserId = id; Permissions = permissions; }
        public Guid? UserId { get; }
        public string? Email => null;
        public IReadOnlyCollection<string> Permissions { get; }
        public bool IsAuthenticated => UserId.HasValue;
    }

    private User NewUser(string name)
    {
        var u = new User { Email = $"{name}-{Guid.NewGuid():N}@test.local", PasswordHash = "x", DisplayName = name };
        _fx.Db.Users.Add(u);
        return u;
    }

    private async Task<(Pet Pet, Dictionary<string, Guid> Users)> SeedAsync()
    {
        var owner = NewUser("owner");
        var coOwner = NewUser("coowner");
        var sitter = NewUser("sitter");
        var viewer = NewUser("viewer");
        var expired = NewUser("expired");
        var vetUser = NewUser("vet");
        var stranger = NewUser("stranger");

        var pet = new Pet { OwnerId = owner.Id, Name = "Milo", AnimalType = AnimalType.Dog };
        _fx.Db.Pets.Add(pet);
        _fx.Db.PetCaretakers.AddRange(
            new PetCaretaker { PetId = pet.Id, UserId = coOwner.Id, Role = CaretakerRole.CoOwner, InvitedById = owner.Id },
            new PetCaretaker { PetId = pet.Id, UserId = sitter.Id, Role = CaretakerRole.Caretaker, InvitedById = owner.Id },
            new PetCaretaker { PetId = pet.Id, UserId = viewer.Id, Role = CaretakerRole.Viewer, InvitedById = owner.Id },
            new PetCaretaker { PetId = pet.Id, UserId = expired.Id, Role = CaretakerRole.CoOwner, InvitedById = owner.Id,
                               ExpiresAt = DateTime.UtcNow.AddDays(-1) });

        var doctor = new Doctor { UserId = vetUser.Id, LicenseNumber = $"LIC-{Guid.NewGuid():N}"[..20], ApprovalStatus = ApprovalStatus.Approved };
        _fx.Db.Doctors.Add(doctor);
        _fx.Db.Appointments.Add(new Appointment
        {
            DoctorId = doctor.Id, PatientUserId = owner.Id, PetId = pet.Id, ScheduledAt = DateTime.UtcNow.AddDays(1),
            Status = AppointmentStatus.Confirmed, Type = ConsultationType.Offline
        });
        await _fx.Db.SaveChangesAsync();

        return (pet, new Dictionary<string, Guid>
        {
            ["owner"] = owner.Id, ["coowner"] = coOwner.Id, ["sitter"] = sitter.Id, ["viewer"] = viewer.Id,
            ["expired"] = expired.Id, ["vet"] = vetUser.Id, ["stranger"] = stranger.Id
        });
    }

    private PetAccessService As(Guid userId, params string[] permissions) => new(_fx.Db, new FakeUser(userId, permissions));

    [Fact]
    public async Task resolves_access_level_for_each_relationship()
    {
        var (pet, u) = await SeedAsync();

        (await As(u["owner"]).GetLevelAsync(pet.Id)).Should().Be(PetAccessLevel.Owner);
        (await As(u["coowner"]).GetLevelAsync(pet.Id)).Should().Be(PetAccessLevel.CoOwner);
        (await As(u["sitter"]).GetLevelAsync(pet.Id)).Should().Be(PetAccessLevel.Caretaker);
        (await As(u["viewer"]).GetLevelAsync(pet.Id)).Should().Be(PetAccessLevel.Viewer);
        (await As(u["vet"]).GetLevelAsync(pet.Id)).Should().Be(PetAccessLevel.Vet);
        (await As(u["expired"]).GetLevelAsync(pet.Id)).Should().Be(PetAccessLevel.None);
        (await As(u["stranger"]).GetLevelAsync(pet.Id)).Should().Be(PetAccessLevel.None);
    }

    [Fact]
    public async Task strangers_cannot_read_and_get_not_found_rather_than_forbidden()
    {
        var (pet, u) = await SeedAsync();

        await As(u["stranger"]).Invoking(s => s.EnsureCanReadAsync(pet.Id)).Should().ThrowAsync<NotFoundException>();
        await As(u["expired"]).Invoking(s => s.EnsureCanReadAsync(pet.Id)).Should().ThrowAsync<NotFoundException>();
        // Support staff with pets.read_any can read but still can't edit.
        var support = As(u["stranger"], Permissions.Pets.ReadAny);
        await support.Invoking(s => s.EnsureCanReadAsync(pet.Id)).Should().NotThrowAsync();
        await support.Invoking(s => s.EnsureCanEditAsync(pet.Id)).Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task write_permissions_follow_the_role_ladder()
    {
        var (pet, u) = await SeedAsync();

        // Viewer: read only.
        await As(u["viewer"]).Invoking(s => s.EnsureCanLogCareAsync(pet.Id)).Should().ThrowAsync<ForbiddenException>();
        // Caretaker: logs care, can't edit the profile.
        await As(u["sitter"]).Invoking(s => s.EnsureCanLogCareAsync(pet.Id)).Should().NotThrowAsync();
        await As(u["sitter"]).Invoking(s => s.EnsureCanEditAsync(pet.Id)).Should().ThrowAsync<ForbiddenException>();
        // Treating vet: clinical records yes, profile no.
        await As(u["vet"]).Invoking(s => s.EnsureCanEditClinicalAsync(pet.Id)).Should().NotThrowAsync();
        await As(u["vet"]).Invoking(s => s.EnsureCanEditAsync(pet.Id)).Should().ThrowAsync<ForbiddenException>();
        // Co-owner: edits, but owner-only actions stay with the owner.
        await As(u["coowner"]).Invoking(s => s.EnsureCanEditAsync(pet.Id)).Should().NotThrowAsync();
        await As(u["coowner"]).Invoking(s => s.EnsureOwnerAsync(pet.Id)).Should().ThrowAsync<ForbiddenException>();
        await As(u["owner"]).Invoking(s => s.EnsureOwnerAsync(pet.Id)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task accessible_pets_query_respects_minimum_role_and_expiry()
    {
        var (pet, u) = await SeedAsync();
        bool Sees(string who, PetAccessLevel min) =>
            PetAccessService.AccessiblePets(_fx.Db, u[who], min).Any(p => p.Id == pet.Id);

        Sees("viewer", PetAccessLevel.Viewer).Should().BeTrue();
        Sees("viewer", PetAccessLevel.Caretaker).Should().BeFalse();
        Sees("sitter", PetAccessLevel.Caretaker).Should().BeTrue();
        Sees("coowner", PetAccessLevel.CoOwner).Should().BeTrue();
        Sees("expired", PetAccessLevel.Viewer).Should().BeFalse();
        Sees("stranger", PetAccessLevel.Viewer).Should().BeFalse();
    }
}
