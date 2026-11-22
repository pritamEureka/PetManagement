using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Reliability;

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
public class SslCommerzPaymentGateway : IPaymentGateway, IPaymentStatusQuery
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

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.StoreId)
        && !string.IsNullOrWhiteSpace(_options.StorePassword)
        && !string.IsNullOrWhiteSpace(_options.BackendBaseUrl);

    public async Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(
        PaymentCheckoutRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_options.StoreId) || string.IsNullOrEmpty(_options.StorePassword))
            throw new InvalidOperationException("SslCommerz:StoreId / StorePassword are not configured.");
        if (string.IsNullOrEmpty(_options.BackendBaseUrl))
            throw new InvalidOperationException("SslCommerz:BackendBaseUrl is not configured.");

        var backend = _options.BackendBaseUrl.TrimEnd('/');
        var orderId = request.OrderId.ToString();

        // Per SSLCommerz EasyCheckout v4 docs, when shipping_method is sent
        // (not "NO"), ALL ship_* fields incl. ship_state and ship_postcode are
        // required, AND cus_postcode/cus_state are required for the customer.
        // We default the optional ones to placeholders the gateway accepts —
        // a one-off shipping address with no postcode still has to checkout.
        var shipState   = NonEmpty(request.ShippingState,      "Dhaka");
        var shipPost    = NonEmpty(request.ShippingPostalCode, "1000");
        var shipCity    = NonEmpty(request.ShippingCity,       "Dhaka");
        var shipCountry = NonEmpty(request.ShippingCountry,    "Bangladesh");

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

            // Customer block — name/email/add1/city/postcode/country are required.
            ["cus_name"]      = NonEmpty(request.CustomerName,    "Customer"),
            ["cus_email"]     = NonEmpty(request.CustomerEmail,   "noreply@pawzaroo.local"),
            ["cus_add1"]      = NonEmpty(request.ShippingAddress, "N/A"),
            ["cus_city"]      = shipCity,
            ["cus_state"]     = shipState,
            ["cus_postcode"]  = shipPost,
            ["cus_country"]   = shipCountry,
            ["cus_phone"]     = NonEmpty(request.CustomerPhone,   "01700000000"),

            // Shipping block — required because shipping_method is set.
            ["shipping_method"] = "Courier",
            ["num_of_item"]     = request.LineItems.Sum(li => li.Quantity).ToString(),
            ["product_name"]    = TruncateForGateway(string.Join(", ", request.LineItems.Select(li => li.Name)), 250),
            ["product_category"]= "general",
            ["product_profile"] = "general",

            ["ship_name"]     = NonEmpty(request.CustomerName,    "Customer"),
            ["ship_add1"]     = NonEmpty(request.ShippingAddress, "N/A"),
            ["ship_city"]     = shipCity,
            ["ship_state"]    = shipState,
            ["ship_postcode"] = shipPost,
            ["ship_country"]  = shipCountry,

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

    // ---- IPaymentStatusQuery (gateway reconciliation) ----
    public bool SupportsQuery => IsConfigured;

    /// <summary>
    /// Looks a transaction up by OUR tran_id (the order Guid) via SSLCommerz's merchantTransIDvalidationAPI.
    /// Returns null on transport/parse failure so reconciliation reports "unknown" instead of a false mismatch.
    /// </summary>
    public async Task<GatewayTransactionStatus?> QueryByTransactionIdAsync(string transactionId, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        var url = _options.ValidatorUrl.Replace("validationserverAPI.php", "merchantTransIDvalidationAPI.php")
                  + $"?tran_id={Uri.EscapeDataString(transactionId)}"
                  + $"&store_id={Uri.EscapeDataString(_options.StoreId)}"
                  + $"&store_passwd={Uri.EscapeDataString(_options.StorePassword)}&format=json";
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("element", out var elements) || elements.ValueKind != JsonValueKind.Array
                || elements.GetArrayLength() == 0)
                return new GatewayTransactionStatus(transactionId, false, false, null, null, null, null);

            var pick = elements[0];
            foreach (var el in elements.EnumerateArray())
            {
                var st = el.TryGetProperty("status", out var s0) ? s0.GetString() : null;
                if (string.Equals(st, "VALID", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(st, "VALIDATED", StringComparison.OrdinalIgnoreCase)) { pick = el; break; }
            }
            var status = pick.TryGetProperty("status", out var s1) ? s1.GetString() : null;
            var paid = string.Equals(status, "VALID", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(status, "VALIDATED", StringComparison.OrdinalIgnoreCase);
            // currency_type/currency_amount are in the currency we requested; amount is the settled one.
            decimal? amount = null;
            foreach (var name in new[] { "currency_amount", "amount" })
                if (amount is null && pick.TryGetProperty(name, out var a) &&
                    decimal.TryParse(a.GetString(), System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out var d)) amount = d;
            var currency = pick.TryGetProperty("currency_type", out var c) ? c.GetString() : null;
            var bank = pick.TryGetProperty("bank_tran_id", out var bt) ? bt.GetString() : null;
            return new GatewayTransactionStatus(transactionId, true, paid, status, amount, currency, bank);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _log.LogWarning(ex, "SSLCommerz transaction query failed for {TranId}", transactionId);
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

    private static string NonEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value!;
}
