using FluentAssertions;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.SecurityPlus;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Modules.Pets;
using Pawzaroo.Shared.Exceptions;
using Pawzaroo.Domain.Common;
using Xunit;

namespace Pawzaroo.Tests.Integration;

/// <summary>
/// Object-level authorization for the pet-sensitive services: strangers and low-privilege caretakers
/// must be denied (404 for strangers, 403 for insufficient role) against a real Postgres.
/// </summary>
public class PetSharingAndCareAccessTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fx;
    public PetSharingAndCareAccessTests(PostgresFixture fx) => _fx = fx;

    private sealed class FakeUser : ICurrentUserService
    {
        public FakeUser(Guid? id) => UserId = id;
        public Guid? UserId { get; }
        public string? Email => null;
        public IReadOnlyCollection<string> Permissions => Array.Empty<string>();
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class NoopKafka : IKafkaProducer
    {
        public Task PublishAsync<T>(string topic, T message, string? key = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoopAudit : IAuditLogger
    {
        public Task LogAsync(string action, string entityName, string? entityId, string? module = null,
            object? oldValues = null, object? newValues = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class AlwaysStepUp : IStepUpAuthService
    {
        public Task<bool> VerifyAsync(StepUpInput input, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasRecentAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(true);
        public Task RequireRecentAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private User NewUser(string name)
    {
        var u = new User { Email = $"{name}-{Guid.NewGuid():N}@test.local", PasswordHash = "x", DisplayName = name };
        _fx.Db.Users.Add(u);
        return u;
    }

    private async Task<(Guid PetId, Dictionary<string, User> Users)> SeedAsync()
    {
        var users = new[] { "owner", "coowner", "caretaker", "viewer", "stranger", "invitee" }
            .ToDictionary(n => n, NewUser);
        var pet = new Pet { OwnerId = users["owner"].Id, Name = "Biscuit", AnimalType = AnimalType.Cat };
        _fx.Db.Pets.Add(pet);
        _fx.Db.PetCaretakers.AddRange(
            new PetCaretaker { PetId = pet.Id, UserId = users["coowner"].Id, Role = CaretakerRole.CoOwner, InvitedById = users["owner"].Id },
            new PetCaretaker { PetId = pet.Id, UserId = users["caretaker"].Id, Role = CaretakerRole.Caretaker, InvitedById = users["owner"].Id },
            new PetCaretaker { PetId = pet.Id, UserId = users["viewer"].Id, Role = CaretakerRole.Viewer, InvitedById = users["owner"].Id });
        await _fx.Db.SaveChangesAsync();
        return (pet.Id, users);
    }

    private PetCareService Care(Guid userId)
    {
        var current = new FakeUser(userId);
        return new PetCareService(_fx.Db, current, new PetAccessService(_fx.Db, current), new NoopAudit());
    }

    private PetSharingService Sharing(Guid userId)
    {
        var current = new FakeUser(userId);
        return new PetSharingService(_fx.Db, current, new PetAccessService(_fx.Db, current),
            new NoopKafka(), new NoopAudit(), new AlwaysStepUp());
    }

    // ---------------- PetCareService ----------------

    [Fact]
    public async Task stranger_cannot_read_weights_and_gets_not_found()
    {
        var (petId, u) = await SeedAsync();
        var act = () => Care(u["stranger"].Id).ListWeightsAsync(petId);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task viewer_can_read_but_not_log_weight()
    {
        var (petId, u) = await SeedAsync();
        await Care(u["viewer"].Id).ListWeightsAsync(petId);

        var act = () => Care(u["viewer"].Id).AddWeightAsync(petId, new WeightLogInput(DateTime.UtcNow.Date, 4.2m, null));
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task caretaker_can_log_weight()
    {
        var (petId, u) = await SeedAsync();
        var created = await Care(u["caretaker"].Id).AddWeightAsync(petId, new WeightLogInput(DateTime.UtcNow.Date, 4.2m, "ok"));
        created.WeightKg.Should().Be(4.2m);
    }

    [Theory]
    [InlineData("caretaker")]
    [InlineData("viewer")]
    [InlineData("coowner")]
    public async Task only_the_owner_can_invite_caretakers(string actor)
    {
        var (petId, u) = await SeedAsync();
        var input = new InviteCaretakerInput(u["invitee"].Email, CaretakerRole.Viewer, null);

        var act = () => Care(u[actor].Id).InviteCaretakerAsync(petId, input);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task stranger_cannot_invite_caretakers()
    {
        var (petId, u) = await SeedAsync();
        var act = () => Care(u["stranger"].Id).InviteCaretakerAsync(petId,
            new InviteCaretakerInput(u["invitee"].Email, CaretakerRole.Viewer, null));
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task owner_can_invite_a_caretaker()
    {
        var (petId, u) = await SeedAsync();
        var dto = await Care(u["owner"].Id).InviteCaretakerAsync(petId,
            new InviteCaretakerInput(u["invitee"].Email, CaretakerRole.Viewer, null));
        dto.UserId.Should().Be(u["invitee"].Id);
    }

    // ---------------- PetSharingService ----------------

    [Fact]
    public async Task stranger_cannot_list_or_create_share_links()
    {
        var (petId, u) = await SeedAsync();
        var list = () => Sharing(u["stranger"].Id).ListShareLinksAsync(petId);
        var create = () => Sharing(u["stranger"].Id).CreateShareLinkAsync(petId, new CreateShareLinkInput(7, "x"));
        await list.Should().ThrowAsync<NotFoundException>();
        await create.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData("caretaker")]
    [InlineData("viewer")]
    public async Task low_privilege_caretakers_cannot_create_share_links(string actor)
    {
        var (petId, u) = await SeedAsync();
        var act = () => Sharing(u[actor].Id).CreateShareLinkAsync(petId, new CreateShareLinkInput(7, null));
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task owner_can_create_and_revoke_a_share_link_but_a_stranger_cannot_revoke_it()
    {
        var (petId, u) = await SeedAsync();
        var link = await Sharing(u["owner"].Id).CreateShareLinkAsync(petId, new CreateShareLinkInput(7, "vet"));
        link.Token.Should().NotBeNullOrWhiteSpace();

        var revokeByStranger = () => Sharing(u["stranger"].Id).RevokeShareLinkAsync(petId, link.Id);
        await revokeByStranger.Should().ThrowAsync<NotFoundException>();

        await Sharing(u["owner"].Id).RevokeShareLinkAsync(petId, link.Id);
        (await Sharing(u["owner"].Id).ListShareLinksAsync(petId)).Single(l => l.Id == link.Id).RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task revoked_share_link_no_longer_resolves_a_shared_report()
    {
        var (petId, u) = await SeedAsync();
        var link = await Sharing(u["owner"].Id).CreateShareLinkAsync(petId, new CreateShareLinkInput(7, null));
        await Sharing(u["owner"].Id).RevokeShareLinkAsync(petId, link.Id);

        var report = await Sharing(u["stranger"].Id).GetSharedHealthReportAsync(link.Token);
        report.Should().BeNull();
    }
}
