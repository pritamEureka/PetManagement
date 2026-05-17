namespace Pawzaroo.Api.Middleware;

/// <summary>
/// Adds the standard "harden your site" response headers. The web tier (nginx)
/// also sets HSTS at the edge; we still set defenses here so a bypassed proxy
/// can't downgrade us.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var h = ctx.Response.Headers;

        // Only set once — composing middlewares may have already added them.
        Set(h, "X-Content-Type-Options",    "nosniff");
        Set(h, "X-Frame-Options",           "DENY");
        Set(h, "Referrer-Policy",           "strict-origin-when-cross-origin");
        Set(h, "Permissions-Policy",        "camera=(), microphone=(), geolocation=(self), interest-cohort=()");
        Set(h, "Cross-Origin-Opener-Policy","same-origin");
        Set(h, "Cross-Origin-Resource-Policy", "same-site");

        // Only HSTS on HTTPS — sending it on plain HTTP is meaningless and
        // accidentally fingerprintable.
        if (ctx.Request.IsHttps)
            Set(h, "Strict-Transport-Security", "max-age=63072000; includeSubDomains");

        // The API never serves HTML, so a tight CSP is safe and cheap.
        Set(h, "Content-Security-Policy",
            "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'");

        await _next(ctx);
    }

    private static void Set(IHeaderDictionary headers, string name, string value)
    {
        if (!headers.ContainsKey(name)) headers[name] = value;
    }
}
