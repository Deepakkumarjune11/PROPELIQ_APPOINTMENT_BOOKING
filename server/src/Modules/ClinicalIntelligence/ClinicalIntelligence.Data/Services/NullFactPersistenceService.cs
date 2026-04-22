using ClinicalIntelligence.Application.AI.Models;
using ClinicalIntelligence.Application.Documents.Services;
using Microsoft.Extensions.Logging;

namespace ClinicalIntelligence.Data.Services;

/// <summary>
/// Stub implementation of <see cref="IFactPersistenceService"/> registered until
/// <c>us_020/task_002_be_fact_persistence</c> creates the real <c>FactPersistenceService</c>.
///
/// Behaviour: logs a warning and discards the facts. The document status is NOT
/// transitioned to <c>Completed</c> until the real implementation is registered.
///
/// To replace: implement <c>FactPersistenceService</c> in <c>ClinicalIntelligence.Data</c>
/// and update the DI registration in <c>ServiceCollectionExtensions.cs</c>.
/// </summary>
public sealed class NullFactPersistenceService : IFactPersistenceService
{
    private readonly ILogger<NullFactPersistenceService> _logger;

    public NullFactPersistenceService(ILogger<NullFactPersistenceService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task PersistAsync(
        Guid                               documentId,
        IReadOnlyList<ExtractedFactResult> facts,
        CancellationToken                  ct = default)
    {
        _logger.LogWarning(
            "NullFactPersistenceService: PersistAsync called for document {DocumentId} with {Count} fact(s) — " +
            "real persistence not yet implemented (us_020/task_002). Facts discarded.",
            documentId, facts.Count);

        return Task.CompletedTask;
    }
}
