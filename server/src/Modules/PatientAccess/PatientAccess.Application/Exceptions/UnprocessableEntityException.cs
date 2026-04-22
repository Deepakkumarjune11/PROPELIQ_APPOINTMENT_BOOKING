namespace PatientAccess.Application.Exceptions;

/// <summary>
/// Thrown when a request is well-formed but cannot be processed in its current state.
/// Maps to HTTP 422 Unprocessable Entity — used for semantic validation failures
/// that go beyond structural validation (e.g., slot is now available; book directly).
/// </summary>
public sealed class UnprocessableEntityException : Exception
{
    public UnprocessableEntityException(string message) : base(message) { }
}
