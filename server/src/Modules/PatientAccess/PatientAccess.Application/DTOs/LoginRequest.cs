namespace PatientAccess.Application.DTOs;

/// <summary>
/// Credential payload for <c>POST /api/v1/auth/login</c>.
/// Staff and Admin supply their <c>Username</c> in the <c>Email</c> field.
/// Patients supply their registered email address.
/// </summary>
public record LoginRequest(string Email, string Password, bool RememberMe = false);
