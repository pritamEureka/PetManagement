namespace Pawzaroo.Infrastructure.Payments;

public class SslCommerzOptions
{
    public const string SectionName = "SslCommerz";

    public string StoreId { get; set; } = string.Empty;
    public string StorePassword { get; set; } = string.Empty;

    /// <summary>When true, hit sandbox.sslcommerz.com; otherwise securepay.sslcommerz.com.</summary>
    public bool IsSandbox { get; set; } = true;

    /// <summary>ISO currency code SSLCommerz will charge in. Sandbox supports BDT, USD, EUR.</summary>
    public string Currency { get; set; } = "BDT";

    /// <summary>URL the frontend uses to render order confirmation. {ORDER_ID} is replaced with the order Guid.</summary>
    public string FrontendSuccessUrl { get; set; } = string.Empty;

    /// <summary>Frontend URL for user-cancelled checkouts. {ORDER_ID} is replaced.</summary>
    public string FrontendCancelUrl { get; set; } = string.Empty;

    /// <summary>Frontend URL for gateway-reported failures. {ORDER_ID} is replaced.</summary>
    public string FrontendFailUrl { get; set; } = string.Empty;

    /// <summary>
    /// Public base URL of this API. SSLCommerz needs absolute success/fail/cancel/ipn
    /// URLs; they're constructed as $"{BackendBaseUrl}/v1/payments/sslcommerz/...".
    /// </summary>
    public string BackendBaseUrl { get; set; } = string.Empty;

    public string InitiateUrl =>
        IsSandbox
            ? "https://sandbox.sslcommerz.com/gwprocess/v4/api.php"
            : "https://securepay.sslcommerz.com/gwprocess/v4/api.php";

    public string ValidatorUrl =>
        IsSandbox
            ? "https://sandbox.sslcommerz.com/validator/api/validationserverAPI.php"
            : "https://securepay.sslcommerz.com/validator/api/validationserverAPI.php";
}
