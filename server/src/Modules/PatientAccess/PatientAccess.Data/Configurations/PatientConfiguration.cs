using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("patient");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.Email)
            .IsRequired()
            .HasMaxLength(320);
        builder.HasIndex(p => p.Email)
            .IsUnique()
            .HasDatabaseName("uix_patient_email");

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Phone).IsRequired().HasMaxLength(30);
        builder.Property(p => p.InsuranceProvider).HasMaxLength(200);
        builder.Property(p => p.InsuranceMemberId).HasMaxLength(100);
        builder.Property(p => p.InsuranceStatus).HasMaxLength(50);

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();
        builder.Property(p => p.UpdatedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAddOrUpdate();

        // Soft delete: excludes IsDeleted=true records from all default queries (DR-017)
        builder.HasQueryFilter(p => !p.IsDeleted);

        // One-to-many: Patient → Appointments
        // OnDelete(Restrict): prevent physical delete of patient with active appointments
        builder.HasMany(p => p.Appointments)
            .WithOne(a => a.Patient)
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
