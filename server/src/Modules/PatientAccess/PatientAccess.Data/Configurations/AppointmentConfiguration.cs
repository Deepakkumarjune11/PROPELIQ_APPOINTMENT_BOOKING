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

        // Self-referencing FK for preferred slot swap watchlist (DR-002)
        // SetNull: when a preferred slot is deleted, preferred_slot_id becomes null
        builder.HasOne(a => a.PreferredSlot)
            .WithMany()
            .HasForeignKey(a => a.PreferredSlotId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
