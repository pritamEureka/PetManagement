using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Pawzaroo.Infrastructure.Features;
using Xunit;
using static Pawzaroo.Tests.Integration.Api.ApiHttp;

namespace Pawzaroo.Tests.Integration.Api;

/// <summary>
/// Real-HTTP tests against the in-process API. Requires Docker (Postgres) and Redis on localhost:6379 (skipped without it).
/// NOTE: the "auth" rate-limit policy allows 10 requests/min per client (TestServer = one "anon" partition). Keep the total
/// number of /auth/* calls across this collection below 10 (currently 7: 1 superadmin login, 3 flow, 1 weak password,
/// 1 bad credentials, 1 malformed body).
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Requires", "Redis")]
[Trait("Requires", "Docker")]
public class ApiEndpointsTests
{
    private readonly ApiFactory _api;
    public ApiEndpointsTests(ApiFactory api) => _api = api;

    private static string? _superToken;
    private static readonly SemaphoreSlim TokenLock = new(1, 1);

    private async Task<string> SuperAdminTokenAsync()
    {
        await TokenLock.WaitAsync();
        try { return _superToken ??= await LoginAsync(_api.CreateClient(), ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword); }
        finally { TokenLock.Release(); }
    }

    // ---------------- health + public endpoints ----------------

    [RedisFact]
    public async Task Liveness_endpoint_is_200()
    {
        var res = await _api.CreateClient().GetAsync("/health");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RedisTheory]
    [InlineData("/api/v1/posts")]
    [InlineData("/api/v1/products")]
    [InlineData("/api/v1/stores")]
    [InlineData("/api/v1/vets")]
    [InlineData("/api/v1/vets/specialties")]
    [InlineData("/api/v1/adoption/listings")]
    public async Task Public_anonymous_endpoints_return_200_with_success_envelope(string url)
    {
        var res = await _api.CreateClient().GetAsync(url);
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var doc = await res.JsonBodyAsync();
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.TryGetProperty("data", out _).Should().BeTrue();
    }

    // ---------------- 401 / 403 ----------------

    [RedisTheory]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/roles")]
    [InlineData("/api/v1/admin/users")]
    public async Task Protected_endpoints_reject_anonymous_with_401(string url)
    {
        var res = await _api.CreateClient().GetAsync(url);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RedisFact]
    public async Task Garbage_bearer_token_is_401()
    {
        var res = await _api.CreateClient().SendAsync(Req(HttpMethod.Get, "/api/v1/auth/me", token: "not.a.jwt"));
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RedisFact]
    public async Task Superadmin_can_call_permission_guarded_endpoints_and_me_reports_identity()
    {
        var token = await SuperAdminTokenAsync();
        var http = _api.CreateClient();

        var roles = await http.SendAsync(Req(HttpMethod.Get, "/api/v1/roles", token));
        roles.StatusCode.Should().Be(HttpStatusCode.OK, await roles.Content.ReadAsStringAsync());

        var me = await http.SendAsync(Req(HttpMethod.Get, "/api/v1/auth/me", token));
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = await me.JsonBodyAsync();
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data").GetProperty("email").GetString().Should().Be(ApiFactory.SuperAdminEmail);
    }

    // ---------------- register -> pending -> approve -> login -> 403 on admin ----------------

