using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.ClinicPlus;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Features;

// =====================================================================================
// Pre-visit questionnaires
// =====================================================================================
public class QuestionnaireService : IQuestionnaireService
{
    private static readonly string[] Types = { "text", "yesno", "choice", "number" };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    public QuestionnaireService(ApplicationDbContext db, ICurrentUserService current) { _db = db; _current = current; }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    private static QuestionnaireTemplateDto ToDto(QuestionnaireTemplate t) =>
        new(t.Id, t.Title, JsonSerializer.Deserialize<List<QuestionDto>>(t.QuestionsJson, Json) ?? new(), t.IsActive);

    private async Task<Guid> MyDoctorIdAsync(CancellationToken ct)
    {
        var uid = Uid();
        return await _db.Doctors.Where(d => d.UserId == uid).Select(d => (Guid?)d.Id).SingleOrDefaultAsync(ct)
               ?? throw new ForbiddenException("Only registered veterinarians can manage questionnaires.");
    }

    public async Task<QuestionnaireTemplateDto?> GetMyTemplateAsync(CancellationToken ct = default)
    {
        var did = await MyDoctorIdAsync(ct);
        var t = await _db.Set<QuestionnaireTemplate>().AsNoTracking().Where(x => x.DoctorId == did)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        return t is null ? null : ToDto(t);
    }

    public async Task<QuestionnaireTemplateDto> UpsertMyTemplateAsync(UpsertQuestionnaireInput input, CancellationToken ct = default)
    {
        var did = await MyDoctorIdAsync(ct);
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 200) errors["title"] = new[] { "Title is required (max 200 characters)." };
        if (input.Questions is null || input.Questions.Count is 0 or > 30) errors["questions"] = new[] { "Add between 1 and 30 questions." };
        else
        {
            var ids = new HashSet<string>();
            foreach (var q in input.Questions)
            {
                if (string.IsNullOrWhiteSpace(q.Id) || q.Id.Length > 40 || !ids.Add(q.Id)) { errors["questions"] = new[] { "Each question needs a unique id (max 40 characters)." }; break; }
                if (!Types.Contains(q.Type)) { errors["questions"] = new[] { $"Unknown question type '{q.Type}'." }; break; }
                if (string.IsNullOrWhiteSpace(q.Label) || q.Label.Length > 300) { errors["questions"] = new[] { "Each question needs a label (max 300 characters)." }; break; }
                if (q.Type == "choice" && (q.Options is null || q.Options.Count is < 2 or > 20 || q.Options.Any(string.IsNullOrWhiteSpace)))
                { errors["questions"] = new[] { "Choice questions need 2-20 options." }; break; }
            }
        }
        if (errors.Count > 0) throw new Shared.Exceptions.ValidationException(errors);

