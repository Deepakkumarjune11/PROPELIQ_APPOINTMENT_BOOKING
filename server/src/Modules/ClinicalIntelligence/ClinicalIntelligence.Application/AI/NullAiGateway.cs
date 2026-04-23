using ClinicalIntelligence.Application.AI.Models;

namespace ClinicalIntelligence.Application.AI;

/// <summary>
/// No-op implementation of <see cref="IChatCompletionGateway"/> used when no AI
/// backend is configured.
///
/// Always reports <see cref="IsCircuitOpen"/> = <c>true</c>, which causes all
/// upstream Hangfire jobs (<see cref="Documents.Jobs.EmbeddingGenerationJob"/>,
/// <see cref="Documents.Jobs.FactExtractionJob"/>, <see cref="Documents.Jobs.CodeSuggestionJob"/>)
/// to route documents to <c>ManualReview</c> immediately via their existing
/// circuit-open guards — no exceptions, no retries, no external calls.
///
/// Replace this registration in <c>ServiceCollectionExtensions</c> with a real
/// gateway (e.g. OllamaGateway or AzureOpenAiGateway) when an AI backend is available.
/// </summary>
public sealed class NullAiGateway : IChatCompletionGateway
{
    /// <summary>Always open — signals upstream jobs to skip AI and route to ManualReview.</summary>
    public bool IsCircuitOpen => true;

    public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        Guid                  documentId,
        CancellationToken     ct = default)
        => Task.FromResult<IReadOnlyList<float[]>>(Array.Empty<float[]>());

    public Task<GatewayResponse> ChatCompletionAsync(
        string            featureContext,
        string            systemPrompt,
        string            userMessage,
        Guid              correlationId,
        CancellationToken ct = default)
        => Task.FromResult(GatewayResponse.Success(string.Empty, 0, 0, featureContext));

    public Task<GatewayResponse> ChatCompletionAsync(
        string            systemPrompt,
        string            userMessage,
        Guid              documentId,
        CancellationToken ct = default)
        => Task.FromResult(GatewayResponse.Success(string.Empty, 0, 0, "FactExtraction"));
}
