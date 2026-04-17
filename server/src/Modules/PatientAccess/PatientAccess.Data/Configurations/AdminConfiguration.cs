using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Configurations;

internal sealed class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    public void Configure(EntityTypeBuilder<Admin> builder)
    {
        builder.ToTable("admin");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.Username)
            .IsRequired()
            .HasMaxLength(100);
        builder.HasIndex(a => a.Username)
            .IsUnique()
            .HasDatabaseName("uix_admin_username");

        builder.Property(a => a.AccessPrivileges)
            .HasColumnType("integer")
            .IsRequired();

        // PBKDF2-HMAC-SHA512 hash from ASP.NET Core Identity; 256 chars for format headroom
        builder.Property(a => a.AuthCredentials)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.IsActive)
            .HasDefaultValue(true)
            .IsRequired();
    }
}
