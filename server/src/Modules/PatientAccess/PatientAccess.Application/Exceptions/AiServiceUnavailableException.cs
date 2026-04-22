namespace PatientAccess.Application.Exceptions;

/// <summary>
/// Thrown by <c>IAiIntakeService</c> when the AI service circuit-breaker is open
/// or the upstream provider is unreachable (AIR-O02).
/// Caught by <see cref="Services.ConversationalIntakeService"/> and converted to a
/// <c>fallbackToManual: true</c> response — the exception is never propagated to the caller (AC-5).
/// </summary>
public sealed class AiServiceUnavailableException : Exception
{
    public AiServiceUnavailableException(string message) : base(message) { }
    public AiServiceUnavailableException(string message, Exception inner) : base(message, inner) { }
}
