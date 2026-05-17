using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Pawzaroo.Application.Common.Cqrs;

/// <summary>
/// Resolves IRequestHandler{TRequest,TResponse} via DI, then composes
/// registered IPipelineBehavior{TRequest,TResponse} in registration order
/// around the handler. Equivalent to MediatR's Send pipeline without the
/// dependency.
/// </summary>
public class Mediator : IMediator
{
    private readonly IServiceProvider _sp;
    private static readonly ConcurrentDictionary<Type, Type> HandlerInterfaceCache = new();

    public Mediator(IServiceProvider sp) => _sp = sp;

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        var requestType = request.GetType();
        var handlerInterface = HandlerInterfaceCache.GetOrAdd(requestType,
            t => typeof(IRequestHandler<,>).MakeGenericType(t, typeof(TResponse)));
        var handler = _sp.GetRequiredService(handlerInterface);

        var behaviorInterface = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = ((IEnumerable<object>)_sp.GetServices(behaviorInterface)).Reverse().ToArray();

        Func<Task<TResponse>> next = () =>
        {
            var method = handlerInterface.GetMethod("HandleAsync")!;
            return (Task<TResponse>)method.Invoke(handler, new object[] { request, ct })!;
        };

        foreach (var behavior in behaviors)
        {
            var currentNext = next;
            var beh = behavior;
            var method = behaviorInterface.GetMethod("HandleAsync")!;
            next = () => (Task<TResponse>)method.Invoke(beh, new object[] { request, currentNext, ct })!;
        }

        return next();
    }
}

/// <summary>Scans the Application assembly and registers every concrete IRequestHandler.</summary>
public static class CqrsServiceCollectionExtensions
{
    public static IServiceCollection AddCqrs(this IServiceCollection services)
    {
        services.AddScoped<IMediator, Mediator>();

        var asm = typeof(IMediator).Assembly;
        var handlerOpenGeneric = typeof(IRequestHandler<,>);
        var concrete = asm.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface);

        foreach (var type in concrete)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == handlerOpenGeneric)
                    services.AddScoped(iface, type);
            }
        }
        return services;
    }
}
