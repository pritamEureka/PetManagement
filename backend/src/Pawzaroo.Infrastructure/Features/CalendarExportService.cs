using System.Text;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.VetPlus;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Features;

public class CalendarExportService : ICalendarExportService
{
    private const int MaxUpcoming = 200;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public CalendarExportService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    private sealed record Row(Guid Id, DateTime ScheduledAt, int Duration, ConsultationType Type, AppointmentStatus Status,
        string DoctorName, string PatientName, string? PetName, string? ClinicName, string? ClinicAddress,
        string? Symptoms, DateTime? UpdatedAt, DateTime CreatedAt);

    public async Task<string> AppointmentIcsAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var row = await Project(_db.Appointments.AsNoTracking()
                .Where(a => a.Id == appointmentId && (a.PatientUserId == uid || a.Doctor.UserId == uid)))
            .SingleOrDefaultAsync(ct);
        if (row is null)
        {
            // Distinguish "missing" from "not yours" without leaking existence.
            throw new NotFoundException("Appointment", appointmentId);
        }
        return IcsWriter.Calendar(new[] { ToEvent(row, uid) });
    }

    public async Task<string> UpcomingIcsAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var now = DateTime.UtcNow;
        var rows = await Project(_db.Appointments.AsNoTracking()
                .Where(a => (a.PatientUserId == uid || a.Doctor.UserId == uid)
                            && a.ScheduledAt >= now
                            && a.Status != AppointmentStatus.CancelledByUser
                            && a.Status != AppointmentStatus.CancelledByDoctor
                            && a.Status != AppointmentStatus.Draft
                            && a.Status != AppointmentStatus.Refunded
                            && a.Status != AppointmentStatus.NoShow)
                .OrderBy(a => a.ScheduledAt).Take(MaxUpcoming))
            .ToListAsync(ct);
        return IcsWriter.Calendar(rows.Select(r => ToEvent(r, uid)));
    }

    private IQueryable<Row> Project(IQueryable<Appointment> q) => q.Select(a => new Row(
        a.Id, a.ScheduledAt, a.DurationMinutes, a.Type, a.Status,
        a.Doctor.User.DisplayName, a.PatientUser.DisplayName, a.Pet != null ? a.Pet.Name : null,
        a.Doctor.ClinicName, a.Doctor.ClinicAddress, a.Symptoms, a.UpdatedAt, a.CreatedAt));

    private static IcsEvent ToEvent(Row r, Guid uid)
    {
        var online = r.Type == ConsultationType.Online;
        var summary = online ? $"Online vet consultation with {r.DoctorName}" : $"Vet visit with {r.DoctorName}";
        if (r.PetName is not null) summary += $" ({r.PetName})";

        var desc = new StringBuilder();
        desc.Append(online ? "Online consultation. Open the appointment in Pawzaroo to join the room." : "In-clinic visit.");
        if (!string.IsNullOrWhiteSpace(r.Symptoms)) desc.Append("\nSymptoms: ").Append(r.Symptoms);

        string? location = online ? "Online (Pawzaroo)"
            : string.Join(", ", new[] { r.ClinicName, r.ClinicAddress }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrEmpty(location)) location = null;

        var status = r.Status switch
        {
            AppointmentStatus.Confirmed or AppointmentStatus.Completed => "CONFIRMED",
            AppointmentStatus.CancelledByUser or AppointmentStatus.CancelledByDoctor => "CANCELLED",
            _ => "TENTATIVE"
        };
        return new IcsEvent($"appt-{r.Id:D}@pawzaroo.app", r.ScheduledAt, r.ScheduledAt.AddMinutes(r.Duration),
            summary, desc.ToString(), location, status, r.UpdatedAt ?? r.CreatedAt);
    }
}

public record IcsEvent(string Uid, DateTime StartUtc, DateTime EndUtc, string Summary, string? Description,
    string? Location, string Status, DateTime LastModifiedUtc);

/// <summary>Minimal RFC 5545 writer: CRLF line endings, TEXT escaping, 75-octet line folding, UTC timestamps.</summary>
public static class IcsWriter
{
    public static string Calendar(IEnumerable<IcsEvent> events)
    {
        var sb = new StringBuilder();
        Line(sb, "BEGIN:VCALENDAR");
        Line(sb, "VERSION:2.0");
        Line(sb, "PRODID:-//Pawzaroo//Appointments//EN");
        Line(sb, "CALSCALE:GREGORIAN");
        Line(sb, "METHOD:PUBLISH");
        var stamp = Utc(DateTime.UtcNow);
        foreach (var e in events)
        {
            Line(sb, "BEGIN:VEVENT");
            Line(sb, "UID:" + Escape(e.Uid));
            Line(sb, "DTSTAMP:" + stamp);
            Line(sb, "DTSTART:" + Utc(e.StartUtc));
            Line(sb, "DTEND:" + Utc(e.EndUtc));
            Line(sb, "LAST-MODIFIED:" + Utc(e.LastModifiedUtc));
            Line(sb, "SUMMARY:" + Escape(e.Summary));
            if (!string.IsNullOrWhiteSpace(e.Description)) Line(sb, "DESCRIPTION:" + Escape(e.Description!));
            if (!string.IsNullOrWhiteSpace(e.Location)) Line(sb, "LOCATION:" + Escape(e.Location!));
            Line(sb, "STATUS:" + e.Status);
            Line(sb, "BEGIN:VALARM");
            Line(sb, "ACTION:DISPLAY");
            Line(sb, "DESCRIPTION:" + Escape(e.Summary));
            Line(sb, "TRIGGER:-PT1H");
            Line(sb, "END:VALARM");
            Line(sb, "END:VEVENT");
        }
        Line(sb, "END:VCALENDAR");
        return sb.ToString();
    }

    public static string Utc(DateTime d)
    {
        var u = d.Kind == DateTimeKind.Local ? d.ToUniversalTime() : d;
        return u.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>TEXT escaping per RFC 5545 3.3.11: backslash, semicolon, comma, newline.</summary>
    public static string Escape(string s) => s
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n")
        .Replace("\r", "\\n");

    /// <summary>Appends a content line folded at 75 octets (never splitting a UTF-8 sequence) and terminated by CRLF.</summary>
    private static void Line(StringBuilder sb, string line)
    {
        var octets = 0;
        var limit = 75;
        foreach (var rune in line.EnumerateRunes())
        {
            var len = rune.Utf8SequenceLength;
            if (octets + len > limit)
            {
                sb.Append("\r\n ");
                octets = 1; // the leading space counts toward the next line
            }
            sb.Append(rune.ToString());
            octets += len;
        }
        sb.Append("\r\n");
    }
}
