namespace Admin.Application.DTOs;

/// <summary>
/// Request payload for <c>PUT /api/v1/admin/users/{id}</c>.
/// <c>Password</c> is optional — if null or empty the existing password is preserved.
/// <c>Email</c> is used for Patient entities (Staff/Admin use Username as email).
/// <c>Role</c> is informational; the underlying entity type cannot be changed via update.
/// </summary>
public record UpdateUserRequest(string Name, string? Email = null, string? Role = null, string? Password = null, string? Department = null);
