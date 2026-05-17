using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Application.Common.Behaviors;
using Pawzaroo.Application.Common.Cqrs;
using Pawzaroo.Application.Modules;
using Pawzaroo.Shared.Abstractions;

namespace Pawzaroo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // CQRS: scans Application assembly for IRequestHandler implementations.
        services.AddCqrs();

        // FluentValidation discovery across the Application assembly.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Pipeline behaviors execute in registration order (outermost first).
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddModules();
        return services;
    }
}
