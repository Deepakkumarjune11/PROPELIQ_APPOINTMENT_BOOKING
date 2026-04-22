namespace Admin.Application.Exceptions;

/// <summary>
/// Thrown when a Staff or Admin entity with the specified ID cannot be found.
/// Maps to HTTP 404 Not Found in the API layer.
/// </summary>
public sealed class UserNotFoundException(Guid userId)
    : Exception($"User with id '{userId}' was not found.");
