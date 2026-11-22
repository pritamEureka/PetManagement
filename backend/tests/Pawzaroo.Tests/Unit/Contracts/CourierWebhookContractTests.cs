using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Pawzaroo.Infrastructure.Features;
using Xunit;

namespace Pawzaroo.Tests.Unit.Contracts;

/// <summary>
/// Webhook contract for courier callbacks: signature = hex(HMAC-SHA256(secret, "{timestamp}.{rawBody}")) in
/// X-Pawzaroo-Signature, unix-seconds timestamp in X-Pawzaroo-Timestamp. The service must FAIL CLOSED.
/// Rejection paths are exercised through the real service (the downstream tracking service is never reached,
/// so null is passed); acceptance semantics are covered by the public static signature helpers.
/// </summary>
public class CourierWebhookContractTests
{
    private const string Secret = "unit-test-secret";
    private const string Body = "{\"trackingNumber\":\"T-1\",\"status\":\"delivered\"}";

    private static string Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

    private static CourierWebhookService Service(Dictionary<string, string?> settings) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), tracking: null!);

    private static Dictionary<string, string?> WithSecret(string secret = Secret) =>
        new() { ["Courier:WebhookSecret"] = secret };

    // ---------------- signature helpers ----------------

    [Fact]
    public void signature_is_lowercase_hex_sha256_and_deterministic()
    {
        var sig = CourierWebhookService.ComputeSignature(Secret, "1700000000", Body);
        sig.Should().MatchRegex("^[0-9a-f]{64}$");
        CourierWebhookService.ComputeSignature(Secret, "1700000000", Body).Should().Be(sig);
    }

    [Fact]
    public void valid_signature_verifies_with_and_without_the_sha256_prefix_and_in_any_case()
    {
        var sig = CourierWebhookService.ComputeSignature(Secret, "1700000000", Body);
        CourierWebhookService.VerifySignature(Secret, "1700000000", Body, sig).Should().BeTrue();
        CourierWebhookService.VerifySignature(Secret, "1700000000", Body, "sha256=" + sig).Should().BeTrue();
        CourierWebhookService.VerifySignature(Secret, "1700000000", Body, sig.ToUpperInvariant()).Should().BeTrue();
    }

    [Fact]
    public void wrong_secret_tampered_body_or_tampered_timestamp_fail_verification()
    {
        var sig = CourierWebhookService.ComputeSignature(Secret, "1700000000", Body);
        CourierWebhookService.VerifySignature("other-secret", "1700000000", Body, sig).Should().BeFalse();
        CourierWebhookService.VerifySignature(Secret, "1700000000", Body + " ", sig).Should().BeFalse();
        CourierWebhookService.VerifySignature(Secret, "1700000001", Body, sig).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("sha256=")]
    [InlineData("deadbeef")]
    [InlineData("not hex at all")]
    public void malformed_signatures_fail_without_throwing(string header)
    {
        var act = () => CourierWebhookService.VerifySignature(Secret, "1700000000", Body, header);
        act.Should().NotThrow();
        act().Should().BeFalse();
    }

    // ---------------- service: fail closed ----------------

    [Fact]
    public async Task missing_secret_rejects_even_a_correctly_signed_request()
    {
        var ts = Now();
        var sig = CourierWebhookService.ComputeSignature(Secret, ts, Body);
        var svc = Service(new Dictionary<string, string?>());   // nothing configured

        (await svc.HandleAsync("pathao", Body, sig, ts)).Should().Be(WebhookResult.Unauthorized);
    }

    [Fact]
    public async Task empty_secret_is_treated_as_missing()
    {
        var ts = Now();
        var sig = CourierWebhookService.ComputeSignature("", ts, Body);
        var svc = Service(WithSecret(""));

        (await svc.HandleAsync("pathao", Body, sig, ts)).Should().Be(WebhookResult.Unauthorized);
    }

    [Fact]
    public async Task wrong_secret_is_rejected()
    {
        var ts = Now();
        var sig = CourierWebhookService.ComputeSignature("attacker-guess", ts, Body);
        (await Service(WithSecret()).HandleAsync("pathao", Body, sig, ts)).Should().Be(WebhookResult.Unauthorized);
    }

    [Fact]
    public async Task stale_timestamp_is_rejected_even_with_a_valid_signature()
    {
        var ts = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString();
        var sig = CourierWebhookService.ComputeSignature(Secret, ts, Body);
        (await Service(WithSecret()).HandleAsync("pathao", Body, sig, ts)).Should().Be(WebhookResult.Unauthorized);
    }

    [Fact]
    public async Task far_future_timestamp_is_rejected()
    {
        var ts = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString();
        var sig = CourierWebhookService.ComputeSignature(Secret, ts, Body);
        (await Service(WithSecret()).HandleAsync("pathao", Body, sig, ts)).Should().Be(WebhookResult.Unauthorized);
    }

    [Theory]
    [InlineData(null, "123")]
    [InlineData("", "123")]
    [InlineData("abc", null)]
    [InlineData("abc", "")]
    [InlineData("abc", "not-a-number")]
    [InlineData("abc", "-5")]
    public async Task missing_or_malformed_headers_are_rejected(string? signature, string? timestamp)
    {
        (await Service(WithSecret()).HandleAsync("pathao", Body, signature, timestamp)).Should().Be(WebhookResult.Unauthorized);
    }

    [Fact]
    public async Task body_tampering_after_signing_is_rejected()
    {
        var ts = Now();
        var sig = CourierWebhookService.ComputeSignature(Secret, ts, Body);
        var tampered = Body.Replace("delivered", "returned");
        (await Service(WithSecret()).HandleAsync("pathao", tampered, sig, ts)).Should().Be(WebhookResult.Unauthorized);
    }

    [Fact]
    public async Task authentic_request_for_an_unknown_provider_is_reported_as_such_not_accepted()
    {
        var ts = Now();
        var sig = CourierWebhookService.ComputeSignature(Secret, ts, Body);
        (await Service(WithSecret()).HandleAsync("definitely-not-a-courier", Body, sig, ts))
            .Should().Be(WebhookResult.UnknownProvider);
    }

    [Fact]
    public async Task provider_specific_secret_takes_precedence_over_the_global_one()
    {
        var settings = WithSecret("global");
        settings["Courier:Providers:acme:WebhookSecret"] = "acme-secret";
        var ts = Now();

        var withGlobal = CourierWebhookService.ComputeSignature("global", ts, Body);
        (await Service(settings).HandleAsync("acme", Body, withGlobal, ts)).Should().Be(WebhookResult.Unauthorized);

        var withProvider = CourierWebhookService.ComputeSignature("acme-secret", ts, Body);
        (await Service(settings).HandleAsync("acme", Body, withProvider, ts)).Should().Be(WebhookResult.UnknownProvider); // authentic, provider not a known courier
    }

    // ---------------- payload parsing contract (internal; falls back to public helpers) ----------------

    [Theory]
    [InlineData("delivered", true)]
    [InlineData("Out For Delivery", true)]
    [InlineData("in_transit", true)]
    [InlineData("zzz-unknown", false)]
    public void status_vocabulary_is_normalised(string raw, bool known)
    {
        (CourierWebhookService.NormalizeStatus(raw) is not null).Should().Be(known);
    }
}
