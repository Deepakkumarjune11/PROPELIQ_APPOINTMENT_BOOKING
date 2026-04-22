using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PatientAccess.Application.DTOs;
using PatientAccess.Application.Services;

namespace PatientAccess.Presentation.Controllers;

/// <summary>
/// Handles JWT-based login, token refresh, and logout for Staff, Admin, and Patient principals.
/// All failure responses use the same generic message to prevent user enumeration (OWASP A07).
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>
    /// Authenticates a user and returns a 15-minute JWT.
    /// </summary>
    /// <remarks>
    /// Staff and Admin supply their <c>Username</c> in the <c>email</c> field.
    /// Rate-limited to 5 requests per 60 seconds per IP (OWASP A07).
    /// </remarks>
    /// <response code="200">Authentication successful — returns token and identity claims.</response>
    /// <response code="401">Invalid credentials (user not found or wrong password).</response>
    /// <response code="429">Too many login attempts — rate limit exceeded.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login-fixed-window")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
     var result = await authService.LoginAsync(request, ct);
        if (result is null)
            return Unauthorized(new { message = "Invalid credentials" });

        return Ok(result);
    }

    /// <summary>
    /// Issues a new 15-minute JWT, blacklisting the current token in Redis.
    /// Accepts tokens within a 30-second grace window past expiry (NFR-005).
    /// </summary>
    /// <response code="200">Refresh successful — returns new token.</response>
    /// <response code="401">Token invalid, expired beyond grace window, or already revoked.</response>
    [HttpPost("refresh")]
    [Authorize]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var token  = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        var result = await authService.RefreshAsync(token, ct);
        if (result is null)
            return Unauthorized(new { message = "Token refresh failed" });

        return Ok(result);
    }

    /// <summary>
    /// Blacklists the current token in Redis (FR-017) and writes a logout audit entry.
    /// </summary>
    /// <response code="204">Logout successful.</response>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        await authService.LogoutAsync(token, ct);
        return NoContent();
    }
}
