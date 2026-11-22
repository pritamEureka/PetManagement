using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Application.Modules.SecurityPlus;
using Pawzaroo.Infrastructure.Caching;
using Pawzaroo.Infrastructure.Modules.Security;
using Pawzaroo.Infrastructure.Modules.SecurityPlus;
using Pawzaroo.Infrastructure.Storage;

namespace Pawzaroo.Infrastructure.Features;

public static class SecurityPlusRegistration
{
    public static IServiceCollection AddSecurityPlusFeatures(this IServiceCollection s)
    {
        s.AddScoped<ISessionService, SessionService>();
        s.AddScoped<IRecoveryCodeService, RecoveryCodeService>();
        s.AddScoped<IConsentHistoryService, ConsentHistoryService>();
        s.AddScoped<IStepUpAuthService, StepUpAuthService>();
        s.AddScoped<IPasskeyService, PasskeyService>();
        s.AddScoped<IAccountRecoveryService, AccountRecoveryService>();

        // Redis degraded-mode policy (Security:RedisDegraded:*), detection signals.
        s.AddSingleton<IDegradedModePolicy, DegradedModePolicyService>();
        s.AddScoped<ISecuritySignalService, SecuritySignalService>();

        // IP geolocation for login anomaly detection: no-op unless Security:Geo:Provider=headers (edge-proxy headers).
        s.AddHttpContextAccessor();
        s.AddScoped<IIpGeoLookup>(sp =>
            string.Equals(sp.GetRequiredService<IConfiguration>()["Security:Geo:Provider"], "headers", StringComparison.OrdinalIgnoreCase)
                ? new HeaderIpGeoLookup(sp.GetRequiredService<IHttpContextAccessor>())
                : new NoOpIpGeoLookup());

        // Upload security: built-in (EICAR) scanner, plus ClamAV when Security:ClamAv:Host is set.
        s.AddSingleton<IMalwareScanner>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            IMalwareScanner? clam = string.IsNullOrWhiteSpace(cfg["Security:ClamAv:Host"])
                ? null
                : new ClamAvMalwareScanner(cfg, sp.GetRequiredService<ILogger<ClamAvMalwareScanner>>());
            return new CompositeMalwareScanner(clam);
        });

        // HMAC-signed proxied media links (key: Storage:UrlSigningKey, falls back to Jwt:SigningKey).
        s.AddSingleton<IMediaUrlSigner>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var key = cfg["Storage:UrlSigningKey"];
            if (string.IsNullOrWhiteSpace(key)) key = cfg["Jwt:SigningKey"];
            if (string.IsNullOrWhiteSpace(key)) key = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"); // ephemeral
            return new MediaUrlSigner(System.Text.Encoding.UTF8.GetBytes("media-url|" + key));
        });
        return s;
    }
}
