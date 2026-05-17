using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Veterinary;

namespace Pawzaroo.Infrastructure.Modules.Vet;

public class DoctorAvailabilityConfiguration : IEntityTypeConfiguration<DoctorAvailability>
{
    public void Configure(EntityTypeBuilder<DoctorAvailability> e)
    {
        e.ToTable("doctor_availabilities");
        e.HasIndex(x => new { x.DoctorId, x.DayOfWeek });
        e.HasOne(x => x.Doctor).WithMany(d => d.Availabilities).HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DoctorHolidayConfiguration : IEntityTypeConfiguration<DoctorHoliday>
{
    public void Configure(EntityTypeBuilder<DoctorHoliday> e)
    {
        e.ToTable("doctor_holidays");
        e.HasIndex(x => new { x.DoctorId, x.Date }).IsUnique();
        e.HasOne(x => x.Doctor).WithMany(d => d.Holidays).HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DoctorCredentialDocumentConfiguration : IEntityTypeConfiguration<DoctorCredentialDocument>
{
    public void Configure(EntityTypeBuilder<DoctorCredentialDocument> e)
    {
        e.ToTable("doctor_credential_documents");
        e.HasIndex(x => x.DoctorId);
        e.Property(x => x.Title).HasMaxLength(256).IsRequired();
        e.Property(x => x.FileUrl).HasMaxLength(1024).IsRequired();
        e.Property(x => x.IssuingAuthority).HasMaxLength(256);
        e.Property(x => x.DocumentNumber).HasMaxLength(128);
        e.HasOne(x => x.Doctor).WithMany(d => d.CredentialDocuments).HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.VerifiedByUser).WithMany().HasForeignKey(x => x.VerifiedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class AppointmentExtendedConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> e)
    {
        // Base config in DomainConfigurations.AppointmentConfiguration; this adds
        // the new slot-link + status indexes introduced by the full Vet module.
        e.Property(x => x.MeetingLink).HasMaxLength(1024);
        e.Property(x => x.FollowUpNotes).HasMaxLength(4000);
        e.Property(x => x.CancellationReason).HasMaxLength(1000);
        e.HasIndex(x => new { x.PatientUserId, x.Status });
        e.HasIndex(x => new { x.DoctorId, x.Status, x.ScheduledAt });
        e.HasIndex(x => x.TimeSlotId).IsUnique().HasFilter("time_slot_id IS NOT NULL");
        e.HasOne(x => x.TimeSlot).WithMany().HasForeignKey(x => x.TimeSlotId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class AppointmentDisputeConfiguration : IEntityTypeConfiguration<AppointmentDispute>
{
    public void Configure(EntityTypeBuilder<AppointmentDispute> e)
    {
        e.ToTable("appointment_disputes");
        e.HasIndex(x => new { x.AppointmentId, x.Status });
        e.Property(x => x.Reason).HasMaxLength(256).IsRequired();
        e.Property(x => x.Details).HasMaxLength(4000);
        e.Property(x => x.Resolution).HasMaxLength(4000);
        e.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.OpenedByUser).WithMany().HasForeignKey(x => x.OpenedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ResolvedByUser).WithMany().HasForeignKey(x => x.ResolvedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
