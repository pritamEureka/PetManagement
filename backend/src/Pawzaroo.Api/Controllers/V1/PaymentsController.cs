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

    [HttpPost("success")]
    [Consumes("application/x-www-form-urlencoded")]
    public Task<IActionResult> Success(CancellationToken ct) =>
        HandleCallbackAsync(PaymentCallbackKind.Success, redirect: true, ct);

    [HttpPost("fail")]
    [Consumes("application/x-www-form-urlencoded")]
    public Task<IActionResult> Fail(CancellationToken ct) =>
        HandleCallbackAsync(PaymentCallbackKind.Fail, redirect: true, ct);

    [HttpPost("cancel")]
    [Consumes("application/x-www-form-urlencoded")]
    public Task<IActionResult> Cancel(CancellationToken ct) =>
        HandleCallbackAsync(PaymentCallbackKind.Cancel, redirect: true, ct);

    [HttpPost("ipn")]
    [Consumes("application/x-www-form-urlencoded")]
    public Task<IActionResult> Ipn(CancellationToken ct) =>
        HandleCallbackAsync(PaymentCallbackKind.Ipn, redirect: false, ct);

    private async Task<IActionResult> HandleCallbackAsync(
        PaymentCallbackKind kind, bool redirect, CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        var dict = form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

        var result = await _gateway.VerifyCallbackAsync(dict, kind, ct);
        if (result is null)
        {
            _log.LogWarning("SSLCommerz {Kind} callback could not be validated; form keys={Keys}",
                kind, string.Join(",", dict.Keys));
            return redirect
                ? Redirect(_options.FrontendFailUrl.Replace("{ORDER_ID}", Guid.Empty.ToString()))
                : BadRequest();
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
