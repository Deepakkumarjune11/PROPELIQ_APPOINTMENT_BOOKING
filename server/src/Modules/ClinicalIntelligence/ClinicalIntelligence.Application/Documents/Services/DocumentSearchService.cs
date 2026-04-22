using ClinicalIntelligence.Application.AI;
using ClinicalIntelligence.Application.AI.Access;
using ClinicalIntelligence.Application.Documents.Dtos;
using ClinicalIntelligence.Application.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ClinicalIntelligence.Application.Documents.Services;

/// <summary>
/// Implements <see cref="IDocumentSearchService"/> using pgvector cosine similarity search
/// (TR-015, AIR-R02) with ownership enforcement (AIR-S02).
///
/// Flow:
/// 1. Circuit-open guard — return empty if the AI gateway is unavailable.
/// 2. Embed <c>queryText</c> via <see cref="IAiGateway"/> (Redis cache-first, AIR-O04).
/// 3. Delegate the pgvector cosine query + ownership filter to <see cref="IEmbeddingChunkRepository"/>.
///    The repository enforces <c>document_id IN (patient's document IDs)</c> before executing
///    the vector query, preventing cross-patient data leakage (AIR-S02).
/// 4. Returns at most 5 chunks with similarity ≥ 0.7 (AIR-R02).
/// </summary>
public sealed class DocumentSearchService : IDocumentSearchService
{
    private const int   TopK              = 5;
    private const float SimilarityThreshold = 0.7f;

    private readonly IAiGateway                      _aiGateway;
    private readonly IEmbeddingChunkRepository       _chunkRepo;
    private readonly IRagAccessFilter                _ragAccessFilter;
    private readonly ILogger<DocumentSearchService>  _logger;

    public DocumentSearchService(
        IAiGateway                     aiGateway,
        IEmbeddingChunkRepository      chunkRepo,
        IRagAccessFilter               ragAccessFilter,
        ILogger<DocumentSearchService> logger)
    {
        _aiGateway       = aiGateway;
        _chunkRepo       = chunkRepo;
        _ragAccessFilter = ragAccessFilter;
        _logger          = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChunkSearchResultDto>> SearchAsync(
        string            queryText,
        Guid              patientId,
        CancellationToken ct = default)
    {
        // Circuit-open guard (AIR-O02) — do not call Azure OpenAI when circuit is open
        if (_aiGateway.IsCircuitOpen)
        {
            _logger.LogWarning(
                "DocumentSearchService: AI gateway circuit open; returning empty results for patient {PatientId}.",
                patientId);
            return Array.Empty<ChunkSearchResultDto>();
        }

        // Embed the query text (single input, batch=1)
        var vectors = await _aiGateway.GenerateEmbeddingsAsync(
            new[] { queryText }, patientId, ct);

        var queryVector = vectors[0];

        // Delegate ownership-filtered cosine similarity query to repository (AIR-S02)
        var results = await _chunkRepo.SearchSimilarAsync(
            patientId, queryVector, TopK, SimilarityThreshold, ct);

        _logger.LogDebug(
            "DocumentSearchService: search for patient {PatientId} returned {Count} chunk(s).",
            patientId, results.Count);

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChunkSearchResultDto>> SearchByDocumentAsync(
        string            queryText,
        Guid              documentId,
        CancellationToken ct = default)
    {
        if (_aiGateway.IsCircuitOpen)
        {
            _logger.LogWarning(
                "DocumentSearchService: AI gateway circuit open; returning empty results for document {DocumentId}.",
                documentId);
            return Array.Empty<ChunkSearchResultDto>();
        }

        var vectors = await _aiGateway.GenerateEmbeddingsAsync(
            new[] { queryText }, documentId, ct);

        var results = await _chunkRepo.SearchSimilarByDocumentAsync(
            documentId, vectors[0], TopK, SimilarityThreshold, ct);

        _logger.LogDebug(
            "DocumentSearchService: document-scoped search for {DocumentId} returned {Count} chunk(s).",
            documentId, results.Count);

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChunkSearchResultDto>> SearchAsync(
        string            queryText,
        Guid              actorId,
        string            actorRole,
        CancellationToken ct = default)
    {
        // Circuit-open guard (AIR-O02)
        if (_aiGateway.IsCircuitOpen)
        {
            _logger.LogWarning(
                "DocumentSearchService: AI gateway circuit open; returning empty results for actor {ActorId}.",
                actorId);
            return Array.Empty<ChunkSearchResultDto>();
        }

        // Resolve permitted document IDs for this actor (AIR-S02, OWASP A01)
        var authorizedIds = await _ragAccessFilter
            .GetAuthorizedDocumentIdsAsync(actorId, actorRole, ct)
            .ConfigureAwait(false);

        // Fail-closed: empty list means no documents are accessible
        if (authorizedIds is { Count: 0 })
        {
            _logger.LogWarning(
                "DocumentSearchService: actor {ActorId} (role={ActorRole}) has no authorised documents — returning empty.",
                actorId, actorRole);
            return Array.Empty<ChunkSearchResultDto>();
        }

        // Embed query (actorId used as correlation ID for AIR-S03 logging)
        var vectors     = await _aiGateway.GenerateEmbeddingsAsync(new[] { queryText }, actorId, ct);
        var queryVector = vectors[0];

        var results = await _chunkRepo.SearchSimilarByAuthorizedIdsAsync(
            authorizedIds, queryVector, TopK, SimilarityThreshold, ct);

        _logger.LogDebug(
            "DocumentSearchService: actor-based search for {ActorId} (role={ActorRole}) returned {Count} chunk(s).",
            actorId, actorRole, results.Count);

        return results;
    }
}
