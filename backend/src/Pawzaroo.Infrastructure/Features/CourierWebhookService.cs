using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Pawzaroo.Application.Modules.CommercePlus;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Infrastructure.Features;

public enum WebhookResult { Accepted, Duplicate, Unauthorized, BadRequest, UnknownProvider }

public interface ICourierWebhookService
{
    /// <summary>
    /// Verifies and ingests a courier callback. Signature = hex(HMAC-SHA256(secret, "{timestamp}.{rawBody}")),
    /// sent in X-Pawzaroo-Signature with the unix-seconds timestamp in X-Pawzaroo-Timestamp.
    /// </summary>
    Task<WebhookResult> HandleAsync(string provider, string rawBody, string? signatureHeader,
        string? timestampHeader, CancellationToken ct = default);
}

public class CourierWebhookService : ICourierWebhookService
{
    private readonly IConfiguration _config;
    private readonly IDeliveryTrackingService _tracking;

    public CourierWebhookService(IConfiguration config, IDeliveryTrackingService tracking)
    {
        _config = config;
        _tracking = tracking;
    }

    public async Task<WebhookResult> HandleAsync(string provider, string rawBody, string? signatureHeader,
        string? timestampHeader, CancellationToken ct = default)
    {
        // Fail closed: no configured secret => nothing is ever accepted.
        var secret = _config[$"Courier:Providers:{provider}:WebhookSecret"];
        if (string.IsNullOrEmpty(secret)) secret = _config["Courier:WebhookSecret"];
        if (string.IsNullOrEmpty(secret)) return WebhookResult.Unauthorized;

        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(timestampHeader))
            return WebhookResult.Unauthorized;
        if (!long.TryParse(timestampHeader, NumberStyles.None, CultureInfo.InvariantCulture, out var ts))
            return WebhookResult.Unauthorized;

        var tolerance = int.TryParse(_config["Courier:WebhookToleranceSeconds"], out var t) && t > 0 ? t : 300;
        var skew = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts);
        if (skew > tolerance) return WebhookResult.Unauthorized;

        if (!VerifySignature(secret, timestampHeader, rawBody, signatureHeader))
            return WebhookResult.Unauthorized;

        // Authentic from here on.
        if (!Enum.TryParse<CourierProvider>(provider, ignoreCase: true, out var courier)
            || !Enum.IsDefined(courier))
            return WebhookResult.UnknownProvider;

        var evt = Parse(courier.ToString(), rawBody);
        if (evt is null) return WebhookResult.BadRequest;

        return await _tracking.IngestExternalEventAsync(evt, ct)
            ? WebhookResult.Accepted
            : WebhookResult.Duplicate;
    }

    public static string ComputeSignature(string secret, string timestamp, string rawBody)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{rawBody}"))).ToLowerInvariant();
    }

    public static bool VerifySignature(string secret, string timestamp, string rawBody, string header)
    {
        var provided = header.Trim();
        if (provided.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) provided = provided[7..];
        provided = provided.ToLowerInvariant();
        var expected = ComputeSignature(secret, timestamp, rawBody);
        // Constant-time; unequal lengths simply fail (length is not secret).
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(provided));
    }

    internal static ExternalTrackingEvent? Parse(string provider, string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var tracking = Str(root, "trackingNumber", "tracking_number", "tracking_code", "consignment_id");
            var rawStatus = Str(root, "status", "event", "delivery_status");
            if (string.IsNullOrWhiteSpace(tracking) || string.IsNullOrWhiteSpace(rawStatus)) return null;
            var status = NormalizeStatus(rawStatus);
            if (status is null) return null;

            var occurred = DateTime.UtcNow;
            var rawTime = Str(root, "occurredAt", "occurred_at", "timestamp", "updated_at");
            if (rawTime is not null
                && DateTime.TryParse(rawTime, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
                occurred = parsed;

            var lat = Num(root, "latitude", "lat");
            var lng = Num(root, "longitude", "lng", "lon");
            (lat, lng) = SanitizeCoordinates(lat, lng);

            return new ExternalTrackingEvent(
                provider,
                Str(root, "eventId", "event_id", "id"),
                tracking.Trim(),
                status.Value,
                Str(root, "description", "message", "note"),
                Str(root, "location", "locationText", "location_text"),
                lat,
                lng,
                occurred);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Out-of-range / non-finite coordinates are dropped (both, since half a coordinate is meaningless)
    /// rather than rejecting the whole event. Valid: lat -90..90, lng -180..180.
    /// </summary>
    public static (double? Lat, double? Lng) SanitizeCoordinates(double? lat, double? lng)
    {
        var latOk = lat is null || (double.IsFinite(lat.Value) && lat.Value is >= -90 and <= 90);
        var lngOk = lng is null || (double.IsFinite(lng.Value) && lng.Value is >= -180 and <= 180);
        return latOk && lngOk ? (lat, lng) : (null, null);
    }

    /// <summary>Maps free-form courier status strings to the normalized enum.</summary>
    public static TrackingStatus? NormalizeStatus(string raw)
    {
        var s = new string(raw.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return s switch
        {
            "labelcreated" or "created" or "pending" or "manifested" or "booked" or "orderplaced" => TrackingStatus.LabelCreated,
            "pickedup" or "pickup" or "pickupcompleted" or "collected" or "intake" => TrackingStatus.PickedUp,
            "intransit" or "shipped" or "dispatched" or "atsortinghub" or "inhub" or "departed" => TrackingStatus.InTransit,
            "outfordelivery" or "ofd" or "outfordeliverytocustomer" => TrackingStatus.OutForDelivery,
            "delivered" or "deliverycompleted" or "completed" => TrackingStatus.Delivered,
            "deliveryfailed" or "failed" or "undelivered" or "attemptfailed" or "cancelled" => TrackingStatus.DeliveryFailed,
            "returned" or "returntosender" or "rts" or "returning" => TrackingStatus.Returned,
            "exception" or "delayed" or "onhold" or "hold" => TrackingStatus.Exception,
            _ => null
        };
    }

    private static string? Str(JsonElement e, params string[] names)
    {
        foreach (var n in names)
            if (e.TryGetProperty(n, out var v))
            {
                if (v.ValueKind == JsonValueKind.String) return v.GetString();
                if (v.ValueKind == JsonValueKind.Number) return v.GetRawText();
            }
        return null;
    }

    private static double? Num(JsonElement e, params string[] names)
    {
        foreach (var n in names)
            if (e.TryGetProperty(n, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return d;
                if (v.ValueKind == JsonValueKind.String
                    && double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ds)) return ds;
            }
        return null;
    }
}
