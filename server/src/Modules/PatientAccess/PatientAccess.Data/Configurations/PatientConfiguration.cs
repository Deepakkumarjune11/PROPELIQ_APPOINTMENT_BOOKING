using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Converters;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    private readonly PhiEncryptedConverter? _enc;
    private readonly PhiEncryptedNullableConverter? _encNullable;

    /// <summary>EF tooling constructor — no encryption (schema-only).</summary>
    public PatientConfiguration() { }

    /// <summary>Runtime constructor — encrypts DR-015 PHI columns at rest (AC-1, NFR-003).</summary>
    public PatientConfiguration(PhiEncryptedConverter? enc, PhiEncryptedNullableConverter? encNullable)
        => (_enc, _encNullable) = (enc, encNullable);

    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("patient");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // Email is NOT encrypted — it is the primary login identifier used in WHERE equality
        // queries and has a unique index (uix_patient_email). Encrypting would break auth (DR-015 edge case).
        builder.Property(p => p.Email)
            .IsRequired()
            .HasMaxLength(320);
        builder.HasIndex(p => p.Email)
            .IsUnique()
            .HasDatabaseName("uix_patient_email");

        // PHI columns: widened to text (no MaxLength) to accommodate variable-length ciphertext.
        builder.Property(p => p.Name).IsRequired().HasColumnType("text");
        if (_enc is not null)
            builder.Property(p => p.Name).HasConversion(_enc);

        builder.Property(p => p.Phone).IsRequired().HasColumnType("text");
        if (_enc is not null)
            builder.Property(p => p.Phone).HasConversion(_enc);

        builder.Property(p => p.InsuranceProvider).HasColumnType("text");
        if (_encNullable is not null)
            builder.Property(p => p.InsuranceProvider).HasConversion(_encNullable);

        builder.Property(p => p.InsuranceMemberId).HasColumnType("text");
        if (_encNullable is not null)
            builder.Property(p => p.InsuranceMemberId).HasConversion(_encNullable);

        builder.Property(p => p.InsuranceStatus).HasMaxLength(50);

        // Administrative metadata: NOT PHI — no encryption, VARCHAR(100) column type constraint only
        builder.Property(p => p.Department)
            .HasColumnName("department")
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        // Auth credentials: hashed password — nullable (null until patient sets a password).
        // NOT PHI and NOT encrypted at-rest (it is already a one-way hash per OWASP A02).
        builder.Property(p => p.AuthCredentials)
            .HasColumnName("auth_credentials")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();
        builder.Property(p => p.UpdatedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAddOrUpdate();

        // Soft delete: excludes IsDeleted=true records from all default queries (DR-017)
        builder.HasQueryFilter(p => !p.IsDeleted);

        // One-to-many: Patient → Appointments is configured in AppointmentConfiguration
        // to co-locate the optional FK and IsRequired(false) settings (avoids shadow PatientId1).
    }
}
