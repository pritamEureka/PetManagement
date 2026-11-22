using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pawzaroo.Application.Modules.Reliability;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Features.Reliability;

public class PaymentAuditService : IPaymentAuditService
{
    // Card data and credentials never reach the audit table.
    private static readonly Regex SensitiveKey = new(
        @"card|cvv|cvc|pan\b|passw|secret|signature|verify_key|store_id",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ApplicationDbContext _db;
    public PaymentAuditService(ApplicationDbContext db) => _db = db;

    public async Task RecordAsync(PaymentAuditRecord r, CancellationToken ct = default)
    {
        var entry = Build(r);
        _db.Set<PaymentAuditEntry>().Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public static PaymentAuditEntry Build(PaymentAuditRecord r)
    {
        string? hash = null, json = null;
        if (r.RawPayload is { Count: > 0 })
        {
            hash = HashPayload(r.RawPayload);
            json = JsonSerializer.Serialize(Redact(r.RawPayload));
        }
        return new PaymentAuditEntry
        {
            EventType = r.EventType,
            Provider = r.Provider,
            OrderId = r.OrderId,
            PaymentId = r.PaymentId,
            Amount = r.Amount,
            Currency = r.Currency,
            RawPayloadHash = hash,
            RawPayloadJson = json,
            IpAddress = r.IpAddress is { Length: > 64 } ip ? ip[..64] : r.IpAddress,
            Note = r.Note is { Length: > 500 } n ? n[..500] : r.Note,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>SHA-256 (hex) of the ORIGINAL payload, key-sorted so field order does not matter.</summary>
    public static string HashPayload(IDictionary<string, string> payload)
    {
        var sb = new StringBuilder();
        foreach (var kv in payload.OrderBy(k => k.Key, StringComparer.Ordinal))
            sb.Append(kv.Key).Append('=').Append(kv.Value).Append('&');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    public static Dictionary<string, string> Redact(IDictionary<string, string> payload) =>
        payload.Where(kv => !SensitiveKey.IsMatch(kv.Key))
               .ToDictionary(kv => kv.Key, kv => kv.Value.Length > 500 ? kv.Value[..500] : kv.Value);
}
