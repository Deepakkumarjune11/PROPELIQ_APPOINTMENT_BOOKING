using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class ClinicalDocumentConfiguration : IEntityTypeConfiguration<ClinicalDocument>
{
    public void Configure(EntityTypeBuilder<ClinicalDocument> builder)
    {
        builder.ToTable("clinical_document");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // PHI column: blob URL or file system path per DR-004; encrypted at rest per DR-015
        builder.Property(d => d.FileReference)
            .IsRequired()
            .HasMaxLength(2048);

        // Store extraction status as varchar — safe to add new states without ALTER TYPE
        builder.Property(d => d.ExtractionStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.UploadedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();

        // ProcessedAt is null until extraction completes or fails
        builder.Property(d => d.ProcessedAt)
            .IsRequired(false);

        // Nullable FK: encounter_id — null for pre-visit historical documents (DR-004)
        // SetNull behaviour is enforced at the application layer; no explicit FK to
        // appointment is wired here because Appointment does not carry a ClinicalDocuments
        // inverse navigation — EF resolves the shadow FK by convention from Guid? EncounterId.
        builder.Property(d => d.EncounterId)
            .IsRequired(false);
        builder.HasIndex(d => d.EncounterId)
            .HasDatabaseName("ix_clinical_document_encounter_id")
            .IsUnique(false);

        // FK: patient_id → patient(id)
        builder.HasOne(d => d.Patient)
            .WithMany(p => p.ClinicalDocuments)
            .HasForeignKey(d => d.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // One-to-many relationship configured on the dependent side in ExtractedFactConfiguration
    }
}