        var set = _db.Set<QuestionnaireTemplate>();
        var t = await set.Where(x => x.DoctorId == did).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (t is null) { t = new QuestionnaireTemplate { DoctorId = did }; set.Add(t); }
        t.Title = input.Title.Trim();
        t.QuestionsJson = JsonSerializer.Serialize(input.Questions.Select(q =>
            new QuestionDto(q.Id.Trim(), q.Type, q.Label.Trim(), q.Required, q.Type == "choice" ? q.Options!.Select(o => o.Trim()).ToList() : null)), Json);
        t.IsActive = input.IsActive;
        await _db.SaveChangesAsync(ct);
        return ToDto(t);
    }

    private async Task<(Appointment A, bool IsDoctor)> LoadAsync(Guid appointmentId, CancellationToken ct)
    {
        var uid = Uid();
        var a = await _db.Appointments.AsNoTracking().Include(x => x.Doctor).SingleOrDefaultAsync(x => x.Id == appointmentId, ct)
                ?? throw new NotFoundException("Appointment", appointmentId);
        var isDoc = a.Doctor.UserId == uid;
        if (!isDoc && a.PatientUserId != uid) throw new ForbiddenException();
        return (a, isDoc);
    }

    private async Task<bool> RequiredByPolicyAsync(Guid doctorId, CancellationToken ct) =>
        await _db.Set<DoctorBookingPolicy>().AnyAsync(p => p.DoctorId == doctorId && p.RequiresQuestionnaire, ct);

    private async Task<QuestionnaireTemplate?> ActiveTemplateAsync(Guid doctorId, CancellationToken ct) =>
        await _db.Set<QuestionnaireTemplate>().AsNoTracking().Where(t => t.DoctorId == doctorId && t.IsActive)
            .OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync(ct);

    public async Task<AppointmentQuestionnaireDto> GetForAppointmentAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var (a, _) = await LoadAsync(appointmentId, ct);
        var resp = await _db.Set<AppointmentQuestionnaireResponse>().AsNoTracking().SingleOrDefaultAsync(r => r.AppointmentId == appointmentId, ct);
        QuestionnaireTemplate? tpl = resp is null
            ? await ActiveTemplateAsync(a.DoctorId, ct)
            : await _db.Set<QuestionnaireTemplate>().AsNoTracking().SingleOrDefaultAsync(t => t.Id == resp.TemplateId, ct);
        var required = tpl is not null && await RequiredByPolicyAsync(a.DoctorId, ct);
        var answers = resp is null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(resp.AnswersJson, Json);
        return new AppointmentQuestionnaireDto(appointmentId, tpl is null ? null : ToDto(tpl), answers, resp?.SubmittedAt, required);
    }

    public async Task<AppointmentQuestionnaireDto> SubmitAsync(Guid appointmentId, SubmitQuestionnaireInput input, CancellationToken ct = default)
    {
        var (a, isDoc) = await LoadAsync(appointmentId, ct);
        if (isDoc) throw new ForbiddenException("Only the patient fills in the questionnaire.");
        if (a.Status is AppointmentStatus.CancelledByDoctor or AppointmentStatus.CancelledByUser or AppointmentStatus.Completed or AppointmentStatus.NoShow)
            throw new ConflictException("This appointment is closed.");
        var tpl = await ActiveTemplateAsync(a.DoctorId, ct) ?? throw new ConflictException("This doctor has no active questionnaire.");
        var questions = JsonSerializer.Deserialize<List<QuestionDto>>(tpl.QuestionsJson, Json) ?? new();

        var errors = new Dictionary<string, string[]>();
        var clean = new Dictionary<string, string>();
        foreach (var q in questions)
        {
            input.Answers.TryGetValue(q.Id, out var raw);
            var v = raw?.Trim();
            if (string.IsNullOrEmpty(v)) { if (q.Required) errors[q.Id] = new[] { "Required." }; continue; }
            if (v.Length > 2000) { errors[q.Id] = new[] { "Too long (max 2000 characters)." }; continue; }
            switch (q.Type)
            {
                case "yesno" when v is not ("yes" or "no"): errors[q.Id] = new[] { "Answer yes or no." }; continue;
                case "number" when !decimal.TryParse(v, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _):
                    errors[q.Id] = new[] { "Enter a number." }; continue;
                case "choice" when q.Options is null || !q.Options.Contains(v): errors[q.Id] = new[] { "Choose one of the options." }; continue;
            }
            clean[q.Id] = v;
        }
        if (errors.Count > 0) throw new Shared.Exceptions.ValidationException(errors);

        var set = _db.Set<AppointmentQuestionnaireResponse>();
        var r = await set.SingleOrDefaultAsync(x => x.AppointmentId == appointmentId, ct);
        if (r is null) { r = new AppointmentQuestionnaireResponse { AppointmentId = appointmentId }; set.Add(r); }
        r.TemplateId = tpl.Id;
        r.AnswersJson = JsonSerializer.Serialize(clean, Json);
        r.SubmittedAt = DateTime.UtcNow;
        _db.Notifications.Add(new InAppNotification
        {
            UserId = a.Doctor.UserId, Title = "Pre-visit questionnaire submitted",
            Body = $"Answers for the appointment at {a.ScheduledAt:u} are ready.", Url = $"/appointments/{a.Id}/encounter"
        });
        await _db.SaveChangesAsync(ct);
        return await GetForAppointmentAsync(appointmentId, ct);
    }

    public async Task EnsureCompletedIfRequiredAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var a = await _db.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == appointmentId, ct)
                ?? throw new NotFoundException("Appointment", appointmentId);
        if (!await RequiredByPolicyAsync(a.DoctorId, ct)) return;
        if (await ActiveTemplateAsync(a.DoctorId, ct) is null) return;
        if (!await _db.Set<AppointmentQuestionnaireResponse>().AnyAsync(r => r.AppointmentId == appointmentId, ct))
            throw new AppException("questionnaire_required", "Please complete the pre-visit questionnaire before checking in.");
    }
}

