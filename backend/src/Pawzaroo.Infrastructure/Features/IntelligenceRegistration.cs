using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Application.Modules.Assistant;
using Pawzaroo.Application.Modules.Intelligence;
using Pawzaroo.Infrastructure.Modules.Assistant;
using Pawzaroo.Infrastructure.Modules.Intelligence;

namespace Pawzaroo.Infrastructure.Features;

public static class IntelligenceRegistration
{
    /// <summary>Operations intelligence (roadmap 8.8) and care assistant (8.12).</summary>
    public static IServiceCollection AddIntelligenceFeatures(this IServiceCollection s)
    {
        s.AddScoped<IOperationsIntelligenceService, OperationsIntelligenceService>();
        // Swap this line to plug in an LLM-backed engine (see IAssistantEngine remarks).
        s.AddScoped<IAssistantEngine, RuleBasedAssistantEngine>();
        s.AddScoped<IAssistantService, AssistantService>();
        s.AddScoped<IAssistantTriageService, AssistantTriageService>();
        s.AddScoped<IScheduledReportService, ScheduledReportService>();
        return s;
    }
}
