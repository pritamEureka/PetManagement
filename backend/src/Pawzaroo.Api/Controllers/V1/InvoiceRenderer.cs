using System.Net;
using System.Text;
using Pawzaroo.Application.Modules.Marketplace.Dtos;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// Server-side HTML invoice. The output is intentionally inline-styled so it
/// renders identically when opened in a new tab and printed via the browser's
/// Print dialog (no external CSS to chase, no font dependencies). All
/// user-supplied strings flow through <see cref="WebUtility.HtmlEncode"/>.
/// </summary>
internal static class InvoiceRenderer
{
    public static string Render(OrderDto o)
    {
        var sb = new StringBuilder();
        sb.Append("""
<!doctype html><html><head><meta charset="utf-8">
<title>Invoice </title>
<style>
  body{font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;color:#1f2937;max-width:760px;margin:32px auto;padding:0 24px;}
  h1{font-size:20px;margin:0 0 4px;}
  .muted{color:#6b7280;font-size:12px;}
  .row{display:flex;justify-content:space-between;gap:24px;margin-top:16px;}
  table{width:100%;border-collapse:collapse;margin-top:18px;font-size:13px;}
  th,td{text-align:left;padding:8px 6px;border-bottom:1px solid #e5e7eb;}
  th{background:#f9fafb;font-weight:600;}
  td.r,th.r{text-align:right;}
  tfoot td{font-weight:600;border-bottom:none;}
  .totals td{padding:4px 6px;}
  .totals tr.total td{font-size:15px;border-top:2px solid #1f2937;padding-top:8px;}
  .badge{display:inline-block;padding:2px 8px;border-radius:9999px;background:#eef2ff;color:#3730a3;font-size:11px;}
  @media print { .noprint { display:none; } body { margin:0; } }
</style>
</head><body>
""");
        sb.Append("<button class=\"noprint\" onclick=\"window.print()\" style=\"float:right;padding:6px 12px;cursor:pointer;\">Print</button>");
        sb.Append($"<h1>Invoice — {Enc(o.OrderNumber)}</h1>");
        sb.Append($"<p class=\"muted\">{o.CreatedAt:yyyy-MM-dd HH:mm} UTC · <span class=\"badge\">{o.Status}</span> · <span class=\"badge\">{o.PaymentStatus}</span></p>");

        sb.Append("<div class=\"row\">");
        sb.Append("<div><strong>Bill to</strong><br>");
        sb.Append($"{Enc(o.CustomerName)}<br><span class=\"muted\">{Enc(o.CustomerEmail)}</span>");
        if (!string.IsNullOrEmpty(o.CustomerPhone)) sb.Append($"<br><span class=\"muted\">{Enc(o.CustomerPhone)}</span>");
        sb.Append("</div>");
        sb.Append("<div><strong>Ship to</strong><br>");
        sb.Append($"{Enc(o.ShippingAddress)}");
        var locationLine = string.Join(", ", new[] { o.ShippingCity, o.ShippingCountry }.Where(s => !string.IsNullOrEmpty(s)));
        if (!string.IsNullOrEmpty(locationLine)) sb.Append($"<br>{Enc(locationLine)}");
        if (!string.IsNullOrEmpty(o.TrackingNumber)) sb.Append($"<br><span class=\"muted\">Tracking: {Enc(o.TrackingNumber!)}</span>");
        sb.Append("</div></div>");

        sb.Append("<table><thead><tr><th>Item</th><th>Store</th><th class=\"r\">Qty</th><th class=\"r\">Unit</th><th class=\"r\">Total</th></tr></thead><tbody>");
        foreach (var it in o.Items)
        {
            sb.Append("<tr>");
            sb.Append($"<td>{Enc(it.ProductName)}</td>");
            sb.Append($"<td>{Enc(it.StoreName)}</td>");
            sb.Append($"<td class=\"r\">{it.Quantity}</td>");
            sb.Append($"<td class=\"r\">{it.UnitPrice:0.00}</td>");
            sb.Append($"<td class=\"r\">{it.Total:0.00}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");

        sb.Append("<table class=\"totals\" style=\"margin-top:8px;width:auto;float:right;\"><tbody>");
        sb.Append($"<tr><td>Subtotal</td><td class=\"r\">{o.Subtotal:0.00}</td></tr>");
        if (o.DiscountAmount > 0)
            sb.Append($"<tr><td>Coupon {Enc(o.CouponCode ?? "")}</td><td class=\"r\">−{o.DiscountAmount:0.00}</td></tr>");
        sb.Append($"<tr><td>Shipping</td><td class=\"r\">{o.ShippingFee:0.00}</td></tr>");
        sb.Append($"<tr><td>Tax</td><td class=\"r\">{o.Tax:0.00}</td></tr>");
        sb.Append($"<tr class=\"total\"><td>Total</td><td class=\"r\">{o.Total:0.00}</td></tr>");
        sb.Append("</tbody></table>");

        sb.Append("<div style=\"clear:both\"></div>");
        sb.Append("<p class=\"muted\" style=\"margin-top:32px\">Thank you for your order.</p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
}
