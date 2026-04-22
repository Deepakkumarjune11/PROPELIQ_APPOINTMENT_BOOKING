namespace ClinicalIntelligence.Application.AI.Schema;

/// <summary>
/// Validates AI response JSON against a per-featureContext schema (US_032, AIR-Q03).
/// Returns immediately on the first violation to avoid unnecessary processing.
/// </summary>
public interface IAiSchemaValidator
{
    /// <summary>
    /// Validates <paramref name="jsonContent"/> against the registered schema for
    /// <paramref name="featureContext"/>.
    /// </summary>
    /// <param name="jsonContent">Raw AI response text to validate.</param>
    /// <param name="featureContext">Feature context key; determines which schema applies.</param>
    /// <returns>
    /// <see cref="SchemaValidationResult.Valid()"/> when content passes or when no schema is
    /// registered for the context; <see cref="SchemaValidationResult.Invalid(string)"/> otherwise.
    /// </returns>
    SchemaValidationResult Validate(string jsonContent, string featureContext);
}
