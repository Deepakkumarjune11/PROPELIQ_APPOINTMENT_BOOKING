using PatientAccess.Application.Models;

namespace PatientAccess.Application.Services;

/// <summary>
/// Thin orchestration layer for the conversational AI intake flow (AIR-002, FR-012).
/// Validates the patient, manages Redis conversation history, delegates to <c>IAiIntakeService</c>,
/// and persists <c>IntakeResponse</c> + <c>AuditLog</c> when all required questions are gathered.
/// </summary>
public interface IConversationalIntakeService
{
    /// <summary>
    /// Processes one conversational turn for the given patient.
    /// </summary>
    /// <param name="patientId">Patient currently filling in the intake form.</param>
    /// <param name="request">Incoming user message and client-side conversation history.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// AI reply, completion flag, fallback flag, and structured answers when complete.
    /// Never throws — circuit-breaker failures are surfaced via <see cref="IntakeChatResponse.FallbackToManual"/>.
    /// </returns>
    Task<IntakeChatResponse> SendMessageAsync(
        Guid patientId,
        IntakeChatRequest request,
        CancellationToken ct = default);
}
