using System.Net;
using System.Text.Json;
using Pawzaroo.Shared.Api;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Api.Middleware;

/// <summary>
/// Translates exceptions into the canonical ApiResponse envelope. All other
/// middleware can throw — never write JSON error bodies directly.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (Pawzaroo.Shared.Exceptions.ValidationException ex)
            { await WriteAsync(ctx, HttpStatusCode.BadRequest, new ApiError { Code = ex.Code, Message = ex.Message, Details = ex.Errors }); }
        catch (FluentValidation.ValidationException ex)
        {
            var details = ex.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            await WriteAsync(ctx, HttpStatusCode.BadRequest,
                new ApiError { Code = "validation_failed", Message = "One or more validation errors occurred.", Details = details });
        }
        catch (NotFoundException ex)        { await WriteAsync(ctx, HttpStatusCode.NotFound,     new ApiError { Code = ex.Code, Message = ex.Message }); }
        catch (ForbiddenException ex)       { await WriteAsync(ctx, HttpStatusCode.Forbidden,    new ApiError { Code = ex.Code, Message = ex.Message }); }
        catch (ConflictException ex)        { await WriteAsync(ctx, HttpStatusCode.Conflict,     new ApiError { Code = ex.Code, Message = ex.Message }); }
        catch (AppException ex)             { await WriteAsync(ctx, HttpStatusCode.BadRequest,   new ApiError { Code = ex.Code, Message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { await WriteAsync(ctx, HttpStatusCode.Unauthorized, new ApiError { Code = "unauthorized", Message = ex.Message }); }
        catch (KeyNotFoundException ex)     { await WriteAsync(ctx, HttpStatusCode.NotFound,     new ApiError { Code = "not_found", Message = ex.Message }); }
        catch (ArgumentException ex)        { await WriteAsync(ctx, HttpStatusCode.BadRequest,   new ApiError { Code = "bad_request", Message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteAsync(ctx, HttpStatusCode.InternalServerError,
                new ApiError { Code = "internal_error", Message = "An unexpected error occurred." });
        }
    }

    private static Task WriteAsync(HttpContext ctx, HttpStatusCode status, ApiError error)
    {
        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/json";
        var correlationId = ctx.Items.TryGetValue("CorrelationId", out var c) ? c?.ToString() : null;
        var body = ApiResponse.Fail(error, correlationId);
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(body, Json));
    }
}
