namespace ClinicalIntelligence.Application.AI.Schema;

/// <summary>
/// Immutable result from <see cref="IAiSchemaValidator.Validate"/> (US_032, AIR-Q03).
/// </summary>
/// <param name="IsValid"><see langword="true"/> when the JSON content satisfies the schema; <see langword="false"/> otherwise.</param>
/// <param name="ErrorReason">
/// Human-readable description of the first schema violation (property name + kind mismatch).
/// <see langword="null"/> when <see cref="IsValid"/> is <see langword="true"/>.
/// Safe to log — must NOT contain raw AI response content (TR-006 PHI protection).
/// </param>
public sealed record SchemaValidationResult(bool IsValid, string? ErrorReason)
{
    /// <summary>Factory: content passes schema validation.</summary>
    public static SchemaValidationResult Valid() => new(true, null);

    /// <summary>Factory: content fails schema validation for the given <paramref name="reason"/>.</summary>
    public static SchemaValidationResult Invalid(string reason) => new(false, reason);
}
