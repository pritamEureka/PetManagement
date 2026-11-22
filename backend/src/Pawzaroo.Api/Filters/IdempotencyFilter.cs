using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Reliability;
using Pawzaroo.Shared.Api;

namespace Pawzaroo.Api.Filters;

/// <summary>
/// Opt-in HTTP idempotency for unsafe actions. Honors the <c>Idempotency-Key</c> request header (optional: without
/// it the action runs normally).
///   - same key + same body, first request finished   -> the ORIGINAL stored response is replayed
///     (status + content type + the already-wrapped ApiResponse body, header <c>Idempotent-Replayed: true</c>)
///   - same key while the first request is in flight  -> 409 Conflict (Retry-After: 1)
///   - same key with a different body/query           -> 422 Unprocessable Entity
/// Scope: (user, key, method, path), 24h TTL. Only 2xx and deterministic 4xx are stored; 5xx, exceptions,
/// 401/403 (auth/step-up can change), 408/409/425/429, redirects and non-text bodies release the key so the
/// client can retry with the same key. Exceptions mapped to 4xx by ExceptionMiddleware are also released
/// (the filter never sees that response) - those are re-evaluated, which is safe because they did not commit.
///
/// Implemented as a RESOURCE filter so it wraps the result filters (ApiResponseWrappingFilter): the captured body is
/// exactly what the client received the first time.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class IdempotentAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new IdempotencyFilter(
            (IIdempotencyStore)serviceProvider.GetService(typeof(IIdempotencyStore))!,
            (ICurrentUserService)serviceProvider.GetService(typeof(ICurrentUserService))!);
}

public sealed class IdempotencyFilter : IAsyncResourceFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayHeaderName = "Idempotent-Replayed";
    private const int MaxKeyLength = 128;
    private const int MaxStoredBodyBytes = 1_000_000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IIdempotencyStore _store;
    private readonly ICurrentUserService _user;

    public IdempotencyFilter(IIdempotencyStore store, ICurrentUserService user)
    {
        _store = store;
        _user = user;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var req = http.Request;

        if (!req.Headers.TryGetValue(HeaderName, out var headerValues) || string.IsNullOrWhiteSpace(headerValues.ToString()))
        {
            await next();
            return;
        }

        var key = headerValues.ToString().Trim();
        if (key.Length > MaxKeyLength || key.Any(char.IsControl))
        {
            context.Result = Error(400, "idempotency_key_invalid", $"{HeaderName} must be 1-{MaxKeyLength} printable characters.");
            return;
        }

        req.EnableBuffering();
        string hash;
        using (var sha = SHA256.Create())
        {
            using var ms = new MemoryStream();
            await req.Body.CopyToAsync(ms, http.RequestAborted);
            req.Body.Position = 0;
            var query = Encoding.UTF8.GetBytes(req.QueryString.Value ?? string.Empty);
            ms.Write(query, 0, query.Length);
            hash = Convert.ToHexString(sha.ComputeHash(ms.ToArray())).ToLowerInvariant();
        }

        var path = req.Path.Value ?? string.Empty;
        if (path.Length > 512) path = path[..512];
        var begin = await _store.BeginAsync(_user.UserId ?? Guid.Empty, key, req.Method, path, hash, http.RequestAborted);

        switch (begin.Outcome)
        {
            case IdempotencyBeginOutcome.Mismatch:
                context.Result = Error(422, "idempotency_key_reused",
                    "This Idempotency-Key was already used with a different request body.");
                return;
            case IdempotencyBeginOutcome.InFlight:
                http.Response.Headers["Retry-After"] = "1";
                context.Result = Error(409, "idempotency_in_flight",
                    "A request with this Idempotency-Key is still being processed.");
                return;
            case IdempotencyBeginOutcome.Replay:
                http.Response.Headers[ReplayHeaderName] = "true";
                context.Result = new ContentResult
                {
                    StatusCode = begin.StatusCode ?? 200,
                    Content = begin.Body,
                    ContentType = begin.ContentType
                };
                return;
        }

        // Proceed: run the pipeline against a buffer so the exact response can be stored and replayed.
        var original = http.Response.Body;
        var buffer = new MemoryStream();
        http.Response.Body = buffer;
        ResourceExecutedContext? executed = null;
        var stored = false;
        try
        {
            executed = await next();

            var status = http.Response.StatusCode;
            var contentType = http.Response.ContentType;
            var failed = executed.Exception is not null && !executed.ExceptionHandled;
            if (!failed && IsStorable(status, contentType) && buffer.Length <= MaxStoredBodyBytes)
            {
                var body = buffer.Length == 0 ? null : Encoding.UTF8.GetString(buffer.ToArray());
                await _store.CompleteAsync(begin.RecordId, status, body, contentType, CancellationToken.None);
                stored = true;
            }
        }
        finally
        {
            // Anything not stored (5xx, exception, non-storable status) releases the key for a same-key retry.
            if (!stored)
            {
                try { await _store.AbandonAsync(begin.RecordId, CancellationToken.None); } catch { /* best effort */ }
            }
            http.Response.Body = original;
            if (buffer.Length > 0)
            {
                buffer.Position = 0;
                await buffer.CopyToAsync(original, CancellationToken.None);
            }
            await buffer.DisposeAsync();
        }
    }

    private static bool IsStorable(int status, string? contentType)
    {
        if (status is >= 200 and < 300) { /* ok */ }
        else if (status is >= 400 and < 500)
        {
            if (status is 401 or 403 or 408 or 409 or 425 or 429) return false;
        }
        else return false;

        // Only text/JSON bodies are persisted (files/streams are not replayable from a text column).
        return string.IsNullOrEmpty(contentType)
               || contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
               || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
    }

    private static ContentResult Error(int status, string code, string message) => new()
    {
        StatusCode = status,
        ContentType = "application/json",
        Content = JsonSerializer.Serialize(
            ApiResponse<object?>.Fail(new ApiError { Code = code, Message = message }), Json)
    };
}
