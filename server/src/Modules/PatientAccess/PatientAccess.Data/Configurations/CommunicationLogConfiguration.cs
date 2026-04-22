using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

/// <summary>
/// EF Core fluent configuration for the <c>communication_log</c> table.
/// PdfBytes is mapped to PostgreSQL <c>bytea</c>; Channel/Status are stored as varchar strings.
/// FK Restrict so deleting a patient/appointment does not cascade-delete audit records (DR-012).
/// </summary>
public sealed class CommunicationLogConfiguration : IEntityTypeConfiguration<CommunicationLog>
{
    public void Configure(EntityTypeBuilder<CommunicationLog> builder)
    {
        builder.ToTable("communication_log");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
               .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Channel)
               .HasConversion<string>()
               .HasMaxLength(10)
               .IsRequired();

        builder.Property(x => x.Status)
               .HasConversion<string>()
               .HasMaxLength(10)
               .IsRequired();

        builder.Property(x => x.AttemptCount)
               .IsRequired()
               .HasDefaultValue(1);

        // nullable bytea column — only populated on email confirmations
        builder.Property(x => x.PdfBytes)
               .HasColumnType("bytea")
               .IsRequired(false);

        builder.Property(x => x.CreatedAt)
               .HasDefaultValueSql("now()")
               .IsRequired();

        // FK → Patient: Restrict so audit records survive patient soft-deletion (DR-012)
        builder.HasOne(x => x.Patient)
               .WithMany()
               .HasForeignKey(x => x.PatientId)
               .OnDelete(DeleteBehavior.Restrict);

        // FK → Appointment: Restrict for same reason
        builder.HasOne(x => x.Appointment)
               .WithMany()
               .HasForeignKey(x => x.AppointmentId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
