using System.Text;
using System.Text.Json;

namespace Pawzaroo.Infrastructure.Modules.Adoption;

/// <summary>
/// Opaque paging cursor. "Newest" ordering uses a keyset (CreatedAt, Id);
/// distance / fee / oldest orderings use a plain offset (Off).
/// </summary>
internal static class AdoptionCursor
{
    private record Payload(long Ts, Guid Id, int? Off = null);

    public static string Encode(DateTime createdAt, Guid id) => Pack(new Payload(createdAt.Ticks, id));

    public static string EncodeOffset(int offset) => Pack(new Payload(0, Guid.Empty, offset));

    public static (DateTime CreatedAt, Guid Id)? Decode(string? cursor)
    {
        var p = Unpack(cursor);
        return p is null || p.Off is not null ? null : (new DateTime(p.Ts, DateTimeKind.Utc), p.Id);
    }

    public static int? DecodeOffset(string? cursor)
    {
        var off = Unpack(cursor)?.Off;
        return off is >= 0 and <= 10_000 ? off : null;
    }

    private static string Pack(Payload p) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(p)));

    private static Payload? Unpack(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try { return JsonSerializer.Deserialize<Payload>(Encoding.UTF8.GetString(Convert.FromBase64String(cursor))); }
        catch { return null; }
    }
}
