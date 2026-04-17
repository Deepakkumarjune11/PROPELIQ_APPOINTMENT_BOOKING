using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("staff");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.Username)
            .IsRequired()
            .HasMaxLength(100);
        builder.HasIndex(s => s.Username)
            .IsUnique()
            .HasDatabaseName("uix_staff_username");

        // Store role as varchar — avoids ALTER TYPE cost when extending StaffRole enum
        builder.Property(s => s.Role)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.PermissionsBitfield)
            .HasColumnType("integer")
            .IsRequired();

        // PBKDF2-HMAC-SHA512 hash from ASP.NET Core Identity (format: version byte + salt + hash, base64)
        // Identity v3 hashes are ~84 chars base64; 256 provides headroom for future format versions
        builder.Property(s => s.AuthCredentials)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();

        builder.Property(s => s.IsActive)
            .HasDefaultValue(true)
            .IsRequired();
    }
}
