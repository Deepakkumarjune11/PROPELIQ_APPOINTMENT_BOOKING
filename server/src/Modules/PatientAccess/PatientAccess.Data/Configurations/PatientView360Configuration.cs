using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Converters;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class PatientView360Configuration : IEntityTypeConfiguration<PatientView360>
{
    private readonly PhiEncryptedConverter? _enc;

    /// <summary>EF tooling constructor — no encryption (schema-only).</summary>
    public PatientView360Configuration() { }

    /// <summary>Runtime constructor — encrypts DR-015 PHI columns at rest (AC-1).</summary>
    public PatientView360Configuration(PhiEncryptedConverter? enc) => _enc = enc;

    public void Configure(EntityTypeBuilder<PatientView360> builder)
    {
        builder.ToTable("patient_view_360");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // PHI column: JSONB consolidated clinical summary per DR-015
        // Stored as text (ciphertext from .NET Data Protection API) — NOT jsonb.
        // Storing as jsonb would cause PostgreSQL to reject encrypted ciphertext as invalid JSON.
        builder.Property(v => v.ConsolidatedFacts)
            .HasColumnType("text")
            .IsRequired();
        if (_enc is not null)
            builder.Property(v => v.ConsolidatedFacts).HasConversion(_enc);

        // PostgreSQL text[] array: conflict flag descriptions from AIR-004
        builder.Property(v => v.ConflictFlags)
            .HasColumnType("text[]")
            .IsRequired();

        // Store verification status as varchar — avoids ALTER TYPE on enum extension
        builder.Property(v => v.VerificationStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(v => v.LastUpdated)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAddOrUpdate();

        // Optimistic concurrency token per DR-018.
        // EF Core appends "AND version = @p" to every UPDATE statement.
        // DbUpdateConcurrencyException is raised when 0 rows match, signalling
        // a concurrent modification; application layer maps to HTTP 409.
        builder.Property(v => v.Version)
            .HasColumnType("integer")
            .IsConcurrencyToken()
            .HasDefaultValue(0);

        // One-to-one: Patient → PatientView360
        // NoAction: PatientView360 is preserved when patient is soft-deleted (DR-017).
        // Physical patient delete is blocked upstream by Appointment FK Restrict.
        builder.HasOne(v => v.Patient)
            .WithOne(p => p.PatientView360)
            .HasForeignKey<PatientView360>(v => v.PatientId)
            .OnDelete(DeleteBehavior.NoAction);

        // Unique index: one consolidated view per patient
        builder.HasIndex(v => v.PatientId)
            .IsUnique()
            .HasDatabaseName("uix_patient_view_360_patient_id");
    }
}
