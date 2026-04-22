using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class DocumentChunkEmbeddingConfiguration
    : IEntityTypeConfiguration<DocumentChunkEmbedding>
{
    public void Configure(EntityTypeBuilder<DocumentChunkEmbedding> builder)
    {
        builder.ToTable("document_chunk_embeddings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.DocumentId)
            .HasColumnName("document_id")
            .IsRequired();

        builder.Property(e => e.ChunkIndex)
            .HasColumnName("chunk_index")
            .IsRequired();

        // Full text stored as PostgreSQL TEXT — no length cap per chunk design (AIR-R01)
        builder.Property(e => e.ChunkText)
            .HasColumnName("chunk_text")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.TokenCount)
            .HasColumnName("token_count")
            .IsRequired();

        // pgvector 1536-dim column — nullable while staging (null until EmbeddingGenerationJob fills it)
        builder.Property(e => e.Embedding)
            .HasColumnName("embedding")
            .HasColumnType("vector(1536)")
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // FK to clinical_document — cascade delete removes embeddings when document is deleted (DR-013)
        builder.HasOne(e => e.Document)
            .WithMany()
            .HasForeignKey(e => e.DocumentId)
            .HasConstraintName("fk_dce_document_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Composite unique constraint prevents duplicate chunks on document re-processing
        builder.HasIndex(e => new { e.DocumentId, e.ChunkIndex })
            .IsUnique()
            .HasDatabaseName("ix_dce_document_chunk_unique");

        // Standard index on document_id — used by embedding job retrieval query
        builder.HasIndex(e => e.DocumentId)
            .HasDatabaseName("ix_dce_document_id");
    }
}
