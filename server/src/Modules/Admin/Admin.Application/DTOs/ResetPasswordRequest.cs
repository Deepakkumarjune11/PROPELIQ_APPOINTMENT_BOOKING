namespace Admin.Application.DTOs;

/// <summary>
/// Request payload for <c>PATCH /api/v1/admin/users/{id}/reset-password</c>.
/// </summary>
public record ResetPasswordRequest(string NewPassword);
