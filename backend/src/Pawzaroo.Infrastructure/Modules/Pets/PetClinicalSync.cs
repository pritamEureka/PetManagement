using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Modules.Pets;

/// <summary>
/// Mirrors vet-module activity into the pet's own health history:
///   * a completed appointment becomes a MedicalRecord (one per appointment),
///   * an uploaded prescription attaches its file to that record and turns each
///     prescribed item into a PetMedication course with dose times.
/// Callers own SaveChanges so the mirror commits atomically with the source change.
/// </summary>
internal static class PetClinicalSync
{
    public static async Task<MedicalRecord?> UpsertVisitRecordAsync(ApplicationDbContext db, Appointment appt, CancellationToken ct)
    {
        if (appt.PetId is not { } petId) return null;

        var rec = await db.MedicalRecords.SingleOrDefaultAsync(m => m.AppointmentId == appt.Id, ct);
        var vetName = await db.Doctors.Where(d => d.Id == appt.DoctorId).Select(d => d.User.DisplayName).SingleOrDefaultAsync(ct);
        if (rec is null)
        {
            rec = new MedicalRecord { PetId = petId, AppointmentId = appt.Id };
            db.MedicalRecords.Add(rec);
        }
        rec.VisitDate = appt.ScheduledAt;
        rec.VetName = vetName;
        rec.Diagnosis ??= string.IsNullOrWhiteSpace(appt.Symptoms) ? $"{appt.Type} consultation" : $"Presented with: {appt.Symptoms}";
        rec.Notes = appt.FollowUpNotes ?? rec.Notes;
        rec.PrescriptionFileUrl = appt.PrescriptionFileUrl ?? rec.PrescriptionFileUrl;
        return rec;
    }

    public static async Task ImportPrescriptionAsync(ApplicationDbContext db, Appointment appt, Prescription rx, CancellationToken ct)
    {
        if (appt.PetId is not { } petId) return;

        var rec = await UpsertVisitRecordAsync(db, appt, ct);
        if (rec is not null)
        {
            rec.PrescriptionFileUrl = rx.FileUrl ?? rec.PrescriptionFileUrl;
            if (!string.IsNullOrWhiteSpace(rx.Notes)) rec.Treatment = rx.Notes;
        }

        foreach (var item in ParseItems(rx.ItemsJson))
        {
            var start = DateTime.UtcNow.Date;
            db.PetMedications.Add(new PetMedication
            {
                PetId = petId,
                PrescriptionId = rx.Id,
                DrugName = item.Drug,
                Dose = item.Dose,
                DoseTimes = DoseTimesFor(item.Frequency),
                StartDate = DateTime.SpecifyKind(start, DateTimeKind.Utc),
                EndDate = DurationDays(item.Duration) is { } days ? DateTime.SpecifyKind(start.AddDays(days - 1), DateTimeKind.Utc) : rx.ValidUntil,
                Instructions = item.Instructions,
                IsActive = true
            });
        }
    }

    internal record RxItem(string Drug, string? Dose, string? Frequency, string? Duration, string? Instructions);

    internal static IReadOnlyList<RxItem> ParseItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<RxItem>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<RxItem>();
            var list = new List<RxItem>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                string? Get(string name) =>
                    el.EnumerateObject().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value is { ValueKind: JsonValueKind.String } v
                        ? v.GetString() : null;
                var drug = Get("drug") ?? Get("name");
                if (string.IsNullOrWhiteSpace(drug)) continue;
                list.Add(new RxItem(drug.Trim()[..Math.Min(drug.Trim().Length, 200)], Get("dose"), Get("frequency"), Get("duration"), Get("instructions")));
            }
            return list;
        }
        catch (JsonException)
        {
            return Array.Empty<RxItem>();
        }
    }

    /// <summary>Maps common frequency notations (OD/BID/TID/QID, "2x", "twice daily", "1-0-1") to clock times.</summary>
    internal static string DoseTimesFor(string? frequency)
    {
        var f = (frequency ?? "").Trim().ToLowerInvariant();
        var dashed = Regex.Match(f, @"^([01])\s*-\s*([01])\s*-\s*([01])$");
        if (dashed.Success)
        {
            var times = new List<string>();
            if (dashed.Groups[1].Value == "1") times.Add("08:00");
            if (dashed.Groups[2].Value == "1") times.Add("14:00");
            if (dashed.Groups[3].Value == "1") times.Add("20:00");
            if (times.Count > 0) return string.Join(",", times);
        }
        var perDay =
            f.Contains("qid") || f.Contains("four") || f.StartsWith("4") ? 4 :
            f.Contains("tid") || f.Contains("three") || f.Contains("thrice") || f.StartsWith("3") ? 3 :
            f.Contains("bid") || f.Contains("twice") || f.Contains("two") || f.StartsWith("2") || f.Contains("12 h") || f.Contains("12h") ? 2 :
            1;
        return perDay switch
        {
            4 => "08:00,12:00,16:00,20:00",
            3 => "08:00,14:00,20:00",
            2 => "09:00,21:00",
            _ => "09:00"
        };
    }

    internal static int? DurationDays(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration)) return null;
        var m = Regex.Match(duration.ToLowerInvariant(), @"(\d+)\s*(day|d|week|wk|w|month|mo|m)?");
        if (!m.Success || !int.TryParse(m.Groups[1].Value, out var n) || n <= 0) return null;
        var unit = m.Groups[2].Value;
        var days = unit.StartsWith("w") ? n * 7 : unit.StartsWith("m") ? n * 30 : n;
        return Math.Min(days, 365);
    }
}
