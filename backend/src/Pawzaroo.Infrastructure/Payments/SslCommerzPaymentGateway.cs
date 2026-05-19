using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;

namespace Pawzaroo.Infrastructure.Payments;

/// <summary>
/// SSLCommerz hosted-checkout gateway (sandbox + production).
///
/// Flow:
///   1. CreateCheckoutSessionAsync POSTs an x-www-form-urlencoded request to
///      api.php; on status="SUCCESS" we return GatewayPageURL for the browser.
///   2. After the user pays, SSLCommerz POSTs to one of four absolute URLs we
///      registered (success / fail / cancel / ipn). The controller forwards
///      the form fields here for verification.
///   3. VerifyCallbackAsync re-checks each success/IPN against the validator API
///      using val_id — never trust the redirect POST blindly; an attacker could
///      forge it. Only Outcome=Succeeded after validator returns status=VALID
///      or VALIDATED.
/// </summary>
public class SslCommerzPaymentGateway : IPaymentGateway
{
    private readonly SslCommerzOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<SslCommerzPaymentGateway> _log;

    public SslCommerzPaymentGateway(
        IOptions<SslCommerzOptions> options,
        HttpClient http,
        ILogger<SslCommerzPaymentGateway> log)
    {
        _options = options.Value;
        _http = http;
        _log = log;
    }

