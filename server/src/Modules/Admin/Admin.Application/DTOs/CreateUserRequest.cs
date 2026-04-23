using PatientAccess.Domain.Enums;

namespace Admin.Application.DTOs;

/// <summary>
/// Request payload for <c>POST /api/v1/admin/users</c>.
/// <c>StaffRole</c> is required when <c>Role == "Staff"</c>; ignored for Admin.
/// <c>Password</c> is required — the admin sets it at creation time and shares it with the user.
/// </summary>
public record CreateUserRequest(
    string     Name,
    string     Email,
    string     Role,
    string     Password,
    StaffRole? StaffRole            = null,
    int        PermissionsBitfield  = 0,
    string?    Department           = null);
