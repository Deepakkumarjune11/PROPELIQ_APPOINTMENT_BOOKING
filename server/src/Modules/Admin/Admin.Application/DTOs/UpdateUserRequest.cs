namespace Admin.Application.DTOs;

/// <summary>
/// Request payload for <c>PUT /api/v1/admin/users/{id}</c>.
/// Password is intentionally excluded — credential updates require a separate flow.
/// </summary>
public record UpdateUserRequest(string Name, string? Department = null);
