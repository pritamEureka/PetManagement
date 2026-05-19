namespace Pawzaroo.Application.Common.Interfaces;

/// <summary>
/// Gateway-agnostic payment abstraction. Two operations:
///   1. CreateCheckoutSessionAsync — bootstraps a hosted-payment session and
///      returns the redirect URL the browser should be sent to.
///   2. VerifyCallbackAsync — given the form fields posted back by the gateway
///      (success / fail / cancel / IPN), verifies authenticity out-of-band and
///      returns the outcome so the order can be transitioned.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(
        PaymentCheckoutRequest request,
        CancellationToken ct = default);

    Task<PaymentCallbackResult?> VerifyCallbackAsync(
        IDictionary<string, string> formFields,
        PaymentCallbackKind kind,
        CancellationToken ct = default);
}

public record PaymentLineItem(string Name, decimal UnitAmount, int Quantity);

public record PaymentCheckoutRequest(
    Guid OrderId,
    string OrderNumber,
    string Currency,
    decimal TotalAmount,
    string? CustomerEmail,
    string? CustomerName,
    string? CustomerPhone,
    string? ShippingAddress,
    string? ShippingCity,
    string? ShippingCountry,
    IReadOnlyList<PaymentLineItem> LineItems);

public record PaymentCheckoutResult(string SessionId, string CheckoutUrl);

/// <summary>Which callback URL the gateway is hitting.</summary>
public enum PaymentCallbackKind { Success, Fail, Cancel, Ipn }

public enum PaymentCallbackOutcome { Succeeded, Failed, Cancelled }

/// <summary>
/// Result of validating a gateway callback. <see cref="AmountValidated"/> is
/// the amount the gateway confirms it captured; the caller is expected to
/// compare it against the order total before flipping the order to Paid.
/// </summary>
public record PaymentCallbackResult(
    PaymentCallbackOutcome Outcome,
    Guid OrderId,
    string ProviderRef,
    decimal? AmountValidated,
    string? Currency);
