using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Converters;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class ClinicalDocumentConfiguration : IEntityTypeConfiguration<ClinicalDocument>
{
    private readonly PhiEncryptedConverter? _enc;

    /// <summary>EF tooling constructor — no encryption (schema-only).</summary>
    public ClinicalDocumentConfiguration() { }

    /// <summary>Runtime constructor — encrypts DR-015 PHI columns at rest (AC-1).</summary>
    public ClinicalDocumentConfiguration(PhiEncryptedConverter? enc) => _enc = enc;

    public void Configure(EntityTypeBuilder<ClinicalDocument> builder)
    {
        builder.ToTable("clinical_document");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // PHI column: blob URL or file system path per DR-004.
        // text type (no MaxLength) accommodates variable-length AES ciphertext (DR-015).
        var fileRefProp = builder.Property(d => d.FileReference)
            .IsRequired()
            .HasColumnType("text");

        if (_enc is not null)
            fileRefProp.HasConversion(_enc);

        // Original filename - display use only; sanitised before storage (OWASP A01)
        builder.Property(d => d.OriginalFileName)
            .IsRequired()
            .HasMaxLength(512);

        // File size in bytes - stored as bigint to support multi-GB files in future
        builder.Property(d => d.FileSizeBytes)
            .IsRequired();

        // Store extraction status as varchar - safe to add new states without ALTER TYPE
        builder.Property(d => d.ExtractionStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.UploadedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();

        builder.Property(d => d.UpdatedAt)
            .IsRequired();

        // ProcessedAt is null until extraction completes or fails
        builder.Property(d => d.ProcessedAt)
            .IsRequired(false);

        // Soft-delete - physical file retained for audit per DR-013
        builder.Property(d => d.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Timestamp of soft-deletion; null while the document is active (DR-017)
        builder.Property(d => d.DeletedAt)
            .IsRequired(false);

        builder.HasQueryFilter(d => !d.IsDeleted);

        // Nullable FK: encounter_id
        builder.Property(d => d.EncounterId)
            .IsRequired(false);
        builder.HasIndex(d => d.EncounterId)
            .HasDatabaseName("ix_clinical_document_encounter_id")
            .IsUnique(false);

        // Patient ownership index - needed for the ownership-filtered GET query
        builder.HasIndex(d => d.PatientId)
            .HasDatabaseName("ix_clinical_document_patient_id");

        // FK: patient_id to patient(id)
        builder.HasOne(d => d.Patient)
            .WithMany(p => p.ClinicalDocuments)
            .HasForeignKey(d => d.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // One-to-many relationship configured on the dependent side in ExtractedFactConfiguration
    }
}
