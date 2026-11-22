using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Pets;

namespace Pawzaroo.Infrastructure.Modules.Pets;

// Auto-applied by ApplicationDbContext (ApplyConfigurationsFromAssembly). The entities have no DbSet
// properties; services reach them through _db.Set<T>().

public class CarePlanTemplateConfiguration : IEntityTypeConfiguration<CarePlanTemplate>
{
    public void Configure(EntityTypeBuilder<CarePlanTemplate> e)
    {
        e.ToTable("care_plan_templates");
        e.Property(x => x.Title).HasMaxLength(200).IsRequired();
        e.Property(x => x.Breed).HasMaxLength(128);
        e.Property(x => x.Description).HasMaxLength(2000);
        e.HasIndex(x => new { x.IsPublished, x.Species });
        e.HasIndex(x => x.PublishedByUserId);
    }
}

public class CarePlanTemplateItemConfiguration : IEntityTypeConfiguration<CarePlanTemplateItem>
{
    public void Configure(EntityTypeBuilder<CarePlanTemplateItem> e)
    {
        e.ToTable("care_plan_template_items");
        e.Property(x => x.Title).HasMaxLength(200).IsRequired();
        e.Property(x => x.Notes).HasMaxLength(1000);
        e.HasIndex(x => x.TemplateId);
        e.HasOne(x => x.Template).WithMany(t => t.Items).HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PetCarePlanConfiguration : IEntityTypeConfiguration<PetCarePlan>
{
    public void Configure(EntityTypeBuilder<PetCarePlan> e)
    {
        e.ToTable("pet_care_plans");
        e.Property(x => x.Title).HasMaxLength(200).IsRequired();
        e.HasIndex(x => new { x.PetId, x.Status });
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PetCarePlanItemConfiguration : IEntityTypeConfiguration<PetCarePlanItem>
{
    public void Configure(EntityTypeBuilder<PetCarePlanItem> e)
    {
        e.ToTable("pet_care_plan_items");
        e.Property(x => x.Title).HasMaxLength(200).IsRequired();
        e.Property(x => x.Notes).HasMaxLength(1000);
        e.Property(x => x.LinkedRecordType).HasMaxLength(32);
        e.HasIndex(x => new { x.PetId, x.Status, x.DueDate });
        e.HasIndex(x => new { x.Status, x.DueDate });
        e.HasIndex(x => x.PlanId);
        e.HasOne(x => x.Plan).WithMany(p => p.Items).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExposureReportConfiguration : IEntityTypeConfiguration<ExposureReport>
{
    public void Configure(EntityTypeBuilder<ExposureReport> e)
    {
        e.ToTable("exposure_reports");
        e.Property(x => x.Substance).HasMaxLength(200).IsRequired();
        e.Property(x => x.AmountEstimate).HasMaxLength(200);
        e.Property(x => x.Symptoms).HasMaxLength(2000);
        e.HasIndex(x => new { x.PetId, x.CreatedAt });
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExposureReportEventConfiguration : IEntityTypeConfiguration<ExposureReportEvent>
{
    public void Configure(EntityTypeBuilder<ExposureReportEvent> e)
    {
        e.ToTable("exposure_report_events");
        e.Property(x => x.Kind).HasMaxLength(48).IsRequired();
        e.Property(x => x.Note).HasMaxLength(2000);
        e.HasIndex(x => new { x.ExposureReportId, x.At });
        e.HasOne(x => x.ExposureReport).WithMany(r => r.Timeline).HasForeignKey(x => x.ExposureReportId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PetEmergencyCardLinkConfiguration : IEntityTypeConfiguration<PetEmergencyCardLink>
{
    public void Configure(EntityTypeBuilder<PetEmergencyCardLink> e)
    {
        e.ToTable("pet_emergency_card_links");
        e.Property(x => x.Token).HasMaxLength(128).IsRequired();
        e.HasIndex(x => x.Token).IsUnique();
        e.HasIndex(x => x.PetId);
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PetAllergyConfiguration : IEntityTypeConfiguration<PetAllergy>
{
    public void Configure(EntityTypeBuilder<PetAllergy> e)
    {
        e.ToTable("pet_allergies");
        e.Property(x => x.Allergen).HasMaxLength(200).IsRequired();
        e.Property(x => x.Reaction).HasMaxLength(500);
        e.HasIndex(x => new { x.PetId, x.Active });
        e.HasIndex(x => x.Allergen);
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PetDietEntryConfiguration : IEntityTypeConfiguration<PetDietEntry>
{
    public void Configure(EntityTypeBuilder<PetDietEntry> e)
    {
        e.ToTable("pet_diet_entries");
        e.Property(x => x.Food).HasMaxLength(200).IsRequired();
        e.Property(x => x.Amount).HasMaxLength(100);
        e.Property(x => x.Frequency).HasMaxLength(100);
        e.Property(x => x.Notes).HasMaxLength(500);
        e.HasIndex(x => new { x.PetId, x.Active });
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class HealthRecordVersionConfiguration : IEntityTypeConfiguration<HealthRecordVersion>
{
    public void Configure(EntityTypeBuilder<HealthRecordVersion> e)
    {
        e.ToTable("health_record_versions");
        e.Property(x => x.SnapshotJson).IsRequired();
        e.Property(x => x.ChangeReason).HasMaxLength(500);
        e.HasIndex(x => new { x.RecordType, x.RecordId, x.Version }).IsUnique();
    }
}

public class HealthRecordSealConfiguration : IEntityTypeConfiguration<HealthRecordSeal>
{
    public void Configure(EntityTypeBuilder<HealthRecordSeal> e)
    {
        e.ToTable("health_record_seals");
        e.Property(x => x.SnapshotJson).IsRequired();
        e.Property(x => x.SignatureHash).HasMaxLength(64).IsRequired();
        e.HasIndex(x => new { x.RecordType, x.RecordId }).IsUnique();
        e.HasIndex(x => x.PetId);
    }
}

public class VaccinationCertificateConfiguration : IEntityTypeConfiguration<VaccinationCertificate>
{
    public void Configure(EntityTypeBuilder<VaccinationCertificate> e)
    {
        e.ToTable("vaccination_certificates");
        e.Property(x => x.Token).HasMaxLength(128).IsRequired();
        e.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        e.HasIndex(x => x.Token).IsUnique();
        e.HasIndex(x => x.PetId);
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PetLabResultConfiguration : IEntityTypeConfiguration<PetLabResult>
{
    public void Configure(EntityTypeBuilder<PetLabResult> e)
    {
        e.ToTable("pet_lab_results");
        e.Property(x => x.TestName).HasMaxLength(200).IsRequired();
        e.Property(x => x.Value).HasMaxLength(100);
        e.Property(x => x.Unit).HasMaxLength(50);
        e.Property(x => x.ReferenceRange).HasMaxLength(100);
        e.Property(x => x.DocumentUrl).HasMaxLength(1024);
        e.HasIndex(x => new { x.PetId, x.TakenAt });
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LivestockTreatmentConfiguration : IEntityTypeConfiguration<LivestockTreatment>
{
    public void Configure(EntityTypeBuilder<LivestockTreatment> e)
    {
        e.ToTable("livestock_treatments");
        e.Property(x => x.Diagnosis).HasMaxLength(300).IsRequired();
        e.Property(x => x.Treatment).HasMaxLength(300).IsRequired();
        e.Property(x => x.Notes).HasMaxLength(1000);
        e.HasIndex(x => new { x.OwnerId, x.AdministeredOn });
        e.HasIndex(x => x.PetId);
        e.HasIndex(x => x.PetGroupId);
        e.HasIndex(x => x.BatchId);
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
    }
}
