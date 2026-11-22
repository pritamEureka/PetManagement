using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Filters;
using Pawzaroo.Infrastructure.Features;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// Courier callbacks. Anonymous at the transport level; every request must carry a valid
/// X-Pawzaroo-Signature (hex HMAC-SHA256 of "{X-Pawzaroo-Timestamp}.{rawBody}" with Courier:WebhookSecret).
/// Fails closed (401) when no secret is configured.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/webhooks/courier")]
[AllowAnonymous]
[SkipApiResponseWrapping]
public class CourierWebhooksController : ControllerBase
{
    public const string SignatureHeader = "X-Pawzaroo-Signature";
    public const string TimestampHeader = "X-Pawzaroo-Timestamp";

    private readonly ICourierWebhookService _webhooks;
    public CourierWebhooksController(ICourierWebhookService webhooks) => _webhooks = webhooks;

    [HttpPost("{provider}")]
    [RequestSizeLimit(262144)]
    public async Task<IActionResult> Receive(string provider, CancellationToken ct)
    {
        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
            body = await reader.ReadToEndAsync(ct);

        var result = await _webhooks.HandleAsync(provider, body,
            Request.Headers[SignatureHeader].FirstOrDefault(),
            Request.Headers[TimestampHeader].FirstOrDefault(), ct);

        // Telemetry (provider tag only for known providers to keep cardinality bounded).
        var providerTag = result == WebhookResult.UnknownProvider ? "unknown" : provider;
        Pawzaroo.Infrastructure.Observability.PawzarooTelemetry.WebhookReceived.Add(1, new KeyValuePair<string, object?>("provider", providerTag));
        if (result is not (WebhookResult.Accepted or WebhookResult.Duplicate))
            Pawzaroo.Infrastructure.Observability.PawzarooTelemetry.WebhookRejected.Add(1,
                new KeyValuePair<string, object?>("provider", providerTag),
                new KeyValuePair<string, object?>("reason", result.ToString()));

        return result switch
        {
            WebhookResult.Accepted => Ok(new { accepted = true }),
            WebhookResult.Duplicate => Ok(new { accepted = false, ignored = true }),
            WebhookResult.BadRequest => BadRequest(new { error = "invalid_payload" }),
            WebhookResult.UnknownProvider => NotFound(),
            _ => Unauthorized()
        };
    }
}
