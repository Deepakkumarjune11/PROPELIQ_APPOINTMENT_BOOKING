namespace PatientAccess.Application.Exceptions;

/// <summary>
/// Thrown when a requested resource cannot be located — maps to HTTP 404 Not Found.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