// =====================================================================================
// Clinical encounter, referrals, consent
// =====================================================================================
public class ClinicalEncounterService : IClinicalEncounterService
{
    private static readonly string[] CodeSystems = { "VeNom", "SNOMED", "Other" };
    private static readonly string[] Flags = { "Normal", "Low", "High", "Critical" };
    private static readonly string[] Scopes = { "Summary", "FullRecord" };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPetAccessService _petAccess;
    private readonly IAuditLogger _audit;
    private readonly ILogger<ClinicalEncounterService> _log;

    public ClinicalEncounterService(ApplicationDbContext db, ICurrentUserService current, IPetAccessService petAccess,
        IAuditLogger audit, ILogger<ClinicalEncounterService> log)
    { _db = db; _current = current; _petAccess = petAccess; _audit = audit; _log = log; }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    private sealed record Ctx(Appointment A, bool IsDoctor);

    private async Task<Ctx> LoadAsync(Guid appointmentId, CancellationToken ct)
    {
        var uid = Uid();
        var a = await _db.Appointments.Include(x => x.Doctor).ThenInclude(d => d.User).Include(x => x.PatientUser).Include(x => x.Pet)
                    .SingleOrDefaultAsync(x => x.Id == appointmentId, ct) ?? throw new NotFoundException("Appointment", appointmentId);
        var isDoc = a.Doctor.UserId == uid;
        if (!isDoc && a.PatientUserId != uid) throw new ForbiddenException();
        return new Ctx(a, isDoc);
    }

    private async Task<Ctx> LoadTreatingAsync(Guid appointmentId, CancellationToken ct)
    {
        var c = await LoadAsync(appointmentId, ct);
        if (!c.IsDoctor) throw new ForbiddenException("Only the treating doctor can do this.");
        return c;
    }

    private Task<ClinicalEncounter?> FindAsync(Guid appointmentId, CancellationToken ct) =>
        _db.Set<ClinicalEncounter>().Include(e => e.DiagnosisCodes).Include(e => e.LabResults)
            .SingleOrDefaultAsync(e => e.AppointmentId == appointmentId, ct);

    // ----- mapping -----

    private async Task<EncounterDto> ToDtoAsync(Ctx c, ClinicalEncounter? e, CancellationToken ct)
    {
        if (e is null || (!c.IsDoctor && e.Status == EncounterStatus.Draft))
            return new EncounterDto(null, c.A.Id, c.A.PetId, EncounterStatus.Draft, null, null, null, null,
                Array.Empty<DiagnosisCodeDto>(), Array.Empty<LabResultDto>(), 1, null, null,
                Array.Empty<EncounterAmendmentDto>(), Array.Empty<ReferralDto>(), c.IsDoctor, null);

        var amendments = await _db.Set<EncounterAmendment>().AsNoTracking().Where(a => a.EncounterId == e.Id)
            .OrderBy(a => a.Version).Select(a => new EncounterAmendmentDto(a.Version, a.Reason, a.AmendedAt, a.SignatureHash)).ToListAsync(ct);
        var refs = await ReferralsForEncounterAsync(e.Id, ct);
        return new EncounterDto(e.Id, e.AppointmentId, e.PetId, e.Status, e.ChiefComplaint, e.Findings, e.Diagnosis, e.TreatmentPlan,
            e.DiagnosisCodes.Where(x => !x.IsDeleted).Select(x => new DiagnosisCodeDto(x.Code, x.System, x.Label)).ToList(),
            e.LabResults.Where(x => !x.IsDeleted).Select(x => new LabResultDto(x.Id, x.Test, x.Value, x.Unit, x.ReferenceRange, x.Flag, x.DocumentUrl)).ToList(),
            e.Version, e.FinalizedAt, e.SignatureHash, amendments, refs, c.IsDoctor, e.UpdatedAt);
    }

