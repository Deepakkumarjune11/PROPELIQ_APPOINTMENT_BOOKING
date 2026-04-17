using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Entities;

public class Staff
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public StaffRole Role { get; set; }

    /// <summary>
    /// Bitmask of granted permissions. Each bit position maps to a named permission constant
    /// defined in the application layer. Max value = 2^31 - 1 (31 permission flags).
    /// API layer validates combinations before persistence. DR-009.
    /// </summary>
    public int PermissionsBitfield { get; set; }

    /// <summary>
    /// PBKDF2-HMAC-SHA512 password hash produced by ASP.NET Core Identity
    /// IPasswordHasher<Staff>. Never stores raw passwords. DR-009.
    /// </summary>
    public string AuthCredentials { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
