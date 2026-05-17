using System.Text;
using System.Text.Json;

namespace Pawzaroo.Infrastructure.Modules.Vet;

internal static class VetCursor
{
    private record Payload(long Ts, Guid Id);

    public static string Encode(DateTime ts, Guid id)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Payload(ts.Ticks, id))));

    public static (DateTime Ts, Guid Id)? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var p = JsonSerializer.Deserialize<Payload>(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)));
            return p is null ? null : (new DateTime(p.Ts, DateTimeKind.Utc), p.Id);
        }
        catch { return null; }
    }
}
