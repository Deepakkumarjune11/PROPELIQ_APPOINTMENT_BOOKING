namespace PatientAccess.Application.Models;

/// <summary>A single turn in the intake conversation (user or AI assistant).</summary>
/// <param name="Role">"user" or "assistant".</param>
/// <param name="Content">The message text.</param>
public sealed record ChatTurn(string Role, string Content);
