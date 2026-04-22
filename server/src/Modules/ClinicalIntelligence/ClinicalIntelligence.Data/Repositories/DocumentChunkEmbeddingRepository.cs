using ClinicalIntelligence.Application.Documents.Dtos;
using ClinicalIntelligence.Application.Documents.Models;
using ClinicalIntelligence.Application.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatientAccess.Data;
using PatientAccess.Data.Entities;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace ClinicalIntelligence.Data.Repositories;

/// <summary>
/// EF Core + pgvector implementation of <see cref="IEmbeddingChunkRepository"/> and
/// <see cref="IChunkStagingService"/> backed by the <c>document_chunk_embeddings</c> table (DR-016).
///
/// Replaces <see cref="NullEmbeddingChunkRepository"/> and <see cref="Services.NullChunkStagingService"/>
/// once the migration is applied (us_019/task_003).
///
/// Security: all vector search queries are scoped to patient-owned documents to enforce
/// cross-patient data isolation (AIR-S02).
/// </summary>
public sealed class DocumentChunkEmbeddingRepository
    : IEmbeddingChunkRepository, IChunkStagingService
{
    private readonly PropelIQDbContext _db;
    private readonly ILogger<DocumentChunkEmbeddingRepository> _logger;

    public DocumentChunkEmbeddingRepository(
        PropelIQDbContext                          db,
        ILogger<DocumentChunkEmbeddingRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── IChunkStagingService ─────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task StageChunksAsync(
        IAsyncEnumerable<DocumentChunk> chunks,
        CancellationToken               ct = default)
    {
        var entities = new List<DocumentChunkEmbedding>();
        await foreach (var chunk in chunks.WithCancellation(ct).ConfigureAwait(false))
        {
            entities.Add(new DocumentChunkEmbedding(
                chunk.DocumentId,
                chunk.ChunkIndex,
                chunk.ChunkText,
                chunk.TokenCount));
        }

        if (entities.Count == 0)
        {
            _logger.LogDebug("StageChunksAsync: no chunks to stage.");
            return;
        }

        await _db.DocumentChunkEmbeddings.AddRangeAsync(entities, ct).ConfigureAwait(false);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "StageChunksAsync: staged {Count} chunk(s) for document {DocumentId}.",
            entities.Count, entities[0].DocumentId);
    }

    // ── IEmbeddingChunkRepository ────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmbeddingChunkDto>> GetUnembeddedChunksAsync(
        Guid              documentId,
        CancellationToken ct = default)
    {
        return await _db.DocumentChunkEmbeddings
            .Where(c => c.DocumentId == documentId && c.Embedding == null)
            .OrderBy(c => c.ChunkIndex)
            .Select(c => new EmbeddingChunkDto(c.Id, c.ChunkIndex, c.ChunkText))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateEmbeddingsAsync(
        IReadOnlyList<EmbeddingChunkUpdateDto> updates,
        CancellationToken                      ct = default)
    {
        if (updates.Count == 0) return;

        var ids    = updates.Select(u => u.ChunkId).ToList();
        var chunks = await _db.DocumentChunkEmbeddings
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var updateMap = updates.ToDictionary(u => u.ChunkId);
        foreach (var chunk in chunks)
        {
            if (updateMap.TryGetValue(chunk.Id, out var update))
                chunk.SetEmbedding(new Vector(update.Vector));
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "UpdateEmbeddingsAsync: persisted {Count} embedding(s) for document.",
            chunks.Count);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Execution plan (pgvector ivfflat, TR-015):
    /// 1. Resolve patient-owned document IDs (ownership guard — AIR-S02).
    /// 2. Cosine-distance ORDER BY using <c>embedding &lt;=&gt; @query</c>; ivfflat index hit.
    /// 3. Take top-<paramref name="limit"/> rows and apply similarity threshold in-memory.
    /// </remarks>
    public async Task<IReadOnlyList<ChunkSearchResultDto>> SearchSimilarAsync(
        Guid              patientId,
        float[]           queryVector,
        int               limit,
        float             threshold,
        CancellationToken ct = default)
    {
        // Step 1 — ownership filter (AIR-S02): only search documents belonging to this patient
        var patientDocIds = await _db.ClinicalDocuments
            .Where(d => d.PatientId == patientId)
            .Select(d => d.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (patientDocIds.Count == 0)
            return Array.Empty<ChunkSearchResultDto>();

        // Step 2 — pgvector cosine distance query (ivfflat index, TR-015)
        var queryVec = new Vector(queryVector);
        var rows = await _db.DocumentChunkEmbeddings
            .Where(c => patientDocIds.Contains(c.DocumentId) && c.Embedding != null)
            .OrderBy(c => c.Embedding!.CosineDistance(queryVec))
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Step 3 — compute similarity and apply threshold in-memory (no EF translation needed)
        var results = new List<ChunkSearchResultDto>(rows.Count);
        foreach (var row in rows)
        {
            var cosineDistance = (float)row.Embedding!.CosineDistance(queryVec);
            var similarity     = 1.0f - cosineDistance;
            if (similarity >= threshold)
            {
                results.Add(new ChunkSearchResultDto(
                    row.DocumentId,
                    row.ChunkIndex,
                    row.ChunkText,
                    row.TokenCount,
                    similarity));
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChunkSearchResultDto>> SearchSimilarByDocumentAsync(
        Guid              documentId,
        float[]           queryVector,
        int               limit,
        float             threshold,
        CancellationToken ct = default)
    {
        var queryVec = new Vector(queryVector);
        var rows = await _db.DocumentChunkEmbeddings
            .Where(c => c.DocumentId == documentId && c.Embedding != null)
            .OrderBy(c => c.Embedding!.CosineDistance(queryVec))
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var results = new List<ChunkSearchResultDto>(rows.Count);
        foreach (var row in rows)
        {
            var cosineDistance = (float)row.Embedding!.CosineDistance(queryVec);
            var similarity     = 1.0f - cosineDistance;
            if (similarity >= threshold)
            {
                results.Add(new ChunkSearchResultDto(
                    row.DocumentId,
                    row.ChunkIndex,
                    row.ChunkText,
                    row.TokenCount,
                    similarity));
            }
        }

        return results;
    }

    /// <inheritdoc />
    /// <remarks>
    /// When <paramref name="authorizedIds"/> is <c>null</c> the query runs without a
    /// document-ID filter (Admin / System unrestricted access).
    /// When it is an empty collection this method returns an empty list immediately —
    /// the access filter has already denied access (fail-closed, AIR-S02).
    /// </remarks>
    public async Task<IReadOnlyList<ChunkSearchResultDto>> SearchSimilarByAuthorizedIdsAsync(
        IReadOnlyList<Guid>? authorizedIds,
        float[]              queryVector,
        int                  limit,
        float                threshold,
        CancellationToken    ct = default)
    {
        // Fail-closed: empty list means no access was granted
        if (authorizedIds is { Count: 0 })
            return Array.Empty<ChunkSearchResultDto>();

        var queryVec = new Vector(queryVector);
        var baseQuery = _db.DocumentChunkEmbeddings
            .Where(c => c.Embedding != null);

        if (authorizedIds is not null)
            baseQuery = baseQuery.Where(c => authorizedIds.Contains(c.DocumentId));

        var rows = await baseQuery
            .OrderBy(c => c.Embedding!.CosineDistance(queryVec))
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var results = new List<ChunkSearchResultDto>(rows.Count);
        foreach (var row in rows)
        {
            var cosineDistance = (float)row.Embedding!.CosineDistance(queryVec);
            var similarity     = 1.0f - cosineDistance;
            if (similarity >= threshold)
            {
                results.Add(new ChunkSearchResultDto(
                    row.DocumentId,
                    row.ChunkIndex,
                    row.ChunkText,
                    row.TokenCount,
                    similarity));
            }
        }

        return results;
    }
}