    private async Task<List<ReferralDto>> ReferralsForEncounterAsync(Guid encounterId, CancellationToken ct)
    {
        var rows = await _db.Set<Referral>().AsNoTracking().Where(r => r.EncounterId == encounterId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return await MapReferralsAsync(rows, ct);
    }

    private async Task<List<ReferralDto>> MapReferralsAsync(List<Referral> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return new();
        var dIds = rows.SelectMany(r => new[] { r.FromDoctorId, r.ToDoctorId ?? Guid.Empty }).Distinct().ToList();
        var names = await _db.Doctors.AsNoTracking().Where(d => dIds.Contains(d.Id)).Select(d => new { d.Id, d.User.DisplayName }).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var eIds = rows.Select(r => r.EncounterId).Distinct().ToList();
        var pets = await _db.Set<ClinicalEncounter>().AsNoTracking().Where(e => eIds.Contains(e.Id))
            .Select(e => new { e.Id, e.PetId }).ToDictionaryAsync(x => x.Id, x => x.PetId, ct);
        var petIds = pets.Values.Where(v => v != null).Select(v => v!.Value).Distinct().ToList();
        var petNames = await _db.Pets.AsNoTracking().Where(p => petIds.Contains(p.Id)).Select(p => new { p.Id, p.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return rows.Select(r =>
        {
            var pid = pets.GetValueOrDefault(r.EncounterId);
            return new ReferralDto(r.Id, r.EncounterId, r.ToDoctorId, r.ToDoctorId is { } t ? names.GetValueOrDefault(t) : null, r.ExternalClinic,
                r.Reason, r.Status, r.ConsentToShareRecords, r.CreatedAt, pid is { } p ? petNames.GetValueOrDefault(p) : null, pid,
                names.GetValueOrDefault(r.FromDoctorId));
        }).ToList();
    }

    // ----- encounter -----

    public async Task<EncounterDto> GetAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var c = await LoadAsync(appointmentId, ct);
        return await ToDtoAsync(c, await FindAsync(appointmentId, ct), ct);
    }

    private static string? Clip(string? s, int max)
    {
        s = s?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        return s.Length > max ? throw new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { ["content"] = new[] { $"A field exceeds {max} characters." } }) : s;
    }

    private void ApplyContent(ClinicalEncounter e, SaveEncounterDraftInput i)
    {
        e.ChiefComplaint = Clip(i.ChiefComplaint, 2000);
        e.Findings = Clip(i.Findings, 8000);
        e.Diagnosis = Clip(i.Diagnosis, 4000);
        e.TreatmentPlan = Clip(i.TreatmentPlan, 8000);

        var codes = i.DiagnosisCodes ?? Array.Empty<DiagnosisCodeDto>();
        var labs = i.LabResults ?? Array.Empty<LabResultDto>();
        if (codes.Count > 30 || labs.Count > 50)
            throw new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { ["content"] = new[] { "Too many diagnosis codes or lab results." } });
        foreach (var c in codes)
        {
            if (string.IsNullOrWhiteSpace(c.Code) || c.Code.Length > 64 || !CodeSystems.Contains(c.System))
                throw new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { ["diagnosisCodes"] = new[] { "Each code needs a value and a system (VeNom, SNOMED or Other)." } });
        }
        foreach (var l in labs)
        {
            if (string.IsNullOrWhiteSpace(l.Test) || l.Test.Length > 200 || (l.Flag is not null && !Flags.Contains(l.Flag)))
                throw new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { ["labResults"] = new[] { "Each lab result needs a test name; flag must be Normal, Low, High or Critical." } });
            if (l.DocumentUrl is { Length: > 0 } u && (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
                throw new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { ["labResults"] = new[] { "Document URL must be http(s)." } });
        }

