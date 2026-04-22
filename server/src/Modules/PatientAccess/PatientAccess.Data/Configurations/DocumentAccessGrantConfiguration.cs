using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class DocumentAccessGrantConfiguration : IEntityTypeConfiguration<DocumentAccessGrant>
{
    public void Configure(EntityTypeBuilder<DocumentAccessGrant> builder)
    {
        builder.ToTable("document_access_grants");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(g => g.DocumentId)
            .HasColumnName("document_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(g => g.GranteeId)
            .HasColumnName("grantee_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(g => g.GranteeType)
            .HasColumnName("grantee_type")
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(g => g.GrantedAt)
            .HasColumnName("granted_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("NOW()");

        // DB-level check constraint — defense-in-depth against invalid grantee_type values (OWASP A03)
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_document_access_grants_grantee_type",
            "grantee_type IN ('staff', 'dept')"));

        // FK to clinical_documents — cross-module reference; no EF navigation property
        // to avoid cross-context coupling. Enforced as raw FK in migration SQL (CreateTable).
        // HasIndex calls omitted — indexes created CONCURRENTLY in migration SQL (suppressTransaction: true).
    }
}
