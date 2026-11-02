using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Pets;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

public class PetLineageConfiguration : IEntityTypeConfiguration<Pet>
{
    // Second configuration for Pet — EF merges it with PetConfiguration. Kept
    // separate so the lineage / identification columns live next to the other
    // pet-care tables they relate to.
    public void Configure(EntityTypeBuilder<Pet> e)
    {
        e.Property(x => x.MicrochipNumber).HasMaxLength(64);
        e.Property(x => x.PublicCode).HasMaxLength(16);
        e.HasIndex(x => x.PublicCode).IsUnique().HasFilter("public_code IS NOT NULL");
        e.HasIndex(x => x.MicrochipNumber);
        e.HasOne(x => x.Sire).WithMany().HasForeignKey(x => x.SireId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.Dam).WithMany().HasForeignKey(x => x.DamId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.Group).WithMany(g => g.Members).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PetWeightLogConfiguration : IEntityTypeConfiguration<PetWeightLog>
{
    public void Configure(EntityTypeBuilder<PetWeightLog> e)
    {
        e.ToTable("pet_weight_logs");
        e.Property(x => x.WeightKg).HasPrecision(8, 2);
        e.Property(x => x.Notes).HasMaxLength(500);
        e.HasOne(x => x.Pet).WithMany(p => p.WeightLogs).HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.PetId, x.RecordedOn });
    }
}

public class PetMedicationConfiguration : IEntityTypeConfiguration<PetMedication>
{
    public void Configure(EntityTypeBuilder<PetMedication> e)
    {
        e.ToTable("pet_medications");
        e.Property(x => x.DrugName).HasMaxLength(200).IsRequired();
        e.Property(x => x.Dose).HasMaxLength(100);
        e.Property(x => x.DoseTimes).HasMaxLength(100).IsRequired();
        e.Property(x => x.Instructions).HasMaxLength(1000);
        e.HasOne(x => x.Pet).WithMany(p => p.Medications).HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.PetId, x.IsActive });
    }
}

public class MedicationDoseLogConfiguration : IEntityTypeConfiguration<MedicationDoseLog>
{
    public void Configure(EntityTypeBuilder<MedicationDoseLog> e)
    {
        e.ToTable("medication_dose_logs");
        e.Property(x => x.Notes).HasMaxLength(500);
        e.HasOne(x => x.Medication).WithMany(m => m.Doses).HasForeignKey(x => x.MedicationId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.MedicationId, x.ScheduledFor }).IsUnique();
    }
}

public class PetCaretakerConfiguration : IEntityTypeConfiguration<PetCaretaker>
{
    public void Configure(EntityTypeBuilder<PetCaretaker> e)
    {
        e.ToTable("pet_caretakers");
        e.HasOne(x => x.Pet).WithMany(p => p.Caretakers).HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.PetId, x.UserId }).IsUnique().HasFilter("is_deleted = false");
        e.HasIndex(x => x.UserId);
    }
}

public class PetCareRoutineConfiguration : IEntityTypeConfiguration<PetCareRoutine>
{
    public void Configure(EntityTypeBuilder<PetCareRoutine> e)
    {
        e.ToTable("pet_care_routines");
        e.Property(x => x.Title).HasMaxLength(128).IsRequired();
        e.Property(x => x.Details).HasMaxLength(500);
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PetCareLogConfiguration : IEntityTypeConfiguration<PetCareLog>
{
    public void Configure(EntityTypeBuilder<PetCareLog> e)
    {
        e.ToTable("pet_care_logs");
        e.Property(x => x.Notes).HasMaxLength(500);
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Routine).WithMany().HasForeignKey(x => x.RoutineId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.PerformedBy).WithMany().HasForeignKey(x => x.PerformedByUserId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.PetId, x.PerformedAt });
    }
}

