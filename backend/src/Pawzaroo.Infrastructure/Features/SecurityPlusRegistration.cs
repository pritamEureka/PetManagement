using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Application.Modules.SecurityPlus;
using Pawzaroo.Infrastructure.Modules.SecurityPlus;

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
        return s;
    }
}
