namespace Admin.Application.Exceptions;

/// <summary>
/// Thrown when a role and permission combination is invalid per <see cref="Validators.PermissionValidator"/>.
/// Maps to HTTP 422 Unprocessable Entity in the API layer.
/// </summary>
public sealed class PermissionConflictException(string message) : Exception(message);