    public async Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(
        PaymentCheckoutRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_options.StoreId) || string.IsNullOrEmpty(_options.StorePassword))
            throw new InvalidOperationException("SslCommerz:StoreId / StorePassword are not configured.");
        if (string.IsNullOrEmpty(_options.BackendBaseUrl))
            throw new InvalidOperationException("SslCommerz:BackendBaseUrl is not configured.");

        var backend = _options.BackendBaseUrl.TrimEnd('/');
        var orderId = request.OrderId.ToString();

        var form = new Dictionary<string, string>
        {
            ["store_id"]     = _options.StoreId,
            ["store_passwd"] = _options.StorePassword,
            ["total_amount"] = request.TotalAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            ["currency"]     = string.IsNullOrEmpty(request.Currency) ? _options.Currency : request.Currency,
            ["tran_id"]      = orderId,

            ["success_url"] = $"{backend}/api/v1/payments/sslcommerz/success",
            ["fail_url"]    = $"{backend}/api/v1/payments/sslcommerz/fail",
            ["cancel_url"]  = $"{backend}/api/v1/payments/sslcommerz/cancel",
            ["ipn_url"]     = $"{backend}/api/v1/payments/sslcommerz/ipn",

            ["cus_name"]    = request.CustomerName    ?? "Customer",
            ["cus_email"]   = request.CustomerEmail   ?? "noreply@pawzaroo.local",
            ["cus_phone"]   = request.CustomerPhone   ?? "0000000000",
            ["cus_add1"]    = request.ShippingAddress ?? "N/A",
            ["cus_city"]    = request.ShippingCity    ?? "N/A",
            ["cus_country"] = request.ShippingCountry ?? "Bangladesh",

            // Shipping mirrors customer for digital-light catalogs; non-zero
            // shipping is collected in OrderService and rolled into total_amount.
            ["shipping_method"] = "Courier",
            ["num_of_item"]     = request.LineItems.Sum(li => li.Quantity).ToString(),
            ["product_name"]    = TruncateForGateway(string.Join(", ", request.LineItems.Select(li => li.Name)), 250),
            ["product_category"]= "general",
            ["product_profile"] = "general",

            ["ship_name"]    = request.CustomerName ?? "Customer",
            ["ship_add1"]    = request.ShippingAddress ?? "N/A",
            ["ship_city"]    = request.ShippingCity    ?? "N/A",
            ["ship_country"] = request.ShippingCountry ?? "Bangladesh",

            ["value_a"] = orderId,
            ["value_b"] = request.OrderNumber
        };

        using var content = new FormUrlEncodedContent(form);
        using var resp = await _http.PostAsync(_options.InitiateUrl, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            _log.LogError("SSLCommerz initiate HTTP {Status}: {Body}", (int)resp.StatusCode, body);
            throw new InvalidOperationException("SSLCommerz initiate request failed.");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
        if (!string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            var failedReason = root.TryGetProperty("failedreason", out var fr) ? fr.GetString() : "(no reason)";
            _log.LogError("SSLCommerz initiate non-success: status={Status} reason={Reason}", status, failedReason);
            throw new InvalidOperationException($"SSLCommerz refused the session: {failedReason}");
        }

        var sessionKey = root.TryGetProperty("sessionkey", out var sk) ? sk.GetString() : null;
        var gatewayUrl = root.TryGetProperty("GatewayPageURL", out var gp) ? gp.GetString() : null;

        if (string.IsNullOrEmpty(gatewayUrl) || string.IsNullOrEmpty(sessionKey))
            throw new InvalidOperationException("SSLCommerz response missing GatewayPageURL/sessionkey.");

        return new PaymentCheckoutResult(sessionKey!, gatewayUrl!);
    }

    public async Task<PaymentCallbackResult?> VerifyCallbackAsync(
        IDictionary<string, string> formFields, PaymentCallbackKind kind, CancellationToken ct = default)
    {
        // tran_id is what we set during initiate — the order Guid. value_a is a
        // belt-and-braces copy of the same. Fall back if one is missing.
        if (!TryGetOrderId(formFields, out var orderId))
        {
            _log.LogWarning("SSLCommerz callback missing tran_id / value_a; kind={Kind}", kind);
            return null;
        }

        var tranRef = formFields.TryGetValue("tran_id", out var tr) ? tr : orderId.ToString();

        switch (kind)
        {
            case PaymentCallbackKind.Cancel:
                return new PaymentCallbackResult(
                    PaymentCallbackOutcome.Cancelled, orderId, tranRef, null, null);

            case PaymentCallbackKind.Fail:
                return new PaymentCallbackResult(
                    PaymentCallbackOutcome.Failed, orderId, tranRef, null, null);

            case PaymentCallbackKind.Success:
            case PaymentCallbackKind.Ipn:
            {
                // Re-validate by calling the validator API with val_id. Never trust the
                // redirect POST: SSLCommerz documents that this round-trip is required.
                if (!formFields.TryGetValue("val_id", out var valId) || string.IsNullOrEmpty(valId))
                {
                    _log.LogWarning("SSLCommerz success/ipn missing val_id; orderId={OrderId}", orderId);
                    return new PaymentCallbackResult(PaymentCallbackOutcome.Failed, orderId,
                        tranRef, null, null);
                }

                var qs = $"?val_id={Uri.EscapeDataString(valId)}" +
                         $"&store_id={Uri.EscapeDataString(_options.StoreId)}" +
                         $"&store_passwd={Uri.EscapeDataString(_options.StorePassword)}" +
                         "&v=1&format=json";

                using var resp = await _http.GetAsync(_options.ValidatorUrl + qs, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _log.LogError("SSLCommerz validator HTTP {Status}: {Body}", (int)resp.StatusCode, body);
                    return new PaymentCallbackResult(PaymentCallbackOutcome.Failed, orderId, valId, null, null);
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
                var amount = root.TryGetProperty("amount", out var am) &&
                             decimal.TryParse(am.GetString(), System.Globalization.NumberStyles.Number,
                                 System.Globalization.CultureInfo.InvariantCulture, out var d)
                    ? d : (decimal?)null;
                var currency = root.TryGetProperty("currency", out var cu) ? cu.GetString() : null;
                var bankTranId = root.TryGetProperty("bank_tran_id", out var bt) ? bt.GetString() : valId;

                var ok = string.Equals(status, "VALID", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(status, "VALIDATED", StringComparison.OrdinalIgnoreCase);

                return new PaymentCallbackResult(
                    ok ? PaymentCallbackOutcome.Succeeded : PaymentCallbackOutcome.Failed,
                    orderId, bankTranId ?? valId, amount, currency);
            }
            default:
                return null;
        }
    }

    private static bool TryGetOrderId(IDictionary<string, string> form, out Guid orderId)
    {
        if (form.TryGetValue("tran_id", out var t) && Guid.TryParse(t, out orderId)) return true;
        if (form.TryGetValue("value_a", out var v) && Guid.TryParse(v, out orderId)) return true;
        orderId = Guid.Empty;
        return false;
    }

    private static string TruncateForGateway(string s, int max) =>
        string.IsNullOrEmpty(s) ? "Order" : (s.Length <= max ? s : s.Substring(0, max));
}
