using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pawzaroo.Infrastructure.Identity;
using Xunit;

namespace Pawzaroo.Tests.Unit.Identity;

public class JwtKeyRingTests
{
    private const string OldKey = "old-signing-key-0123456789-abcdefghij-XYZ";
    private const string NewKey = "new-signing-key-0123456789-abcdefghij-XYZ";
    private const string OtherKey = "other-signing-key-0123456789-abcdefghij-X";

    private static JwtTokenService ServiceFor(string key)
        => new(Options.Create(new JwtSettings { SigningKey = key }));

    private static TokenValidationParameters Params(JwtKeyRing ring) => new()
    {
        ValidIssuer = "pawzaroo", ValidAudience = "pawzaroo.clients",
        IssuerSigningKeyResolver = ring.Resolver,
        ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true, ValidateLifetime = true,
    };

    private static string Issue(string key)
        => ServiceFor(key).GenerateAccessToken(Guid.NewGuid(), "a@b.c", new[] { "User" }, new[] { "x" }).Token;

    [Fact]
    public void Token_signed_with_previous_key_validates()
    {
        var token = Issue(OldKey);
        var ring = new JwtKeyRing(NewKey, new[] { OldKey });
        new JwtSecurityTokenHandler().ValidateToken(token, Params(ring), out var validated);
        validated.Should().NotBeNull();
    }

    [Fact]
    public void Token_signed_with_current_key_validates()
    {
        var token = Issue(NewKey);
        var ring = new JwtKeyRing(NewKey, new[] { OldKey });
        new JwtSecurityTokenHandler().ValidateToken(token, Params(ring), out _);
    }

    [Fact]
    public void Token_signed_with_unknown_key_fails()
    {
        var token = Issue(OtherKey);
        var ring = new JwtKeyRing(NewKey, new[] { OldKey });
        var act = () => new JwtSecurityTokenHandler().ValidateToken(token, Params(ring), out _);
        act.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void Previous_key_is_dropped_after_rotation_completes()
    {
        var token = Issue(OldKey);
        var ring = new JwtKeyRing(NewKey);
        var act = () => new JwtSecurityTokenHandler().ValidateToken(token, Params(ring), out _);
        act.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void New_tokens_use_current_key_and_carry_kid()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(Issue(NewKey));
        jwt.Header.Kid.Should().Be(JwtKeyRing.ComputeKid(NewKey));
        jwt.Header.Kid.Should().NotBe(JwtKeyRing.ComputeKid(OldKey));
        jwt.Header.Kid.Should().HaveLength(16);
    }

    [Fact]
    public void Token_without_kid_is_tried_against_all_keys()
    {
        var ring = new JwtKeyRing(NewKey, new[] { OldKey });
        ring.ResolveKeys(null).Should().HaveCount(2);
        ring.ResolveKeys("deadbeefdeadbeef").Should().BeEmpty();
    }

    [Fact]
    public void ReadPreviousKeys_supports_array_and_comma_separated()
    {
        var arr = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:PreviousSigningKeys:0"] = OldKey, ["Jwt:PreviousSigningKeys:1"] = OtherKey,
        }).Build();
        JwtKeyRing.ReadPreviousKeys(arr).Should().Equal(OldKey, OtherKey);

        var csv = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:PreviousSigningKeys"] = $"{OldKey}, {OtherKey} ;",
        }).Build();
        JwtKeyRing.ReadPreviousKeys(csv).Should().Equal(OldKey, OtherKey);
    }

    [Fact]
    public void Duplicate_of_current_key_is_ignored()
    {
        new JwtKeyRing(NewKey, new[] { NewKey, OldKey }).AllKeys.Should().HaveCount(2);
    }
}
