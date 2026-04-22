using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Converters;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class ExtractedFactConfiguration : IEntityTypeConfiguration<ExtractedFact>
{
    private readonly PhiEncryptedConverter? _enc;

    /// <summary>EF tooling constructor — no encryption (schema-only).</summary>
    public ExtractedFactConfiguration() { }

    /// <summary>Runtime constructor — encrypts DR-015 PHI columns at rest (AC-1).</summary>
    public ExtractedFactConfiguration(PhiEncryptedConverter? enc) => _enc = enc;

    public void Configure(EntityTypeBuilder<ExtractedFact> builder)
    {
        builder.ToTable("extracted_fact");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // Soft-delete filter: active queries never see deleted facts (DR-017)
        builder.HasQueryFilter(f => !f.IsDeleted);

        // Store fact type as varchar — consistent with other enum columns, avoids ALTER TYPE
        builder.Property(f => f.FactType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // PHI column: ciphertext from .NET Data Protection API (DR-015).
        // Column type is text (unbounded) — base64-encoded ciphertext length is unpredictable.
        builder.Property(f => f.FactText)
            .HasColumnType("text")
            .IsRequired();
        if (_enc is not null)
            builder.Property(f => f.FactText).HasConversion(_enc);

        // confidence_score: PostgreSQL real (4-byte float) — sufficient precision for 0.0–1.0
        builder.Property(f => f.ConfidenceScore)
            .HasColumnType("real")
            .IsRequired();

        // Source citation offsets for character-level tracing per AIR-006.
        // Nullable: a fact without a matched source position is valid if citation was not resolved.
        builder.Property(f => f.SourceCharOffset)
            .HasColumnType("integer")
            .IsRequired(false);

        builder.Property(f => f.SourceCharLength)
            .HasColumnType("integer")
            .IsRequired(false);

        builder.Property(f => f.ExtractedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();

        // Soft-delete columns (DR-017)
        builder.Property(f => f.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(f => f.DeletedAt)
            .IsRequired(false);

        // FK: document_id → clinical_document(id)
        // Restrict: facts are retained for audit trail even if the parent document is soft-deleted (DR-012)
        builder.HasOne(f => f.Document)
            .WithMany(d => d.ExtractedFacts)
            .HasForeignKey(f => f.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Supporting index for fast fact lookup by document (common query pattern)
        builder.HasIndex(f => f.DocumentId)
            .HasDatabaseName("ix_extracted_fact_document_id");
    }
}
