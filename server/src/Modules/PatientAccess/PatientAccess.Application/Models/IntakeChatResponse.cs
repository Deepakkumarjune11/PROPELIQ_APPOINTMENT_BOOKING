namespace PatientAccess.Application.Models;

/// <summary>
/// Response from one conversational turn (AIR-002).
/// </summary>
/// <param name="AssistantMessage">The AI-generated reply to display in the chat window.</param>
/// <param name="IsComplete">
/// <see langword="true"/> when the AI has collected all required intake information.
/// <see cref="StructuredAnswers"/> is populated alongside this flag.
/// </param>
/// <param name="FallbackToManual">
/// <see langword="true"/> when the AI circuit-breaker has tripped (AC-5 / AIR-O02).
/// The frontend should display the fallback banner and redirect to the manual form.
/// </param>
/// <param name="StructuredAnswers">
/// Populated when <see cref="IsComplete"/> is <see langword="true"/>. Keys match the
/// INTAKE_QUESTIONS ids so the manual form pre-populates correctly on mode-switch (AC-3).
/// </param>
public sealed record IntakeChatResponse(
    string AssistantMessage,
    bool IsComplete,
    bool FallbackToManual,
    Dictionary<string, string>? StructuredAnswers
);
