namespace Pawzaroo.Shared.Api;

/// <summary>Uniform envelope for every API response.</summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }
    public string? CorrelationId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string? correlationId = null)
        => new() { Success = true, Data = data, CorrelationId = correlationId };

    public static ApiResponse<T> Fail(ApiError error, string? correlationId = null)
        => new() { Success = false, Error = error, CorrelationId = correlationId };
}

public class ApiResponse : ApiResponse<object?>
{
    public static ApiResponse Ok(string? correlationId = null) => new() { Success = true, CorrelationId = correlationId };
    public static new ApiResponse Fail(ApiError error, string? correlationId = null)
        => new() { Success = false, Error = error, CorrelationId = correlationId };
}

public class ApiError
{
    public string Code { get; init; } = "error";
    public string Message { get; init; } = "An error occurred.";
    public IReadOnlyDictionary<string, string[]>? Details { get; init; }
}
