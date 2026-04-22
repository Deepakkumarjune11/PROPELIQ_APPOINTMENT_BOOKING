namespace PatientAccess.Application.Exceptions;

/// <summary>
/// Thrown when a resource conflict is detected — maps to HTTP 409 Conflict.
/// Common cases: slot already booked, duplicate email concurrent insert.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
