using Admin.Application.DTOs;

namespace Admin.Application.Services;

/// <summary>
/// Admin user lifecycle operations: list, create, update, assign role, disable/enable, reset password.
/// All mutating operations write an immutable AuditLog entry (FR-016).
/// </summary>
public interface IUserManagementService
{
    Task<IReadOnlyList<AdminUserDto>> GetAllUsersAsync(CancellationToken ct);

    Task<AdminUserDto> CreateUserAsync(CreateUserRequest request, Guid actorId, CancellationToken ct);

    Task<AdminUserDto> UpdateUserAsync(Guid userId, UpdateUserRequest request, Guid actorId, CancellationToken ct);

    Task AssignRoleAsync(Guid userId, AssignRoleRequest request, Guid actorId, CancellationToken ct);

    Task ResetPasswordAsync(Guid userId, ResetPasswordRequest request, Guid actorId, CancellationToken ct);

    Task DisableUserAsync(Guid userId, Guid actorId, CancellationToken ct);

    Task EnableUserAsync(Guid userId, Guid actorId, CancellationToken ct);
}
