namespace Pawzaroo.Shared.Exceptions;

public class AppException : Exception
{
    public string Code { get; }
    public AppException(string code, string message) : base(message) => Code = code;
}

public class NotFoundException : AppException
{
    public NotFoundException(string entity, object key) : base("not_found", $"{entity} '{key}' was not found.") { }
}

public class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("validation_failed", "One or more validation errors occurred.") => Errors = errors;
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string? message = null) : base("forbidden", message ?? "Forbidden.") { }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base("conflict", message) { }
}
