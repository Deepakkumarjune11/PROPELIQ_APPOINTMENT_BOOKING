namespace ClinicalIntelligence.Application.Infrastructure;

/// <summary>
/// Abstraction for the embedding vector cache (AIR-O04).
/// Isolates the <see cref="AI.AzureOpenAiGateway"/> from the concrete Redis client so the
/// gateway can be unit-tested without a live Redis connection.
///
/// Cache keys are SHA256 hashes of the chunk text — no PII stored as key material.
/// TTL for all embedding entries: 7 days (AIR-O04).
/// </summary>
public interface IAiEmbeddingCache
{
    /// <summary>Returns the cached embedding vector for the given key, or <c>null</c> on miss.</summary>
    Task<float[]?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Stores an embedding vector in the cache with the specified TTL.</summary>
    Task SetAsync(string key, float[] vector, TimeSpan expiry, CancellationToken ct = default);
}
