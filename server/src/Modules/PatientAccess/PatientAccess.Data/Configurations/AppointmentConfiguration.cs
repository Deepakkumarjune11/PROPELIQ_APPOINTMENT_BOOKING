using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointment");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // Store status as varchar — human-readable in SQL, no migration cost when adding new values
        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.SlotDatetime)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Risk score 0.0000–1.0000 (e.g., 0.7823)
        builder.Property(a => a.NoShowRiskScore)
            .HasPrecision(5, 4);

        builder.Property(a => a.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();
        builder.Property(a => a.UpdatedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAddOrUpdate();

        // Soft delete: excludes IsDeleted=true records from all default queries (DR-017)
        builder.HasQueryFilter(a => !a.IsDeleted);

        // Composite partial index — supports the availability range query (AC-1, NFR-001 p95 ≤ 2s).
        // Partial filter (is_deleted = false) keeps the index lean by excluding soft-deleted rows.
        // Column order: slot_datetime first (range predicate), then status (equality predicate).
        builder.HasIndex(a => new { a.SlotDatetime, a.Status })
               .HasFilter("\"IsDeleted\" = false")
               .HasDatabaseName("ix_appointments_slot_datetime_status_active");

        // Optimistic concurrency using PostgreSQL system column xmin (AC-4, US_013/task_003).
        // Maps the xmin system column (type xid → uint) as a shadow property.
        // EF Core includes xmin in the WHERE clause of UPDATE statements.
        // A concurrent write raises DbUpdateConcurrencyException → converted to SlotAlreadyBookedException → 409.
        // No DDL change required — xmin is a PostgreSQL system column automatically maintained.
        builder.Property<uint>("xmin")
               .HasColumnType("xid")
               .IsRowVersion();

        // Self-referencing FK for preferred slot swap watchlist (DR-002)
        // SetNull: when a preferred slot is deleted, preferred_slot_id becomes null
        builder.HasOne(a => a.PreferredSlot)
            .WithMany()
            .HasForeignKey(a => a.PreferredSlotId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Partial index — limits watchlist polling to only appointments on the watchlist.
        // Replaces the plain auto-generated FK index with a filtered, named variant.
        // The actual CREATE INDEX will use CONCURRENTLY in the migration (NFR-012).
        builder.HasIndex(a => a.PreferredSlotId)
            .HasFilter("\"PreferredSlotId\" IS NOT NULL")
            .HasDatabaseName("ix_appointments_preferred_slot_id");

        // Walk-in column defaults (US_016, AC-4 / AC-5).
        builder.Property(a => a.IsWalkIn)
            .HasDefaultValue(false)
            .IsRequired();

        // Partial index for today's ordered queue reads (US_016, NFR-012).
        // Covers: SELECT ... WHERE QueuePosition IS NOT NULL ORDER BY QueuePosition
        // Actual CREATE INDEX uses CONCURRENTLY in the migration for zero-downtime (NFR-012).
        builder.HasIndex(a => a.QueuePosition)
            .HasFilter("\"QueuePosition\" IS NOT NULL")
            .HasDatabaseName("ix_appointments_queue_position");
    }
}
