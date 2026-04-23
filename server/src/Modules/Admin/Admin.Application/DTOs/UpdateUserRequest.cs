namespace Admin.Application.DTOs;

/// <summary>
/// Request payload for <c>PUT /api/v1/admin/users/{id}</c>.
/// <c>Password</c> is optional — if null or empty the existing password is preserved.
/// </summary>
public record UpdateUserRequest(string Name, string? Password = null, string? Department = null);
