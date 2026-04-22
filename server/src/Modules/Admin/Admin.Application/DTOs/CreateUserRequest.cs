using PatientAccess.Domain.Enums;

namespace Admin.Application.DTOs;

/// <summary>
/// Request payload for <c>POST /api/v1/admin/users</c>.
/// <c>StaffRole</c> is required when <c>Role == "Staff"</c>; ignored for Admin.
/// </summary>
public record CreateUserRequest(
    string     Name,
    string     Email,
    string     Role,
    StaffRole? StaffRole            = null,
    int        PermissionsBitfield  = 0,
    string?    Department           = null);
