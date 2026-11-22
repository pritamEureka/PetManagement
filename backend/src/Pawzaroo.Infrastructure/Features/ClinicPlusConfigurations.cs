using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Veterinary;

namespace Pawzaroo.Infrastructure.Features;

public class DoctorEmergencyProfileConfiguration : IEntityTypeConfiguration<DoctorEmergencyProfile>
{
    public void Configure(EntityTypeBuilder<DoctorEmergencyProfile> e)
    {
        e.ToTable("doctor_emergency_profiles");
        e.HasIndex(x => x.DoctorId).IsUnique();
        e.HasIndex(x => new { x.Is24x7, x.City });
        e.Property(x => x.EmergencyPhone).HasMaxLength(32);
        e.Property(x => x.ClinicName).HasMaxLength(200);
        e.Property(x => x.ClinicAddress).HasMaxLength(400);
        e.Property(x => x.City).HasMaxLength(120);
        e.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DoctorBookingPolicyConfiguration : IEntityTypeConfiguration<DoctorBookingPolicy>
{
    public void Configure(EntityTypeBuilder<DoctorBookingPolicy> e)
    {
        e.ToTable("doctor_booking_policies");
        e.HasIndex(x => x.DoctorId).IsUnique();
        e.Property(x => x.DepositPercent).HasPrecision(5, 2);
        e.Property(x => x.DepositFixed).HasPrecision(12, 2);
        e.Property(x => x.LateCancelFeePercent).HasPrecision(5, 2);
        e.Property(x => x.NoShowFeePercent).HasPrecision(5, 2);
        e.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppointmentDepositConfiguration : IEntityTypeConfiguration<AppointmentDeposit>
{
    public void Configure(EntityTypeBuilder<AppointmentDeposit> e)
    {
        e.ToTable("appointment_deposits");
        e.HasIndex(x => x.AppointmentId).IsUnique();
        e.Property(x => x.Amount).HasPrecision(12, 2);
        e.Property(x => x.FeeAmount).HasPrecision(12, 2);
        e.Property(x => x.RefundAmount).HasPrecision(12, 2);
        e.Property(x => x.Note).HasMaxLength(500);
        e.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppointmentFollowUpConfiguration : IEntityTypeConfiguration<AppointmentFollowUp>
{
    public void Configure(EntityTypeBuilder<AppointmentFollowUp> e)
    {
        e.ToTable("appointment_follow_ups");
        e.HasIndex(x => new { x.PatientUserId, x.Status });
        e.HasIndex(x => x.SourceAppointmentId);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.HasOne<Appointment>().WithMany().HasForeignKey(x => x.SourceAppointmentId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<Appointment>().WithMany().HasForeignKey(x => x.FollowUpAppointmentId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CalendarShareConfiguration : IEntityTypeConfiguration<CalendarShare>
{
    public void Configure(EntityTypeBuilder<CalendarShare> e)
    {
        e.ToTable("calendar_shares");
        e.HasIndex(x => x.TokenHash).IsUnique();
        e.HasIndex(x => x.OwnerUserId);
        e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        e.Property(x => x.TokenHint).HasMaxLength(16).IsRequired();
        e.Property(x => x.Label).HasMaxLength(120);
    }
}

public class QuestionnaireTemplateConfiguration : IEntityTypeConfiguration<QuestionnaireTemplate>
{
    public void Configure(EntityTypeBuilder<QuestionnaireTemplate> e)
    {
        e.ToTable("questionnaire_templates");
        e.HasIndex(x => new { x.DoctorId, x.IsActive });
        e.Property(x => x.Title).HasMaxLength(200).IsRequired();
        e.Property(x => x.QuestionsJson).HasColumnType("jsonb");
        e.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppointmentQuestionnaireResponseConfiguration : IEntityTypeConfiguration<AppointmentQuestionnaireResponse>
{
    public void Configure(EntityTypeBuilder<AppointmentQuestionnaireResponse> e)
    {
        e.ToTable("appointment_questionnaire_responses");
        e.HasIndex(x => x.AppointmentId).IsUnique();
        e.Property(x => x.AnswersJson).HasColumnType("jsonb");
        e.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<QuestionnaireTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ClinicalEncounterConfiguration : IEntityTypeConfiguration<ClinicalEncounter>
{
    public void Configure(EntityTypeBuilder<ClinicalEncounter> e)
    {
        e.ToTable("clinical_encounters");
        e.HasIndex(x => x.AppointmentId).IsUnique();
        e.HasIndex(x => new { x.DoctorId, x.Status });
        e.Property(x => x.ChiefComplaint).HasMaxLength(2000);
        e.Property(x => x.Findings).HasMaxLength(8000);
        e.Property(x => x.Diagnosis).HasMaxLength(4000);
        e.Property(x => x.TreatmentPlan).HasMaxLength(8000);
        e.Property(x => x.SignatureHash).HasMaxLength(64);
        e.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.DiagnosisCodes).WithOne().HasForeignKey(c => c.EncounterId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.LabResults).WithOne().HasForeignKey(c => c.EncounterId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EncounterDiagnosisCodeConfiguration : IEntityTypeConfiguration<EncounterDiagnosisCode>
{
    public void Configure(EntityTypeBuilder<EncounterDiagnosisCode> e)
    {
        e.ToTable("encounter_diagnosis_codes");
        e.HasIndex(x => new { x.System, x.Code });
        e.Property(x => x.Code).HasMaxLength(64).IsRequired();
        e.Property(x => x.System).HasMaxLength(24).IsRequired();
        e.Property(x => x.Label).HasMaxLength(300);
    }
}

public class EncounterAmendmentConfiguration : IEntityTypeConfiguration<EncounterAmendment>
{
    public void Configure(EntityTypeBuilder<EncounterAmendment> e)
    {
        e.ToTable("encounter_amendments");
        e.HasIndex(x => new { x.EncounterId, x.Version }).IsUnique();
        e.Property(x => x.SnapshotJson).HasColumnType("jsonb");
        e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        e.Property(x => x.SignatureHash).HasMaxLength(64).IsRequired();
        e.HasOne<ClinicalEncounter>().WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PrescriptionSealConfiguration : IEntityTypeConfiguration<PrescriptionSeal>
{
    public void Configure(EntityTypeBuilder<PrescriptionSeal> e)
    {
        e.ToTable("prescription_seals");
        e.HasIndex(x => x.PrescriptionId).IsUnique();
        e.HasIndex(x => x.EncounterId);
        e.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        e.HasOne<Prescription>().WithMany().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EncounterLabResultConfiguration : IEntityTypeConfiguration<EncounterLabResult>
{
    public void Configure(EntityTypeBuilder<EncounterLabResult> e)
    {
        e.ToTable("encounter_lab_results");
        e.Property(x => x.Test).HasMaxLength(200).IsRequired();
        e.Property(x => x.Value).HasMaxLength(200);
        e.Property(x => x.Unit).HasMaxLength(50);
        e.Property(x => x.ReferenceRange).HasMaxLength(100);
        e.Property(x => x.Flag).HasMaxLength(20);
        e.Property(x => x.DocumentUrl).HasMaxLength(1024);
    }
}

public class EncounterCompletionOverrideConfiguration : IEntityTypeConfiguration<EncounterCompletionOverride>
{
    public void Configure(EntityTypeBuilder<EncounterCompletionOverride> e)
    {
        e.ToTable("encounter_completion_overrides");
        e.HasIndex(x => x.AppointmentId);
        e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        e.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReferralConfiguration : IEntityTypeConfiguration<Referral>
{
    public void Configure(EntityTypeBuilder<Referral> e)
    {
        e.ToTable("referrals");
        e.HasIndex(x => x.EncounterId);
        e.HasIndex(x => new { x.ToDoctorId, x.Status });
        e.Property(x => x.ExternalClinic).HasMaxLength(300);
        e.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        e.HasOne<ClinicalEncounter>().WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RecordShareConsentConfiguration : IEntityTypeConfiguration<RecordShareConsent>
{
    public void Configure(EntityTypeBuilder<RecordShareConsent> e)
    {
        e.ToTable("record_share_consents");
        e.HasIndex(x => new { x.PetId, x.GranteeDoctorId });
        e.Property(x => x.Scope).HasMaxLength(24).IsRequired();
    }
}