public class PetExpenseConfiguration : IEntityTypeConfiguration<PetExpense>
{
    public void Configure(EntityTypeBuilder<PetExpense> e)
    {
        e.ToTable("pet_expenses");
        e.Property(x => x.Amount).HasPrecision(12, 2);
        e.Property(x => x.Description).HasMaxLength(500);
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.PetId, x.SpentOn });
    }
}

public class PetInsurancePolicyConfiguration : IEntityTypeConfiguration<PetInsurancePolicy>
{
    public void Configure(EntityTypeBuilder<PetInsurancePolicy> e)
    {
        e.ToTable("pet_insurance_policies");
        e.Property(x => x.Provider).HasMaxLength(200).IsRequired();
        e.Property(x => x.PolicyNumber).HasMaxLength(100).IsRequired();
        e.Property(x => x.CoverageSummary).HasMaxLength(2000);
        e.Property(x => x.CoverageLimit).HasPrecision(12, 2);
        e.Property(x => x.Premium).HasPrecision(12, 2);
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PetHealthShareLinkConfiguration : IEntityTypeConfiguration<PetHealthShareLink>
{
    public void Configure(EntityTypeBuilder<PetHealthShareLink> e)
    {
        e.ToTable("pet_health_share_links");
        e.Property(x => x.Token).HasMaxLength(64).IsRequired();
        e.Property(x => x.Label).HasMaxLength(128);
        e.HasIndex(x => x.Token).IsUnique();
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PetOwnershipTransferConfiguration : IEntityTypeConfiguration<PetOwnershipTransfer>
{
    public void Configure(EntityTypeBuilder<PetOwnershipTransfer> e)
    {
        e.ToTable("pet_ownership_transfers");
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.PetId);
    }
}

public class PetGroupConfiguration : IEntityTypeConfiguration<PetGroup>
{
    public void Configure(EntityTypeBuilder<PetGroup> e)
    {
        e.ToTable("pet_groups");
        e.Property(x => x.Name).HasMaxLength(128).IsRequired();
        e.Property(x => x.Purpose).HasMaxLength(128);
        e.Property(x => x.Notes).HasMaxLength(1000);
        e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.OwnerId);
    }
}

public class ProductionLogConfiguration : IEntityTypeConfiguration<ProductionLog>
{
    public void Configure(EntityTypeBuilder<ProductionLog> e)
    {
        e.ToTable("production_logs");
        e.Property(x => x.Quantity).HasPrecision(12, 2);
        e.Property(x => x.Unit).HasMaxLength(32).IsRequired();
        e.Property(x => x.Notes).HasMaxLength(500);
        e.HasOne(x => x.PetGroup).WithMany().HasForeignKey(x => x.PetGroupId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.OwnerId, x.RecordedOn });
    }
}

public class BreedingRecordConfiguration : IEntityTypeConfiguration<BreedingRecord>
{
    public void Configure(EntityTypeBuilder<BreedingRecord> e)
    {
        e.ToTable("breeding_records");
        e.Property(x => x.SireDescription).HasMaxLength(256);
        e.Property(x => x.Notes).HasMaxLength(1000);
        e.HasOne(x => x.Dam).WithMany().HasForeignKey(x => x.DamId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Sire).WithMany().HasForeignKey(x => x.SireId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => x.OwnerId);
    }
}

public class LostFoundReportConfiguration : IEntityTypeConfiguration<LostFoundReport>
{
    public void Configure(EntityTypeBuilder<LostFoundReport> e)
    {
        e.ToTable("lost_found_reports");
        e.Property(x => x.Breed).HasMaxLength(128);
        e.Property(x => x.Color).HasMaxLength(64);
        e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        e.Property(x => x.City).HasMaxLength(128).IsRequired();
        e.Property(x => x.Area).HasMaxLength(128);
        e.Property(x => x.ContactPhone).HasMaxLength(32);
        e.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.Status, x.Kind, x.City });
        e.HasIndex(x => x.CreatedAt);
    }
}
