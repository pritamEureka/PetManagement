namespace Pawzaroo.Application.Common.Cqrs;

public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken ct);
}

public interface IRequestHandler<TRequest> : IRequestHandler<TRequest, Unit> where TRequest : IRequest<Unit> { }

/// <summary>Implemented by pipeline behaviors (validation, logging, transactions, etc.).</summary>
public interface IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct);
}
