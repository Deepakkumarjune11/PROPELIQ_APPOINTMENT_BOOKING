namespace PatientAccess.Application.Models;

/// <summary>
/// Request payload for one conversational turn sent by the patient (FR-012).
/// </summary>
/// <param name="Message">
/// The text the patient just typed. An empty string on the very first call triggers
/// the AI greeting (AC-1) — the backend ignores the empty message and generates the opening prompt.
/// </param>
/// <param name="ConversationHistory">
/// Prior turns that the client holds locally. Used as a fallback when Redis history is absent
/// (e.g., after a page refresh). If Redis cache is populated, server history takes precedence.
/// </param>
public sealed record IntakeChatRequest(
    string Message,
    IReadOnlyList<ChatTurn> ConversationHistory
);
