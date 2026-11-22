using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Veterinary;

namespace Pawzaroo.Infrastructure.Features;

public class TelemedicineRoomConfiguration : IEntityTypeConfiguration<TelemedicineRoom>
{
    public void Configure(EntityTypeBuilder<TelemedicineRoom> e)
    {
        e.ToTable("telemedicine_rooms");
        e.HasIndex(x => x.AppointmentId).IsUnique();
        e.HasIndex(x => new { x.ClosedAt, x.ExpiresAt });
        e.Property(x => x.RoomName).HasMaxLength(128).IsRequired();
        e.Property(x => x.JoinUrl).HasMaxLength(1024).IsRequired();
        e.Property(x => x.PreConsultNote).HasMaxLength(2000);
        e.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TelemedicineEventConfiguration : IEntityTypeConfiguration<TelemedicineEvent>
{
    public void Configure(EntityTypeBuilder<TelemedicineEvent> e)
    {
        e.ToTable("telemedicine_events");
        e.HasIndex(x => new { x.RoomId, x.At });
        e.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConsultationNoteConfiguration : IEntityTypeConfiguration<ConsultationNote>
{
    public void Configure(EntityTypeBuilder<ConsultationNote> e)
    {
        e.ToTable("consultation_notes");
        e.HasIndex(x => x.AppointmentId).IsUnique();
        e.Property(x => x.Subjective).HasMaxLength(4000);
        e.Property(x => x.Objective).HasMaxLength(4000);
        e.Property(x => x.Assessment).HasMaxLength(4000);
        e.Property(x => x.Plan).HasMaxLength(4000);
        e.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.AuthorDoctor).WithMany().HasForeignKey(x => x.AuthorDoctorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AppointmentWaitlistEntryConfiguration : IEntityTypeConfiguration<AppointmentWaitlistEntry>
{
    public void Configure(EntityTypeBuilder<AppointmentWaitlistEntry> e)
    {
        e.ToTable("appointment_waitlist_entries");
        e.HasIndex(x => new { x.DoctorId, x.Status, x.CreatedAt });
        e.HasIndex(x => x.UserId);
        e.Property(x => x.Note).HasMaxLength(500);
        e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppointmentSeriesConfiguration : IEntityTypeConfiguration<AppointmentSeries>
{
    public void Configure(EntityTypeBuilder<AppointmentSeries> e)
    {
        e.ToTable("appointment_series");
        e.HasIndex(x => x.PatientUserId);
        e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppointmentSeriesItemConfiguration : IEntityTypeConfiguration<AppointmentSeriesItem>
{
    public void Configure(EntityTypeBuilder<AppointmentSeriesItem> e)
    {
        e.ToTable("appointment_series_items");
        e.HasIndex(x => x.AppointmentId).IsUnique();
        e.HasOne(x => x.Series).WithMany(s => s.Items).HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
    }
}
