using PatientAccess.Domain.Enums;

namespace Admin.Application.DTOs;

/// <summary>
/// Request payload for <c>PATCH /api/v1/admin/users/{id}/role</c>.
/// </summary>
public record AssignRoleRequest(StaffRole StaffRole, int PermissionsBitfield);
