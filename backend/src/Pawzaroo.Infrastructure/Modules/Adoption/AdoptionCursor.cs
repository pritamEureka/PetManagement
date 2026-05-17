using System.Text;
using System.Text.Json;

namespace Pawzaroo.Infrastructure.Modules.Adoption;

internal static class AdoptionCursor
{
    private record Payload(long Ts, Guid Id);

    public static string Encode(DateTime createdAt, Guid id)
    {
        var json = JsonSerializer.Serialize(new Payload(createdAt.Ticks, id));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static (DateTime CreatedAt, Guid Id)? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var p = JsonSerializer.Deserialize<Payload>(
                Encoding.UTF8.GetString(Convert.FromBase64String(cursor)));
            return p is null ? null : (new DateTime(p.Ts, DateTimeKind.Utc), p.Id);
        }
        catch { return null; }
    }
}