    [RedisFact]
    public async Task Register_is_pending_until_approved_then_login_works_and_plain_user_gets_403_on_admin_endpoints()
    {
        var http = _api.CreateClient();
        var email = $"it-{Guid.NewGuid():N}@pawzaroo.test";
        const string password = "Passw0rdIT1";

        var reg = await http.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password, displayName = "IT User" });
        reg.StatusCode.Should().Be(HttpStatusCode.OK, await reg.Content.ReadAsStringAsync());
        Guid userId;
        using (var doc = await reg.JsonBodyAsync())
        {
            doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
            userId = doc.RootElement.GetProperty("data").GetProperty("userId").GetGuid();
        }

        // Pending accounts cannot log in; error is the wrapped ApiResponse with a stable code.
        var pending = await http.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        pending.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using (var doc = await pending.JsonBodyAsync())
        {
            doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            doc.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("registration_pending");
        }

        var superToken = await SuperAdminTokenAsync();
        var approve = await http.SendAsync(Req(HttpMethod.Post, $"/api/v1/admin/users/{userId}/approve", superToken, new { notes = "it" }));
        approve.StatusCode.Should().Be(HttpStatusCode.NoContent, await approve.Content.ReadAsStringAsync());

        var userToken = await LoginAsync(http, email, password);

        (await http.SendAsync(Req(HttpMethod.Get, "/api/v1/auth/me", userToken))).StatusCode.Should().Be(HttpStatusCode.OK);

        // Authenticated but lacking the permission -> 403 (not 401).
        (await http.SendAsync(Req(HttpMethod.Get, "/api/v1/roles", userToken))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await http.SendAsync(Req(HttpMethod.Post, $"/api/v1/admin/users/{userId}/approve", userToken, new { notes = "x" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------- error shapes ----------------

    [RedisFact]
    public async Task Weak_password_registration_returns_400_validation_envelope_with_details()
    {
        var res = await _api.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { email = $"weak-{Guid.NewGuid():N}@pawzaroo.test", password = "short", displayName = "Weak" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var doc = await res.JsonBodyAsync();
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        var err = doc.RootElement.GetProperty("error");
        err.GetProperty("code").GetString().Should().NotBeNullOrEmpty();
        err.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
        err.GetProperty("details").EnumerateObject().Should().NotBeEmpty();
    }

    [RedisFact]
    public async Task Bad_credentials_are_403_with_failure_envelope()
    {
        var res = await _api.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = $"nobody-{Guid.NewGuid():N}@pawzaroo.test", password = "Whatever123" });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var doc = await res.JsonBodyAsync();
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").TryGetProperty("code", out _).Should().BeTrue();
    }

    [RedisFact]
    public async Task Malformed_json_body_yields_problem_details_400()
    {
        var content = new StringContent("{ not json", Encoding.UTF8, "application/json");
        var res = await _api.CreateClient().PostAsync("/api/v1/auth/login", content);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var doc = await res.JsonBodyAsync();
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(400);
    }

    [RedisFact]
    public async Task Unknown_route_is_404()
    {
        (await _api.CreateClient().GetAsync("/api/v1/definitely-not-a-route")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------- [Idempotent] replay (test-only probe controller) ----------------

    [RedisFact]
    public async Task Idempotent_post_replays_identical_body_and_marks_header_and_does_not_re_execute()
    {
        var http = _api.CreateClient();
        var token = await SuperAdminTokenAsync();
        var key = Guid.NewGuid().ToString();
        var before = IdempotencyProbeController.Executions;

        HttpRequestMessage Make(string value, string k) =>
            WithKey(Req(HttpMethod.Post, "/test-support/idempotent", token, new { value }), k);

        var first = await http.SendAsync(Make("a", key));
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        first.Headers.Contains("Idempotent-Replayed").Should().BeFalse();
        var firstBody = await first.Content.ReadAsStringAsync();

        var second = await http.SendAsync(Make("a", key));
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Headers.GetValues("Idempotent-Replayed").Should().ContainSingle().Which.Should().Be("true");
        (await second.Content.ReadAsStringAsync()).Should().Be(firstBody);   // byte-identical, incl. timestamp + correlationId
        IdempotencyProbeController.Executions.Should().Be(before + 1);

        // Wrapped in the standard envelope.
        using var doc = JsonDocument.Parse(firstBody);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data").GetProperty("echo").GetString().Should().Be("a");

        // Same key, different body -> 422 and no execution.
        var mismatch = await http.SendAsync(Make("different", key));
        ((int)mismatch.StatusCode).Should().Be(422);
        IdempotencyProbeController.Executions.Should().Be(before + 1);

        // New key -> executes again; no key -> executes normally.
        (await http.SendAsync(Make("a", Guid.NewGuid().ToString()))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await http.SendAsync(Req(HttpMethod.Post, "/test-support/idempotent", token, new { value = "a" }))).StatusCode.Should().Be(HttpStatusCode.OK);
        IdempotencyProbeController.Executions.Should().Be(before + 3);
    }

    private static HttpRequestMessage WithKey(HttpRequestMessage r, string key)
    {
        r.Headers.Add("Idempotency-Key", key);
        return r;
    }

    // ---------------- courier webhook (fail closed) ----------------

    private const string CourierUrl = "/api/v1/webhooks/courier/SteadFast";

    private static HttpRequestMessage Webhook(string body, string? signature, string? timestamp)
    {
        var r = new HttpRequestMessage(HttpMethod.Post, CourierUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (signature is not null) r.Headers.Add("X-Pawzaroo-Signature", signature);
        if (timestamp is not null) r.Headers.Add("X-Pawzaroo-Timestamp", timestamp);
        return r;
    }

    private static string Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

    [RedisFact]
    public async Task Courier_webhook_without_signature_headers_is_401()
    {
        var res = await _api.CreateClient().SendAsync(Webhook("{}", null, null));
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RedisFact]
    public async Task Courier_webhook_with_bad_signature_is_401()
    {
        var res = await _api.CreateClient().SendAsync(Webhook("{}", new string('a', 64), Now()));
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RedisFact]
    public async Task Courier_webhook_signed_with_wrong_secret_is_401()
    {
        var ts = Now();
        const string body = "{\"trackingNumber\":\"T1\",\"status\":\"delivered\"}";
        var sig = CourierWebhookService.ComputeSignature("some-other-secret", ts, body);
        (await _api.CreateClient().SendAsync(Webhook(body, sig, ts))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RedisFact]
    public async Task Courier_webhook_with_stale_timestamp_is_401_even_if_signature_is_valid()
    {
        var ts = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds().ToString();
        const string body = "{\"trackingNumber\":\"T1\",\"status\":\"delivered\"}";
        var sig = CourierWebhookService.ComputeSignature(ApiFactory.CourierSecret, ts, body);
        (await _api.CreateClient().SendAsync(Webhook(body, sig, ts))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RedisFact]
    public async Task Courier_webhook_valid_signature_is_accepted_past_the_auth_gate()
    {
        var http = _api.CreateClient();
        var ts = Now();

        // Authentic + well-formed but matches no order -> 200 {accepted:false, ignored:true} (not 401).
        var body = $"{{\"trackingNumber\":\"NOPE-{Guid.NewGuid():N}\",\"status\":\"delivered\"}}";
        var sig = CourierWebhookService.ComputeSignature(ApiFactory.CourierSecret, ts, body);
        var ok = await http.SendAsync(Webhook(body, "sha256=" + sig, ts));
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        using (var doc = await ok.JsonBodyAsync())
            doc.RootElement.GetProperty("accepted").GetBoolean().Should().BeFalse();

        // Authentic but invalid payload -> 400 (proves the signature passed, distinct from 401).
        const string bad = "{}";
        var badSig = CourierWebhookService.ComputeSignature(ApiFactory.CourierSecret, ts, bad);
        (await http.SendAsync(Webhook(bad, badSig, ts))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Tampering with the body after signing -> 401.
        (await http.SendAsync(Webhook(body + " ", sig, ts))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
