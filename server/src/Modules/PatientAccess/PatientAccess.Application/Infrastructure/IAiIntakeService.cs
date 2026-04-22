using PatientAccess.Application.Exceptions;
using PatientAccess.Application.Models;

namespace PatientAccess.Application.Infrastructure;

/// <summary>
/// Abstraction for the AI intake conversation service (AIR-002).
/// Implemented in task_003 (AI Intake Prompt Setup) via Azure OpenAI GPT-4.
/// <para>
/// Throws <see cref="AiServiceUnavailableException"/> when the circuit-breaker is open
/// or the upstream provider is unreachable (AIR-O02).
/// </para>
/// </summary>
public interface IAiIntakeService
{
    /// <summary>
    /// Sends the full conversation history to the AI and returns the next assistant message.
    /// </summary>
    /// <param name="conversationHistory">
    /// All prior turns including the user's latest message (already appended by the caller).
    /// </param>
    /// <param name="ct">Cancellation token — supports p95 ≤ 3s timeout enforcement (AIR-Q02).</param>
    /// <returns>AI response with optional structured answers when all questions are gathered.</returns>
    /// <exception cref="AiServiceUnavailableException">Thrown when the AI service is unreachable.</exception>
    Task<IntakeConversationResult> SendMessageAsync(
        IReadOnlyList<ChatTurn> conversationHistory,
        CancellationToken ct = default);
}
