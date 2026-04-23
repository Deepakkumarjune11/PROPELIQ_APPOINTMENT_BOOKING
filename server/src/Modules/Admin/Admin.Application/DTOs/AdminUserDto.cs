namespace Admin.Application.DTOs;

/// <summary>
/// Projection of a Staff or Admin entity returned to the admin UI.
/// Deliberately omits <c>AuthCredentials</c> to prevent credential leakage (OWASP A02).
/// </summary>
public record AdminUserDto(
    Guid    Id,
    string  Name,
    string  Email,
    string  Role,
    bool    IsActive,
    int     PermissionsBitfield,
    string? Department   = null);
