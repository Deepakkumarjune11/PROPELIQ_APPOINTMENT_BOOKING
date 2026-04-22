using System.Text.Json;

namespace ClinicalIntelligence.Application.AI.Schema;

/// <summary>
/// <c>System.Text.Json</c>-based AI response schema validator (US_032, AC-2, AIR-Q03).
///
/// Validation algorithm (per featureContext with a registered schema):
///   1. Parse the JSON — a <see cref="JsonException"/> is a hard schema failure.
///   2. Assert root element is <c>Object</c>.
///   3. For each <c>RequiredProperties</c> entry:
///      a. Assert the property exists on the root object.
///      b. Assert the property's <see cref="JsonValueKind"/> matches the expected kind.
///         Boolean fields (sentinel = <c>True | False</c> = 7) accept either
///         <see cref="JsonValueKind.True"/> or <see cref="JsonValueKind.False"/>.
///
/// No schema registered for <paramref name="featureContext"/> → returns <see cref="SchemaValidationResult.Valid()"/>
/// (free-text contexts bypass validation).
///
/// Thread-safety: stateless; safe for singleton lifetime.
/// </summary>
public sealed class JsonDocumentSchemaValidator : IAiSchemaValidator
{
    // Sentinel for boolean fields: JsonValueKind.True (5) | JsonValueKind.False (6) = 7
    private static readonly JsonValueKind BooleanSentinel = JsonValueKind.True | JsonValueKind.False;

    /// <inheritdoc />
    public SchemaValidationResult Validate(string jsonContent, string featureContext)
    {
        if (!AiSchemaRegistry.TryGetSchema(featureContext, out var schema))
            return SchemaValidationResult.Valid(); // No schema registered — pass-through

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(jsonContent);
        }
        catch (JsonException ex)
        {
            // Malformed JSON is a hard schema failure — error message is safe (no content)
            return SchemaValidationResult.Invalid($"JSON parse error: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return SchemaValidationResult.Invalid(
                    $"Root element must be Object, got {root.ValueKind}");

            foreach (var (propertyName, expectedKind) in schema.RequiredProperties)
            {
                if (!root.TryGetProperty(propertyName, out var prop))
                    return SchemaValidationResult.Invalid(
                        $"Missing required property '{propertyName}'");

                if (expectedKind == BooleanSentinel)
                {
                    // Boolean field: accept JsonValueKind.True or JsonValueKind.False
                    if (prop.ValueKind != JsonValueKind.True && prop.ValueKind != JsonValueKind.False)
                        return SchemaValidationResult.Invalid(
                            $"Property '{propertyName}' expected boolean, got {prop.ValueKind}");
                }
                else if (prop.ValueKind != expectedKind)
                {
                    return SchemaValidationResult.Invalid(
                        $"Property '{propertyName}' expected {expectedKind}, got {prop.ValueKind}");
                }
            }
        }

        return SchemaValidationResult.Valid();
    }
}
