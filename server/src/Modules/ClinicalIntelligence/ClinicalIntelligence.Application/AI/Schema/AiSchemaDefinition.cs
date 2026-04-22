using System.Text.Json;

namespace ClinicalIntelligence.Application.AI.Schema;

/// <summary>
/// Describes the expected shape of a structured AI response for a specific featureContext
/// (US_032, AIR-Q03).
///
/// Each entry in <see cref="RequiredProperties"/> maps a top-level JSON property name to its
/// expected <see cref="JsonValueKind"/>. Boolean properties use the sentinel value
/// <c>JsonValueKind.True | JsonValueKind.False</c> (= 7) to indicate that either
/// <c>true</c> or <c>false</c> is acceptable — see <see cref="AiSchemaRegistry"/>.
/// </summary>
/// <param name="RequiredProperties">
/// Dictionary of required top-level property names to their expected <see cref="JsonValueKind"/>.
/// </param>
public sealed record AiSchemaDefinition(
    IReadOnlyDictionary<string, JsonValueKind> RequiredProperties);
