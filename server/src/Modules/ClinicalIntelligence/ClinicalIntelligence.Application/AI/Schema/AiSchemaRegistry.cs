using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace ClinicalIntelligence.Application.AI.Schema;

/// <summary>
/// Static registry of expected JSON schemas per featureContext (US_032, AIR-Q03).
///
/// Only feature contexts that return structured JSON are registered here. Free-text contexts
/// (e.g., <c>"ConversationalIntake"</c>) are absent — the validator passes them without checks.
///
/// Boolean fields use the sentinel <c>JsonValueKind.True | JsonValueKind.False</c> (value = 7)
/// as an "accept either boolean" marker interpreted by <see cref="JsonDocumentSchemaValidator"/>.
/// </summary>
public static class AiSchemaRegistry
{
    // Boolean field sentinel: JsonValueKind.True (5) | JsonValueKind.False (6) = 7.
    // JsonValueKind.Null = 7 coincides numerically but we only use this sentinel in schemas
    // that cannot legitimately require a Null value — it is safe to reuse.
    private static readonly JsonValueKind BooleanField = JsonValueKind.True | JsonValueKind.False;

    private static readonly IReadOnlyDictionary<string, AiSchemaDefinition> Schemas =
        new Dictionary<string, AiSchemaDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["FactExtraction"] = new(new Dictionary<string, JsonValueKind>
            {
                { "facts",             JsonValueKind.Array  },
                { "documentId",        JsonValueKind.String },
                { "extractionVersion", JsonValueKind.String },
            }),

            ["CodeSuggestion"] = new(new Dictionary<string, JsonValueKind>
            {
                { "suggestedCodes", JsonValueKind.Array  },
                { "confidence",     JsonValueKind.Number },
                { "rationale",      JsonValueKind.String },
            }),

            ["ConflictDetection"] = new(new Dictionary<string, JsonValueKind>
            {
                { "conflicts",            JsonValueKind.Array },
                { "hasCriticalConflicts", BooleanField        },  // accept true or false
            }),
        };

    /// <summary>
    /// Attempts to retrieve the <see cref="AiSchemaDefinition"/> registered for
    /// <paramref name="featureContext"/>.
    /// </summary>
    /// <param name="featureContext">Feature context key (case-insensitive).</param>
    /// <param name="schema">The registered schema, or <see langword="null"/> when absent.</param>
    /// <returns>
    /// <see langword="true"/> when a schema is registered; <see langword="false"/> otherwise
    /// (caller should skip validation).
    /// </returns>
    public static bool TryGetSchema(
        string featureContext,
        [NotNullWhen(true)] out AiSchemaDefinition? schema)
        => Schemas.TryGetValue(featureContext, out schema);
}
