namespace PatientAccess.Application.DTOs;

/// <summary>
/// Successful authentication response containing a signed JWT and identity claims.
/// <c>ExpiresAt</c> is Unix time in milliseconds (UTC) matching the token's <c>exp</c> claim.
/// </summary>
public record AuthResponse(string Token, Guid UserId, string Username, string Role, long ExpiresAt);
