using PatientAccess.Application.Models;

namespace PatientAccess.Application.Infrastructure;

/// <summary>
/// Result returned by <see cref="IAiIntakeService.SendMessageAsync"/> for one conversational turn.
/// </summary>
/// <param name="AssistantMessage">AI-generated reply text.</param>
/// <param name="IsComplete">
/// <see langword="true"/> when all required intake questions have been answered.
/// <see cref="StructuredAnswers"/> is populated alongside this flag.
/// </param>
/// <param name="StructuredAnswers">
/// Populated when <see cref="IsComplete"/> is <see langword="true"/>. Maps questionId → answer text.
/// </param>
public sealed record IntakeConversationResult(
    string AssistantMessage,
    bool IsComplete,
    Dictionary<string, string>? StructuredAnswers
);
