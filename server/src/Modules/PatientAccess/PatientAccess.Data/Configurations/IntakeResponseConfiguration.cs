using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Converters;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class IntakeResponseConfiguration : IEntityTypeConfiguration<IntakeResponse>
{
    private readonly PhiEncryptedConverter? _enc;

    /// <summary>EF tooling constructor — no encryption (schema-only).</summary>
    public IntakeResponseConfiguration() { }

    /// <summary>
    /// Runtime constructor — <paramref name="enc"/> enables PHI encryption at rest (DR-015).
    /// </summary>
    public IntakeResponseConfiguration(PhiEncryptedConverter? enc) => _enc = enc;

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

        // PHI column: changed from jsonb to text to store variable-length AES ciphertext (DR-015, AC-1).
        // Ciphertext is raw base64 — not valid JSON — so jsonb is not suitable after encryption.
        // When _enc is null (EF tooling), no conversion is applied and schema is inspected as-is.
        var answersProperty = builder.Property(i => i.Answers)
            .HasColumnType("text")
            .IsRequired();

        if (_enc is not null)
            answersProperty.HasConversion(_enc);

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