        _db.Set<EncounterDiagnosisCode>().RemoveRange(e.DiagnosisCodes.Where(x => !x.IsDeleted).ToList());
        _db.Set<EncounterLabResult>().RemoveRange(e.LabResults.Where(x => !x.IsDeleted).ToList());
        foreach (var c in codes.GroupBy(c => (c.System, Code: c.Code.Trim().ToUpperInvariant())).Select(g => g.First()))
            _db.Set<EncounterDiagnosisCode>().Add(new EncounterDiagnosisCode { EncounterId = e.Id, Code = c.Code.Trim(), System = c.System, Label = Clip(c.Label, 300) });
        foreach (var l in labs)
            _db.Set<EncounterLabResult>().Add(new EncounterLabResult
            {
                EncounterId = e.Id, Test = l.Test.Trim(), Value = Clip(l.Value, 200), Unit = Clip(l.Unit, 50),
                ReferenceRange = Clip(l.ReferenceRange, 100), Flag = l.Flag, DocumentUrl = l.DocumentUrl
            });
    }

    public async Task<EncounterDto> SaveDraftAsync(Guid appointmentId, SaveEncounterDraftInput input, CancellationToken ct = default)
    {
        var c = await LoadTreatingAsync(appointmentId, ct);
        if (c.A.Status is AppointmentStatus.CancelledByDoctor or AppointmentStatus.CancelledByUser or AppointmentStatus.Draft)
            throw new ConflictException("No clinical record can be written for this appointment.");
        var e = await FindAsync(appointmentId, ct);
        if (e is null)
        {
            e = new ClinicalEncounter { AppointmentId = appointmentId, DoctorId = c.A.DoctorId, PetId = c.A.PetId };
            _db.Set<ClinicalEncounter>().Add(e);
        }
        else if (e.Status != EncounterStatus.Draft) throw new ConflictException("This record is finalized. Use an amendment to change it.");
        ApplyContent(e, input);
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(c, await FindAsync(appointmentId, ct), ct);
    }

    private static string SignatureOf(ClinicalEncounter e, Guid doctorId, DateTime at, int version) =>
        EncounterSignature.Hash(EncounterSignature.Canonical(e.ChiefComplaint, e.Findings, e.Diagnosis, e.TreatmentPlan,
            e.DiagnosisCodes.Where(x => !x.IsDeleted).Select(x => (x.Code, x.System)), doctorId, at, version));

    public async Task<EncounterDto> FinalizeAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var c = await LoadTreatingAsync(appointmentId, ct);
        var e = await FindAsync(appointmentId, ct) ?? throw new ConflictException("Write the clinical record before finalizing it.");
        if (e.Status != EncounterStatus.Draft) throw new ConflictException("This record is already finalized.");
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(e.Diagnosis)) errors["diagnosis"] = new[] { "A diagnosis is required to finalize." };
        if (string.IsNullOrWhiteSpace(e.TreatmentPlan)) errors["treatmentPlan"] = new[] { "A treatment plan is required to finalize." };
        if (errors.Count > 0) throw new Shared.Exceptions.ValidationException(errors);

        var now = DateTime.UtcNow;
        e.Status = EncounterStatus.Finalized;
        e.Version = 1;
        e.FinalizedAt = now;
        e.FinalizedByDoctorId = c.A.DoctorId;
        e.SignatureHash = SignatureOf(e, c.A.DoctorId, now, 1);
        await SealPrescriptionsAsync(e, ct);

        _db.Notifications.Add(new InAppNotification
        {
            UserId = c.A.PatientUserId, Title = "Visit summary available",
            Body = c.A.Pet is null ? "Your vet finalized the clinical record." : $"{c.A.Pet.Name}'s clinical record from the visit is ready.",
            Url = $"/appointments?encounter={c.A.Id}"
        });
        await _db.SaveChangesAsync(ct);
        await TryAuditAsync("encounter.finalize", e, new { e.Version, e.SignatureHash });
        return await ToDtoAsync(c, await FindAsync(appointmentId, ct), ct);
    }

    private async Task SealPrescriptionsAsync(ClinicalEncounter e, CancellationToken ct)
    {
        var rxs = await _db.Prescriptions.AsNoTracking().Where(p => p.AppointmentId == e.AppointmentId).ToListAsync(ct);
        if (rxs.Count == 0) return;
        var ids = rxs.Select(r => r.Id).ToList();
        var sealedIds = (await _db.Set<PrescriptionSeal>().AsNoTracking().Where(s => ids.Contains(s.PrescriptionId)).Select(s => s.PrescriptionId).ToListAsync(ct)).ToHashSet();
        foreach (var rx in rxs.Where(r => !sealedIds.Contains(r.Id)))
        {
            var content = $"{rx.Id:D}|{rx.IssuedById:D}|{rx.IssuedAt:O}|{rx.Notes}|{rx.FileUrl}|{rx.ItemsJson}|{rx.ValidUntil:O}";
            _db.Set<PrescriptionSeal>().Add(new PrescriptionSeal { PrescriptionId = rx.Id, EncounterId = e.Id, ContentHash = EncounterSignature.Hash(content) });
        }
    }

    public async Task<EncounterDto> AmendAsync(Guid appointmentId, AmendEncounterInput input, CancellationToken ct = default)
    {
        var c = await LoadTreatingAsync(appointmentId, ct);
        var e = await FindAsync(appointmentId, ct) ?? throw new NotFoundException("ClinicalEncounter", appointmentId);
        if (e.Status == EncounterStatus.Draft) throw new ConflictException("Finalize the record first; drafts can be edited directly.");
        var reason = input.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length is < 5 or > 1000)
            throw new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { ["reason"] = new[] { "Give a reason (5-1000 characters)." } });

        // Snapshot the superseded content together with the signature it carried, so history stays verifiable.
        var snapshot = JsonSerializer.Serialize(new
        {
            e.Version, e.ChiefComplaint, e.Findings, e.Diagnosis, e.TreatmentPlan, SignedAt = e.FinalizedAt, e.SignatureHash,
            DiagnosisCodes = e.DiagnosisCodes.Where(x => !x.IsDeleted).Select(x => new { x.Code, x.System, x.Label }),
            LabResults = e.LabResults.Where(x => !x.IsDeleted).Select(x => new { x.Test, x.Value, x.Unit, x.ReferenceRange, x.Flag, x.DocumentUrl })
        }, Json);
        var now = DateTime.UtcNow;
        _db.Set<EncounterAmendment>().Add(new EncounterAmendment
        {
            EncounterId = e.Id, Version = e.Version + 1, SnapshotJson = snapshot, Reason = reason, AmendedAt = now,
            AmendedByDoctorId = c.A.DoctorId, SignatureHash = e.SignatureHash ?? ""
        });

        ApplyContent(e, input.Content);
        if (string.IsNullOrWhiteSpace(e.Diagnosis) || string.IsNullOrWhiteSpace(e.TreatmentPlan))
            throw new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { ["content"] = new[] { "Diagnosis and treatment plan must remain filled." } });
        e.Version += 1;
        e.Status = EncounterStatus.Amended;
        e.FinalizedAt = now;                       // time of the latest signature
        e.FinalizedByDoctorId = c.A.DoctorId;
        // Codes were just replaced in the tracker; compute the signature from the new content.
        var newCodes = _db.Set<EncounterDiagnosisCode>().Local.Where(x => x.EncounterId == e.Id && _db.Entry(x).State == EntityState.Added)
            .Select(x => (x.Code, x.System));
        e.SignatureHash = EncounterSignature.Hash(EncounterSignature.Canonical(e.ChiefComplaint, e.Findings, e.Diagnosis, e.TreatmentPlan,
            newCodes, c.A.DoctorId, now, e.Version));
        await SealPrescriptionsAsync(e, ct);
        await _db.SaveChangesAsync(ct);
        await TryAuditAsync("encounter.amend", e, new { e.Version, Reason = reason, e.SignatureHash });
        return await ToDtoAsync(c, await FindAsync(appointmentId, ct), ct);
    }

    private async Task TryAuditAsync(string action, ClinicalEncounter e, object data)
    {
        try { await _audit.LogAsync(action, nameof(ClinicalEncounter), e.Id.ToString(), "clinical", null, data); }
        catch (Exception ex) { _log.LogWarning(ex, "Audit log failed for {Action} {Encounter}", action, e.Id); }
    }

    // ----- referrals -----

    public async Task<ReferralDto> CreateReferralAsync(Guid appointmentId, CreateReferralInput input, CancellationToken ct = default)
    {
        var c = await LoadTreatingAsync(appointmentId, ct);
        var e = await FindAsync(appointmentId, ct) ?? throw new ConflictException("Start the clinical record before creating a referral.");
        var ext = input.ExternalClinic?.Trim();
        if ((input.ToDoctorId is null) == string.IsNullOrEmpty(ext))
            throw new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { ["toDoctorId"] = new[] { "Choose a doctor on Pawzaroo or enter an external clinic (not both)." } });
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 2000)
            throw new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { ["reason"] = new[] { "A reason is required (max 2000 characters)." } });
        Doctor? to = null;
        if (input.ToDoctorId is { } tid)
        {
            to = await _db.Doctors.Include(d => d.User).SingleOrDefaultAsync(d => d.Id == tid && d.ApprovalStatus == ApprovalStatus.Approved, ct)
                 ?? throw new NotFoundException("Doctor", tid);
            if (to.Id == c.A.DoctorId) throw new ConflictException("You cannot refer a patient to yourself.");
        }
        var r = new Referral
        {
            EncounterId = e.Id, FromDoctorId = c.A.DoctorId, ToDoctorId = to?.Id, ExternalClinic = ext is { Length: > 300 } ? ext[..300] : ext,
            Reason = input.Reason.Trim(), ConsentToShareRecords = input.ConsentToShareRecords
        };
        _db.Set<Referral>().Add(r);
        if (to is not null)
            _db.Notifications.Add(new InAppNotification { UserId = to.UserId, Title = "New patient referral",
                Body = $"{c.A.Doctor.User.DisplayName} referred a patient to you.", Url = "/appointments/referrals" });
        _db.Notifications.Add(new InAppNotification
        {
            UserId = c.A.PatientUserId, Title = "Your vet created a referral",
            Body = input.ConsentToShareRecords && c.A.PetId is not null
                ? "The receiving vet asks to view your pet's records. You can grant or decline this in the visit summary."
                : "Your vet referred you to another clinic.",
            Url = $"/appointments?encounter={c.A.Id}"
        });
        await _db.SaveChangesAsync(ct);
        return (await MapReferralsAsync(new List<Referral> { r }, ct))[0];
    }

    public async Task<IReadOnlyList<ReferralDto>> IncomingReferralsAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var did = await _db.Doctors.Where(d => d.UserId == uid).Select(d => (Guid?)d.Id).SingleOrDefaultAsync(ct)
                  ?? throw new ForbiddenException();
        var rows = await _db.Set<Referral>().AsNoTracking().Where(r => r.ToDoctorId == did).OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync(ct);
        return await MapReferralsAsync(rows, ct);
    }

    public async Task<ReferralDto> RespondReferralAsync(Guid referralId, RespondReferralInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var r = await _db.Set<Referral>().SingleOrDefaultAsync(x => x.Id == referralId, ct) ?? throw new NotFoundException("Referral", referralId);
        var me = await _db.Doctors.Where(d => d.UserId == uid).Select(d => new { d.Id, Name = d.User.DisplayName }).SingleOrDefaultAsync(ct);
        if (me is null || r.ToDoctorId != me.Id) throw new ForbiddenException();
        if (r.Status != ReferralStatus.Pending) throw new ConflictException("This referral was already answered.");

        var enc = await _db.Set<ClinicalEncounter>().AsNoTracking().SingleAsync(x => x.Id == r.EncounterId, ct);
        if (input.Accept && r.ConsentToShareRecords && enc.PetId is { } petId && !await HasActiveConsentAsync(petId, me.Id, ct))
            throw new AppException("consent_required", "The pet owner has not granted record sharing for this referral yet.");

        r.Status = input.Accept ? ReferralStatus.Accepted : ReferralStatus.Declined;
        r.RespondedAt = DateTime.UtcNow;
        var fromUser = await _db.Doctors.Where(d => d.Id == r.FromDoctorId).Select(d => d.UserId).SingleAsync(ct);
        _db.Notifications.Add(new InAppNotification { UserId = fromUser, Title = input.Accept ? "Referral accepted" : "Referral declined",
            Body = $"{me.Name} {(input.Accept ? "accepted" : "declined")} your referral.", Url = "/appointments" });
        await _db.SaveChangesAsync(ct);
        return (await MapReferralsAsync(new List<Referral> { r }, ct))[0];
    }

    // ----- record-sharing consent -----

    public async Task<RecordConsentDto> GrantConsentAsync(GrantRecordConsentInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        await _petAccess.EnsureOwnerAsync(input.PetId, ct);
        var scope = string.IsNullOrWhiteSpace(input.Scope) ? "FullRecord" : input.Scope!;
        if (!Scopes.Contains(scope) || input.ExpiresInDays is < 1 or > 365)
            throw new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { ["scope"] = new[] { "Scope must be Summary or FullRecord; expiry 1-365 days." } });
        var doc = await _db.Doctors.Include(d => d.User).SingleOrDefaultAsync(d => d.Id == input.GranteeDoctorId && d.ApprovalStatus == ApprovalStatus.Approved, ct)
                  ?? throw new NotFoundException("Doctor", input.GranteeDoctorId);

        var now = DateTime.UtcNow;
        var existing = await _db.Set<RecordShareConsent>().Where(x => x.PetId == input.PetId && x.GranteeDoctorId == doc.Id && x.RevokedAt == null && x.ExpiresAt > now).ToListAsync(ct);
        foreach (var x in existing) x.RevokedAt = now;     // replace, never stack
        var consent = new RecordShareConsent
        {
            PetId = input.PetId, GrantedByUserId = uid, GranteeDoctorId = doc.Id, Scope = scope, ExpiresAt = now.AddDays(input.ExpiresInDays)
        };
        _db.Set<RecordShareConsent>().Add(consent);
        _db.Notifications.Add(new InAppNotification { UserId = doc.UserId, Title = "Pet records shared with you",
            Body = $"An owner granted you access until {consent.ExpiresAt:yyyy-MM-dd}.", Url = "/appointments/referrals" });
        await _db.SaveChangesAsync(ct);
        return new RecordConsentDto(consent.Id, consent.PetId, doc.Id, doc.User.DisplayName, consent.Scope, consent.ExpiresAt, null);
    }

    public async Task<IReadOnlyList<RecordConsentDto>> ListConsentsAsync(Guid petId, CancellationToken ct = default)
    {
        await _petAccess.EnsureOwnerAsync(petId, ct);
        return await _db.Set<RecordShareConsent>().AsNoTracking().Where(x => x.PetId == petId).OrderByDescending(x => x.CreatedAt).Take(50)
            .Join(_db.Doctors, x => x.GranteeDoctorId, d => d.Id, (x, d) => new RecordConsentDto(x.Id, x.PetId, d.Id, d.User.DisplayName, x.Scope, x.ExpiresAt, x.RevokedAt))
            .ToListAsync(ct);
    }

    public async Task RevokeConsentAsync(Guid consentId, CancellationToken ct = default)
    {
        var x = await _db.Set<RecordShareConsent>().SingleOrDefaultAsync(c => c.Id == consentId, ct) ?? throw new NotFoundException("Consent", consentId);
        await _petAccess.EnsureOwnerAsync(x.PetId, ct);
        if (x.RevokedAt is null) { x.RevokedAt = DateTime.UtcNow; await _db.SaveChangesAsync(ct); }
    }

    public Task<bool> HasActiveConsentAsync(Guid petId, Guid doctorId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return _db.Set<RecordShareConsent>().AsNoTracking()
            .AnyAsync(x => x.PetId == petId && x.GranteeDoctorId == doctorId && x.RevokedAt == null && x.ExpiresAt > now, ct);
    }
}

