using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class CodeSuggestionConfiguration : IEntityTypeConfiguration<CodeSuggestion>
{
    public void Configure(EntityTypeBuilder<CodeSuggestion> builder)
    {
        builder.ToTable("code_suggestion");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // Store code type as varchar — ICD-10 or CPT per DR-007
        builder.Property(c => c.CodeType)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(c => c.CodeValue)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.ConfidenceScore)
            .HasColumnType("real")
            .IsRequired();

        // PostgreSQL uuid[] array: denormalised evidence fact references per DR-007
        builder.Property(c => c.EvidenceFactIds)
            .HasColumnType("uuid[]")
            .IsRequired();

        builder.Property(c => c.StaffReviewed)
            .HasDefaultValue(false)
            .IsRequired();

        // ReviewOutcome: nullable ("accepted" | "rejected")
        builder.Property(c => c.ReviewOutcome)
            .HasMaxLength(50)
            .IsRequired(false);

        // ReviewJustification: required when ReviewOutcome = "rejected" (UC-005)
        builder.Property(c => c.ReviewJustification)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();

        builder.Property(c => c.ReviewedAt)
            .IsRequired(false);

        builder.Property(c => c.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        // FK: patient_id → patient(id); Restrict prevents physical delete with active suggestions
        builder.HasOne(c => c.Patient)
            .WithMany(p => p.CodeSuggestions)
            .HasForeignKey(c => c.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Supporting index for patient-based suggestion lookup
        builder.HasIndex(c => c.PatientId)
            .HasDatabaseName("ix_code_suggestion_patient_id");

        // AC-2: partial index optimises the 422-gate "unreviewed codes" query (US_023).
        // Covers only active, unreviewed rows — keeps the index tiny and avoids heap bloat.
        builder.HasIndex(c => new { c.PatientId, c.StaffReviewed })
            .HasDatabaseName("ix_code_suggestion_staff_reviewed")
            .HasFilter("is_deleted = false AND staff_reviewed = false");
    }
}
