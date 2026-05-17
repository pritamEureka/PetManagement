using FluentAssertions;
using Pawzaroo.Application.Common.Permissions;
using Xunit;

namespace Pawzaroo.Tests.Unit;

public class PermissionsCatalogTests
{
    [Fact]
    public void Catalog_emits_every_permission_as_module_action_pair()
    {
        var all = Permissions.All().ToList();
        all.Should().NotBeEmpty();
        all.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Module) && !string.IsNullOrWhiteSpace(p.Action));
    }

    [Fact]
    public void Catalog_includes_canonical_actions()
    {
        var codes = Permissions.All().Select(p => $"{p.Module}.{p.Action}").ToHashSet();
        codes.Should().Contain(new[]
        {
            "users.view", "posts.create", "adoption.approve", "vets.approve",
            "stores.refund", "products.feature", "orders.refund", "roles.assign"
        });
    }
}
