namespace Pawzaroo.Shared.Results;

public readonly record struct Error(string Code, string Message);

public class Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }
    protected Result(bool ok, Error? err) { IsSuccess = ok; Error = err; }
    public static Result Success() => new(true, null);
    public static Result Failure(Error err) => new(false, err);
    public static Result Failure(string code, string message) => new(false, new Error(code, message));
}

public sealed class Result<T> : Result
{
    public T? Value { get; }
    private Result(T value) : base(true, null) { Value = value; }
    private Result(Error err) : base(false, err) { }
    public static Result<T> Success(T value) => new(value);
    public static new Result<T> Failure(Error err) => new(err);
    public static new Result<T> Failure(string code, string message) => new(new Error(code, message));
}
