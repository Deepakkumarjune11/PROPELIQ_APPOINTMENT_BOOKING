using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class IntakeResponseConfiguration : IEntityTypeConfiguration<IntakeResponse>
{
    public void Configure(EntityTypeBuilder<IntakeResponse> builder)
    {
        builder.ToTable("intake_response");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // Store mode as varchar — avoids ALTER TYPE cost when extending the enum
        builder.Property(i => i.Mode)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // PHI column: JSONB type per DR-013.
        // Application-layer ValueConverter applies .NET Data Protection API
        // encryption before write and decryption after read per DR-015.
        builder.Property(i => i.Answers)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();

        // FK: patient_id → patient(id); Restrict prevents orphan intake responses
        builder.HasOne(i => i.Patient)
            .WithMany(p => p.IntakeResponses)
            .HasForeignKey(i => i.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