// =====================================================================================
// Completion gate + prescription sealing hooks
// =====================================================================================
public class ClinicalGate : IClinicalGate
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly IAuditLogger _audit;
    private readonly ILogger<ClinicalGate> _log;
    public ClinicalGate(ApplicationDbContext db, IConfiguration config, IAuditLogger audit, ILogger<ClinicalGate> log)
    { _db = db; _config = config; _audit = audit; _log = log; }

    /// <summary>Default FALSE (does not break existing flows); set Clinical:RequireFinalizedEncounter=true to enforce.</summary>
    public bool Enabled => _config.GetValue("Clinical:RequireFinalizedEncounter", false);

    public async Task EnsureCanCompleteAsync(Appointment appt, string? overrideReason, CancellationToken ct = default)
    {
        if (!Enabled) return;
        var enc = await _db.Set<ClinicalEncounter>().AsNoTracking().SingleOrDefaultAsync(e => e.AppointmentId == appt.Id, ct);
        var ok = enc is { Status: EncounterStatus.Finalized or EncounterStatus.Amended }
                 && !string.IsNullOrWhiteSpace(enc.Diagnosis) && !string.IsNullOrWhiteSpace(enc.TreatmentPlan);
        if (ok) return;

        var reason = overrideReason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length < 10)
            throw new ConflictException("clinical_incomplete: finalize the clinical record (diagnosis and treatment plan) before completing, or provide an override reason of at least 10 characters.");

        _db.Set<EncounterCompletionOverride>().Add(new EncounterCompletionOverride
        { AppointmentId = appt.Id, DoctorId = appt.DoctorId, Reason = reason.Length > 1000 ? reason[..1000] : reason });
        try { await _audit.LogAsync("appointment.complete_override", nameof(Appointment), appt.Id.ToString(), "clinical", null, new { Reason = reason }, ct); }
        catch (Exception ex) { _log.LogWarning(ex, "Audit log failed for completion override {Appointment}", appt.Id); }
    }

    public async Task EnsurePrescriptionEditableAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var sealedEncounter = await _db.Set<ClinicalEncounter>().AsNoTracking()
            .AnyAsync(e => e.AppointmentId == appointmentId && e.Status != EncounterStatus.Draft, ct);
        if (sealedEncounter)
            throw new ConflictException("prescription_sealed: this visit's clinical record is finalized and its prescriptions are sealed. Amend the record to proceed.");
    }
}
