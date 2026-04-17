namespace PatientAccess.Data.Entities;

public class Admin
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Bitmask of administrative privileges (e.g., user management, system configuration).
    /// Interpreted by the application layer; stored as int. DR-010.
    /// </summary>
    public int AccessPrivileges { get; set; }

    /// <summary>
    /// PBKDF2-HMAC-SHA512 password hash produced by ASP.NET Core Identity
    /// IPasswordHasher<Admin>. Never stores raw passwords. DR-010.
    /// </summary>
    public string AuthCredentials { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
