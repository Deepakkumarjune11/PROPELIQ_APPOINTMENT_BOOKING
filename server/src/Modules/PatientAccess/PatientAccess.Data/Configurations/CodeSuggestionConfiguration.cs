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

        // PostgreSQL uuid[] array: denormalised evidence fact references per DR-007
        builder.Property(c => c.EvidenceFactIds)
            .HasColumnType("uuid[]")
            .IsRequired();

        builder.Property(c => c.StaffReviewed)
            .HasDefaultValue(false)
            .IsRequired();

        // ReviewOutcome: nullable free-text (e.g., "Accepted", "Rejected", "Modified")
        builder.Property(c => c.ReviewOutcome)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();

        // ReviewedAt: null until StaffReviewed transitions to true
        builder.Property(c => c.ReviewedAt)
            .IsRequired(false);

        // FK: patient_id → patient(id); Restrict prevents physical delete with active suggestions
        builder.HasOne(c => c.Patient)
            .WithMany(p => p.CodeSuggestions)
            .HasForeignKey(c => c.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Supporting index for patient-based suggestion lookup
        builder.HasIndex(c => c.PatientId)
            .HasDatabaseName("ix_code_suggestion_patient_id");
    }
}
