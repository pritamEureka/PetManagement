namespace Pawzaroo.Application.Common.Cqrs;

/// <summary>Marker for a request returning a value.</summary>
public interface IRequest<TResponse> { }

/// <summary>Marker for a request returning Unit.</summary>
public interface IRequest : IRequest<Unit> { }

/// <summary>Empty value type for commands without a return.</summary>
public readonly struct Unit
{
    public static readonly Unit Value = default;
}
