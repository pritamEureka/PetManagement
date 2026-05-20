using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Infrastructure.Payments;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// SSLCommerz hosted-checkout callbacks. The four endpoints (success / fail /
/// cancel / ipn) all receive form-POSTs from the gateway (or the user's
/// browser, in the success/fail/cancel cases). Browser-initiated calls get a
/// 302 redirect to a frontend confirmation/cancel page; the IPN endpoint just
/// returns 200.
///
/// Anonymous: the user is mid-redirect from the gateway and has no session
/// cookie yet on this domain (or for IPN, the gateway has no credentials).
/// We authenticate via the validator API round-trip in IPaymentGateway.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments/sslcommerz")]
[AllowAnonymous]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentGateway _gateway;
    private readonly IOrderService _orders;
    private readonly SslCommerzOptions _options;
    private readonly ILogger<PaymentsController> _log;

    public PaymentsController(
        IPaymentGateway gateway,
        IOrderService orders,
        IOptions<SslCommerzOptions> options,
        ILogger<PaymentsController> log)
    {
        _gateway = gateway;
        _orders = orders;
        _options = options.Value;
        _log = log;
    }

    // SSLCommerz returns the user's browser to these URLs via an auto-submitting
    // form (POST with x-www-form-urlencoded body). We also accept GET so that
    //   (a) the URL is directly testable in a browser, and
    //   (b) refreshes / back-navigation don't 404. On GET there's no form data
    //       to validate, so we just bounce the browser to the matching frontend
    //       page — the page itself re-fetches the order and shows its true
    //       status (the IPN endpoint will have already flipped Paid).

    [HttpGet("success"), HttpPost("success")]
    public Task<IActionResult> Success(CancellationToken ct) =>
        HandleCallbackAsync(PaymentCallbackKind.Success, redirect: true, ct);

    [HttpGet("fail"), HttpPost("fail")]
    public Task<IActionResult> Fail(CancellationToken ct) =>
        HandleCallbackAsync(PaymentCallbackKind.Fail, redirect: true, ct);

    [HttpGet("cancel"), HttpPost("cancel")]
    public Task<IActionResult> Cancel(CancellationToken ct) =>
        HandleCallbackAsync(PaymentCallbackKind.Cancel, redirect: true, ct);

    // IPN is server-to-server only — keep it POST-only and don't redirect.
    [HttpPost("ipn")]
    public Task<IActionResult> Ipn(CancellationToken ct) =>
        HandleCallbackAsync(PaymentCallbackKind.Ipn, redirect: false, ct);

    private async Task<IActionResult> HandleCallbackAsync(
        PaymentCallbackKind kind, bool redirect, CancellationToken ct)
    {
        // Read the form only when the request actually has one — Request.Form
        // throws on GET / empty bodies. Falling back to the query string lets a
        // refresh of the URL still recover an orderId for the redirect.
        IDictionary<string, string> dict;
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            dict = form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        }
        else
        {
            dict = Request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        }

        var result = await _gateway.VerifyCallbackAsync(dict, kind, ct);

        // No form/data we could validate. For browser hits (GET), drop the user
        // on the frontend page that matches the URL they reached — the page
        // will fetch the order and show its current state. For IPN we 400.
        if (result is null)
        {
            _log.LogWarning("SSLCommerz {Kind} callback could not be validated; keys={Keys}",
                kind, string.Join(",", dict.Keys));
            if (!redirect) return BadRequest();
            var fallbackTemplate = kind switch
            {
                PaymentCallbackKind.Success => _options.FrontendSuccessUrl,
                PaymentCallbackKind.Cancel  => _options.FrontendCancelUrl,
                _                           => _options.FrontendFailUrl,
            };
            var orderHint = dict.TryGetValue("orderId", out var oid) ? oid
                          : dict.TryGetValue("tran_id", out var tid) ? tid
                          : Guid.Empty.ToString();
            return Redirect(fallbackTemplate.Replace("{ORDER_ID}", orderHint));
        }

        switch (result.Outcome)
        {
            case PaymentCallbackOutcome.Succeeded:
                await _orders.MarkPaymentSucceededAsync(result.OrderId, result.ProviderRef, result.AmountValidated, ct);
                break;
            case PaymentCallbackOutcome.Failed:
                await _orders.MarkPaymentFailedAsync(result.OrderId, result.ProviderRef, cancelled: false, ct);
                break;
            case PaymentCallbackOutcome.Cancelled:
                await _orders.MarkPaymentFailedAsync(result.OrderId, result.ProviderRef, cancelled: true, ct);
                break;
        }

        if (!redirect) return Ok();

        var template = result.Outcome switch
        {
            PaymentCallbackOutcome.Succeeded => _options.FrontendSuccessUrl,
            PaymentCallbackOutcome.Cancelled => _options.FrontendCancelUrl,
            _ => _options.FrontendFailUrl
        };
        return Redirect(template.Replace("{ORDER_ID}", result.OrderId.ToString()));
    }
}
