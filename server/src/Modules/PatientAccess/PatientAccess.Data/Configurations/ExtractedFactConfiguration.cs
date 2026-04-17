using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class ExtractedFactConfiguration : IEntityTypeConfiguration<ExtractedFact>
{
    public void Configure(EntityTypeBuilder<ExtractedFact> builder)
    {
        builder.ToTable("extracted_fact");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // Store fact type as varchar — consistent with other enum columns, avoids ALTER TYPE
        builder.Property(f => f.FactType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // PHI column: extracted clinical value per DR-015
        builder.Property(f => f.FactText)
            .IsRequired()
            .HasMaxLength(2000);

        // confidence_score: PostgreSQL real (4-byte float) — sufficient precision for 0.0–1.0
        builder.Property(f => f.ConfidenceScore)
            .HasColumnType("real")
            .IsRequired();

        // Source citation offsets for character-level tracing per AIR-006
        builder.Property(f => f.SourceCharOffset)
            .HasColumnType("integer")
            .IsRequired();
        builder.Property(f => f.SourceCharLength)
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(f => f.ExtractedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();

        // FK: document_id → clinical_document(id)
        // Cascade delete: facts are derived data; deleting a document atomically removes its facts (DR-011)
        builder.HasOne(f => f.Document)
            .WithMany(d => d.ExtractedFacts)
            .HasForeignKey(f => f.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Supporting index for fast fact lookup by document (common query pattern)
        builder.HasIndex(f => f.DocumentId)
            .HasDatabaseName("ix_extracted_fact_document_id");
    }
}
