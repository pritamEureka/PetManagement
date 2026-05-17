using FluentValidation;
using Pawzaroo.Application.Common.Cqrs;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Application.Common.Behaviors;

/// <summary>
/// Runs all registered FluentValidation validators for the request type before
/// the handler executes. Aggregated failures are thrown as a ValidationException
/// which the API exception middleware maps to a 400 response.
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f != null).ToList();

        if (failures.Count == 0) return await next();

        var errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        throw new Pawzaroo.Shared.Exceptions.ValidationException(errors);
    }
}
