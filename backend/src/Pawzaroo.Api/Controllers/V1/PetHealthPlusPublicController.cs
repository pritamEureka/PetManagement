using System.Net;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Api.Filters;
using Pawzaroo.Application.Modules.Pets.HealthPlus;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Anonymous token lookups: emergency health card and vaccination certificate verification. Rate limited per IP.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public")]
[AllowAnonymous]
public class PetHealthPlusPublicController : ControllerBase
{
    private readonly IExposureService _exposure;
    private readonly IVaccinationCertificateService _certs;

    public PetHealthPlusPublicController(IExposureService exposure, IVaccinationCertificateService certs)
    { _exposure = exposure; _certs = certs; }

    [HttpGet("emergency-card/{token}")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<EmergencyCardDto>> Card(string token, CancellationToken ct)
    {
        var c = await _exposure.GetPublicEmergencyCardAsync(token, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpGet("emergency-card/{token}/view")]
    [EnableRateLimiting("auth")]
    [Produces("text/html")]
    [SkipApiResponseWrapping]
    public async Task<IActionResult> CardHtml(string token, CancellationToken ct)
    {
        var c = await _exposure.GetPublicEmergencyCardAsync(token, ct);
        if (c is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return Content(RenderCard(c), "text/html");
    }

    [HttpGet("vaccination-certificates/{token}")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<CertificateVerifyDto>> Verify(string token, CancellationToken ct)
    {
        var r = await _certs.VerifyAsync(token, ct);
        return r.Valid ? Ok(r) : NotFound();
    }

    [HttpGet("vaccination-certificates/{token}/certificate")]
    [EnableRateLimiting("auth")]
    [Produces("text/html")]
    [SkipApiResponseWrapping]
    public async Task<IActionResult> CertificateHtml(string token, CancellationToken ct)
    {
        var r = await _certs.VerifyAsync(token, ct);
        if (!r.Valid) return NotFound();
        Response.Headers.CacheControl = "no-store";
        var verifyUrl = $"{Request.Scheme}://{Request.Host}/api/v1/public/vaccination-certificates/{WebUtility.UrlEncode(token)}";
        return Content(RenderCertificate(r, verifyUrl), "text/html");
    }

    private static string E(string? s) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(s) ? "—" : s);

    private const string Style = "<style>body{font-family:system-ui,sans-serif;max-width:720px;margin:24px auto;padding:0 16px;color:#1f2937}" +
        "h1{font-size:22px;margin:0}h2{font-size:15px;border-bottom:2px solid #1f2937;padding-bottom:3px;margin-top:22px}" +
        ".red{background:#b91c1c;color:#fff;padding:10px 14px;border-radius:6px;font-weight:600}.muted{color:#6b7280;font-size:12px}" +
        "table{width:100%;border-collapse:collapse;font-size:13px}td,th{text-align:left;border-bottom:1px solid #e5e7eb;padding:5px}</style>";

    private static string RenderCard(EmergencyCardDto c)
    {
        var sb = new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta name=\"robots\" content=\"noindex\">");
        sb.Append(Style).Append($"<title>Emergency card — {E(c.PetName)}</title></head><body>");
        sb.Append($"<div class=\"red\">EMERGENCY HEALTH CARD — {E(c.PetName)}</div>");
        sb.Append($"<p>{E(c.Species)} · {E(c.Breed)} · {(c.WeightKg is { } w ? $"{w:0.##} kg" : "weight unknown")} · Born {E(c.BirthDate)} · Microchip {E(c.MicrochipNumber)}</p>");
        void List(string title, IReadOnlyList<string> items)
        {
            sb.Append($"<h2>{title}</h2>");
            if (items.Count == 0) { sb.Append("<p class=\"muted\">None recorded.</p>"); return; }
            sb.Append("<ul>"); foreach (var i in items) sb.Append($"<li>{E(i)}</li>"); sb.Append("</ul>");
        }
        List("Allergies", c.Allergies); List("Current medication", c.ActiveMedications); List("Recent conditions", c.Conditions);
        sb.Append($"<h2>Veterinarian</h2><p>{E(c.VetName)} · {E(c.VetClinic)} · {E(c.VetPhone)}</p>");
        sb.Append($"<h2>Owner</h2><p>{E(c.OwnerName)} · {(string.IsNullOrWhiteSpace(c.OwnerPhone) ? "—" : $"<a href=\"tel:{WebUtility.HtmlEncode(c.OwnerPhone)}\">{E(c.OwnerPhone)}</a>")}</p>");
        sb.Append($"<p class=\"muted\">Generated {c.GeneratedAt:yyyy-MM-dd HH:mm} UTC{(c.ExpiresAt is { } x ? $" · link expires {x:yyyy-MM-dd HH:mm} UTC" : "")}. Information is owner-maintained and not a diagnosis.</p></body></html>");
        return sb.ToString();
    }

    private static string RenderCertificate(CertificateVerifyDto r, string verifyUrl)
    {
        var sb = new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta name=\"robots\" content=\"noindex\">");
        sb.Append(Style).Append("<style>@media print{.noprint{display:none}}</style>");
        sb.Append($"<title>Vaccination certificate — {E(r.PetName)}</title></head><body>");
        sb.Append("<button class=\"noprint\" style=\"float:right\" onclick=\"window.print()\">Print / Save PDF</button>");
        sb.Append($"<h1>Vaccination certificate</h1><p>{E(r.PetName)} · {E(r.Species)} · {E(r.Breed)} · Microchip {E(r.MicrochipNumber)}<br>Owner: {E(r.OwnerName)}</p>");
        if (r.ContentChangedSinceIssue) sb.Append("<p class=\"red\">Vaccination records have changed since this certificate was issued.</p>");
        sb.Append("<h2>Vaccinations</h2><table><tr><th>Vaccine</th><th>Given</th><th>Next due</th><th>By</th><th>Vet-sealed</th></tr>");
        foreach (var v in r.Vaccinations)
            sb.Append($"<tr><td>{E(v.VaccineName)}</td><td>{v.AdministeredOn:yyyy-MM-dd}</td><td>{(v.NextDueOn is { } n ? n.ToString("yyyy-MM-dd") : "—")}</td><td>{E(v.AdministeredByVet)}</td><td>{(v.Sealed ? "Yes" : "No")}</td></tr>");
        sb.Append($"</table><h2>Verify</h2><p class=\"muted\">Issued {r.IssuedAt:yyyy-MM-dd HH:mm} UTC. Verification link (encode as QR):<br><code>{WebUtility.HtmlEncode(verifyUrl)}</code></p>");
        sb.Append($"<p class=\"muted\">{WebUtility.HtmlEncode(r.Notice)} This page is print-to-PDF HTML, not a PDF/A archival file.</p></body></html>");
        return sb.ToString();
    }
}
