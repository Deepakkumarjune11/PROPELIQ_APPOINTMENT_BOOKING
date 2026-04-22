namespace ClinicalIntelligence.Application.AI.Sanitization;

/// <summary>
/// Configuration bound from <c>appsettings.json → PromptSanitization</c>.
/// Hot-reloaded at runtime via <see cref="Microsoft.Extensions.Options.IOptionsMonitor{T}"/>.
/// </summary>
public sealed class PromptInjectionOptions
{
    public const string SectionName = "PromptSanitization";

    /// <summary>
    /// Patterns that immediately block a request and throw
    /// <see cref="PromptInjectionBlockedException"/> (INJ-xxx IDs).
    /// </summary>
    public List<InjectionPatternEntry> BlockPatterns  { get; init; } = [];

    /// <summary>
    /// Patterns that flag a request for review logging but allow it to proceed
    /// with the normalised input (REV-xxx IDs).
    /// </summary>
    public List<InjectionPatternEntry> ReviewPatterns { get; init; } = [];
}

/// <summary>A single regex rule with a stable identifier and human-readable description.</summary>
/// <param name="Id">Stable pattern identifier, e.g. <c>INJ-001</c>.</param>
/// <param name="Pattern">.NET regex pattern string (case-insensitive, multiline).</param>
/// <param name="Description">Short human-readable label for telemetry / incident reports.</param>
public sealed record InjectionPatternEntry(string Id, string Pattern, string Description);
