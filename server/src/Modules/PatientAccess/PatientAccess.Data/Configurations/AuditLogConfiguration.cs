using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_log");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // Scalar Guid — no FK constraint to Staff/Admin; actor may be System (Guid.Empty)
        builder.Property(a => a.ActorId)
            .IsRequired();

        builder.Property(a => a.ActorType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.ActionType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.TargetEntityId)
            .IsRequired();

        builder.Property(a => a.TargetEntityType)
            .HasColumnName("target_entity_type")
            .HasMaxLength(100)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        // JSONB payload: before/after state or event context.
        // Must not contain unredacted PII per AIR-S03 / NFR-007.
        builder.Property(a => a.Details)
            .HasColumnType("jsonb");

        builder.Property(a => a.OccurredAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();

        // ── Compliance columns added in US_026 ───────────────────────────────

        builder.Property(a => a.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);          // IPv6 max 39 + mapped IPv4 prefix = 45

        builder.Property(a => a.OldValues)
            .HasColumnName("old_values")
            .HasColumnType("jsonb");

        builder.Property(a => a.NewValues)
            .HasColumnName("new_values")
            .HasColumnType("jsonb");

        builder.Property(a => a.PreviousHash)
            .HasColumnName("previous_hash")
            .HasColumnType("text");

        builder.Property(a => a.ChainHash)
            .HasColumnName("chain_hash")
            .HasColumnType("text")
            .IsRequired()
            .HasDefaultValue(string.Empty);

        // No HasQueryFilter — audit records must never be excluded from queries (DR-008)
        // No soft-delete — AuditLog is immutable append-only; DR-012 retains indefinitely

        // HIPAA audit retrieval indexes — DR-008
        builder.HasIndex(a => a.ActorId)
            .HasDatabaseName("ix_audit_log_actor_id");
        builder.HasIndex(a => a.TargetEntityId)
            .HasDatabaseName("ix_audit_log_target");
        builder.HasIndex(a => a.OccurredAt)
            .HasDatabaseName("ix_audit_log_occurred_at");
    }
}
