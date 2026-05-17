using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Cqrs;

namespace Pawzaroo.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[CQRS] {Request} -> handling", name);
        try
        {
            var response = await next();
            sw.Stop();
            _logger.LogInformation("[CQRS] {Request} <- ok in {ElapsedMs}ms", name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "[CQRS] {Request} <- failed in {ElapsedMs}ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
