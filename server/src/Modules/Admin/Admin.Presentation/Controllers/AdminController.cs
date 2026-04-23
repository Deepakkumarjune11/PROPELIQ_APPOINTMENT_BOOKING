using System.Security.Claims;
using Admin.Application.DTOs;
using Admin.Application.Exceptions;
using Admin.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Presentation.Controllers;

/// <summary>
/// User lifecycle management endpoints.
/// All operations require the <c>Admin</c> role; every mutating action is
/// correlated to the calling admin via the NameIdentifier JWT claim.
/// Self-disable is forbidden to prevent admin lockout (UC-006 extension 3a).
/// </summary>
[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Roles = "Admin")]
public sealed class AdminController(IUserManagementService userService) : ControllerBase
{
    private Guid ActorId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── GET /api/v1/admin/users ──────────────────────────────────────────────

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var users = await userService.GetAllUsersAsync(ct);
        return Ok(users);
    }

    // ── POST /api/v1/admin/users ─────────────────────────────────────────────

    [HttpPost]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        try
        {
            var user = await userService.CreateUserAsync(request, ActorId, ct);
            return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, user);
        }
        catch (PermissionConflictException ex)
        {
            return UnprocessableEntity(new { error_code = "permission_conflict", message = ex.Message });
        }
    }

    // ── PUT /api/v1/admin/users/{id} ─────────────────────────────────────────

    [HttpPut("{id:guid}")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        try
        {
            var user = await userService.UpdateUserAsync(id, request, ActorId, ct);
            return Ok(user);
        }
        catch (UserNotFoundException)
        {
            return NotFound();
        }
    }

    // ── PATCH /api/v1/admin/users/{id}/role ──────────────────────────────────

    [HttpPatch("{id:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest request, CancellationToken ct)
    {
        try
        {
            await userService.AssignRoleAsync(id, request, ActorId, ct);
            return NoContent();
        }
        catch (UserNotFoundException)
        {
            return NotFound();
        }
        catch (PermissionConflictException ex)
        {
            return UnprocessableEntity(new { error_code = "permission_conflict", message = ex.Message });
        }
    }

    // ── PATCH /api/v1/admin/users/{id}/reset-password ────────────────────────

    [HttpPatch("{id:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { error_code = "invalid_password", message = "New password is required." });

        try
        {
            await userService.ResetPasswordAsync(id, request, ActorId, ct);
            return NoContent();
        }
        catch (UserNotFoundException)
        {
            return NotFound();
        }
    }

    // ── PATCH /api/v1/admin/users/{id}/disable ───────────────────────────────

    [HttpPatch("{id:guid}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableUser(Guid id, CancellationToken ct)
    {
        if (id == ActorId)
            return BadRequest(new { error_code = "self_disable_forbidden" });

        try
        {
            await userService.DisableUserAsync(id, ActorId, ct);
            return NoContent();
        }
        catch (UserNotFoundException)
        {
            return NotFound();
        }
    }

    // ── PATCH /api/v1/admin/users/{id}/enable ────────────────────────────────

    [HttpPatch("{id:guid}/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnableUser(Guid id, CancellationToken ct)
    {
        try
        {
            await userService.EnableUserAsync(id, ActorId, ct);
            return NoContent();
        }
        catch (UserNotFoundException)
        {
            return NotFound();
        }
    }
}
