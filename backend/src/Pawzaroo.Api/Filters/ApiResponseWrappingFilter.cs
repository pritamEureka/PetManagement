using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Pawzaroo.Shared.Api;

namespace Pawzaroo.Api.Filters;

/// <summary>
/// Wraps every successful action result in ApiResponse{T}. Skips actions that
/// already return ApiResponse (idempotent) and IActionResult flow control
/// (File, Redirect, Empty, etc.). Decorate controllers/actions with
/// [SkipApiResponseWrapping] to opt out.
/// </summary>
public class ApiResponseWrappingFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var hasOptOut = context.ActionDescriptor.EndpointMetadata.OfType<SkipApiResponseWrappingAttribute>().Any();
        if (!hasOptOut && context.Result is ObjectResult or && or.Value is not null
            && or.Value.GetType().Name.StartsWith("ApiResponse") == false
            && or.Value is not ProblemDetails)
        {
            var correlationId = context.HttpContext.Items.TryGetValue("CorrelationId", out var c) ? c?.ToString() : null;
            var wrappedType = typeof(ApiResponse<>).MakeGenericType(or.Value.GetType());
            var wrapped = Activator.CreateInstance(wrappedType);
            wrappedType.GetProperty("Success")!.SetValue(wrapped, true);
            wrappedType.GetProperty("Data")!.SetValue(wrapped, or.Value);
            wrappedType.GetProperty("CorrelationId")!.SetValue(wrapped, correlationId);
            or.Value = wrapped;
            or.DeclaredType = wrappedType;
        }
        await next();
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class SkipApiResponseWrappingAttribute : Attribute { }
