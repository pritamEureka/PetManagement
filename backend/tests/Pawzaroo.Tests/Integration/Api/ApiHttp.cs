using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pawzaroo.Tests.Integration.Api;

internal static class ApiHttp
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static async Task<JsonDocument> JsonBodyAsync(this HttpResponseMessage res)
        => JsonDocument.Parse(await res.Content.ReadAsStringAsync());

    public static async Task<string> LoginAsync(HttpClient http, string email, string password)
    {
        var res = await http.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"login failed {(int)res.StatusCode}: {text}");
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    public static HttpRequestMessage Req(HttpMethod m, string url, string? token = null, object? body = null)
    {
        var r = new HttpRequestMessage(m, url);
        if (token is not null) r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) r.Content = JsonContent.Create(body);
        return r;
    }
}
