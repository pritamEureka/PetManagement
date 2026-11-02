using System.Net;
using System.Text;
using Pawzaroo.Application.Modules.Pets.Dtos;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// Printable pet health record (same approach as <see cref="InvoiceRenderer"/>):
/// inline-styled HTML the browser can Print → Save as PDF. Every user-supplied
/// string goes through <see cref="WebUtility.HtmlEncode"/>.
/// </summary>
internal static class PetHealthReportRenderer
{
    public static string Render(HealthReportDto r)
    {
        var p = r.Pet;
        var sb = new StringBuilder();
        sb.Append("""
<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="robots" content="noindex">
<style>
  body{font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;color:#1f2937;max-width:820px;margin:32px auto;padding:0 20px;}
  h1{font-size:22px;margin:0 0 4px;} h2{font-size:15px;margin:26px 0 8px;border-bottom:2px solid #1f2937;padding-bottom:4px;}
  .muted{color:#6b7280;font-size:12px;} .grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:8px 18px;margin-top:12px;font-size:13px;}
  .grid b{display:block;color:#6b7280;font-weight:500;font-size:11px;text-transform:uppercase;letter-spacing:.03em;}
  table{width:100%;border-collapse:collapse;font-size:12.5px;} th,td{text-align:left;padding:6px;border-bottom:1px solid #e5e7eb;vertical-align:top;}
  th{background:#f9fafb;font-weight:600;} .warn{background:#fef3c7;padding:8px 10px;border-radius:6px;font-size:13px;margin-top:12px;}
  .empty{color:#9ca3af;font-size:12px;font-style:italic;}
  @media print { .noprint{display:none;} body{margin:0;} }
</style>
""");
        sb.Append($"<title>Health record — {Enc(p.Name)}</title></head><body>");
        sb.Append("<button class=\"noprint\" onclick=\"window.print()\" style=\"float:right;padding:6px 12px;cursor:pointer;\">Print / Save PDF</button>");
        sb.Append($"<h1>{Enc(p.Name)} — health record</h1>");
        sb.Append($"<p class=\"muted\">Generated {r.GeneratedAt:yyyy-MM-dd HH:mm} UTC");
        if (r.ExpiresAt is { } exp) sb.Append($" · shared link valid until {exp:yyyy-MM-dd HH:mm} UTC");
        sb.Append(" · Pawzaroo</p>");

        sb.Append("<div class=\"grid\">");
        Field(sb, "Species", p.AnimalType.ToString());
        Field(sb, "Breed", p.Breed);
        Field(sb, "Sex", p.Gender.ToString());
        Field(sb, "Born", p.BirthDate?.ToString("yyyy-MM-dd"));
        Field(sb, "Weight", p.WeightKg is { } w ? $"{w:0.##} kg" : null);
        Field(sb, "Colour", p.Color);
        Field(sb, "Microchip", p.MicrochipNumber);
        Field(sb, "Tag", p.TagNumber);
        Field(sb, "Owner", p.OwnerName);
        sb.Append("</div>");
        if (!string.IsNullOrWhiteSpace(p.Allergies))
            sb.Append($"<div class=\"warn\"><strong>Allergies:</strong> {Enc(p.Allergies)}</div>");

        sb.Append("<h2>Vaccinations</h2>");
        Table(sb, new[] { "Vaccine", "Given", "Next due", "By", "Notes" },
            p.Vaccinations.Select(v => new[] { v.VaccineName, D(v.AdministeredOn), D(v.NextDueOn), v.AdministeredByVet, v.Notes }));

        sb.Append("<h2>Vet visits &amp; medical history</h2>");
        Table(sb, new[] { "Date", "Diagnosis", "Treatment", "Vet", "Notes" },
            p.MedicalRecords.Select(m => new[] { D(m.VisitDate), m.Diagnosis, m.Treatment, m.VetName, m.Notes }));

        sb.Append("<h2>Medications</h2>");
        Table(sb, new[] { "Drug", "Dose", "Times", "From", "Until", "Status" },
            r.Medications.Select(m => new[] { m.DrugName, m.Dose, m.DoseTimes, D(m.StartDate), D(m.EndDate), m.IsActive ? "Active" : "Stopped" }));

        sb.Append("<h2>Weight history</h2>");
        Table(sb, new[] { "Date", "Weight (kg)", "Notes" },
            r.Weights.OrderByDescending(w => w.RecordedOn).Take(24).Select(w => new[] { D(w.RecordedOn), w.WeightKg.ToString("0.##"), w.Notes }));

        sb.Append("<h2>Grooming</h2>");
        Table(sb, new[] { "Date", "Service", "Provider", "Notes" },
            p.GroomingRecords.Select(g => new[] { D(g.PerformedOn), g.ServiceType, g.Provider, g.Notes }));

        if (!string.IsNullOrWhiteSpace(p.DietNotes))
            sb.Append($"<h2>Diet</h2><p style=\"font-size:13px\">{Enc(p.DietNotes)}</p>");

        sb.Append("<p class=\"muted\" style=\"margin-top:28px\">This record is maintained by the pet's owner and treating vets on Pawzaroo. Verify critical information with the issuing clinic.</p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void Field(StringBuilder sb, string label, string? value) =>
        sb.Append($"<div><b>{label}</b>{(string.IsNullOrWhiteSpace(value) ? "—" : Enc(value))}</div>");

    private static void Table(StringBuilder sb, string[] headers, IEnumerable<string?[]> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0) { sb.Append("<p class=\"empty\">None recorded.</p>"); return; }
        sb.Append("<table><thead><tr>");
        foreach (var h in headers) sb.Append($"<th>{h}</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var row in list)
        {
            sb.Append("<tr>");
            foreach (var cell in row) sb.Append($"<td>{(string.IsNullOrWhiteSpace(cell) ? "—" : Enc(cell))}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");
    }

    private static string? D(DateTime? d) => d?.ToString("yyyy-MM-dd");
    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? "");
}
