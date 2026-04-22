using ClinicalIntelligence.Application.Documents.Models;
using ClinicalIntelligence.Application.Documents.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatientAccess.Data;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace ClinicalIntelligence.Data.Services;

/// <summary>
/// Production implementation of <see cref="ICodeSuggestionPersistenceService"/> (US_023/task_002).
///
/// <list type="number">
///   <item><b>GetPlainTextFactsAsync</b> — loads all non-deleted ExtractedFact rows for the
///   patient (via ClinicalDocument join), decrypts PHI ciphertext, and returns DTOs for
///   context assembly.</item>
///   <item><b>PersistAsync</b> — soft-deletes existing CodeSuggestion rows (DR-017) and
///   inserts new rows for idempotent re-generation support.</item>
/// </list>
/// </summary>
public sealed class CodeSuggestionPersistenceService : ICodeSuggestionPersistenceService
{
    private const string FactTextPurpose = "PropelIQ.ExtractedFacts.FactText";

    private readonly PropelIQDbContext                          _db;
    private readonly IDataProtectionProvider                    _dataProtectionProvider;
    private readonly ILogger<CodeSuggestionPersistenceService>  _logger;

    public CodeSuggestionPersistenceService(
        PropelIQDbContext                           db,
        IDataProtectionProvider                     dataProtectionProvider,
        ILogger<CodeSuggestionPersistenceService>   logger)
    {
        _db                     = db;
        _dataProtectionProvider = dataProtectionProvider;
        _logger                 = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FactForAssemblyDto>> GetPlainTextFactsAsync(
        Guid patientId, CancellationToken ct = default)
    {
        // Load all active document IDs for this patient (AIR-S02 scope guard)
        var patientDocIds = await _db.ClinicalDocuments
            .Where(d => d.PatientId == patientId && !d.IsDeleted)
            .Select(d => d.Id)
            .ToListAsync(ct);

        if (patientDocIds.Count == 0)
        {
            _logger.LogInformation(
                "CodeSuggestionPersistenceService: no active documents for patient {PatientId}.", patientId);
            return Array.Empty<FactForAssemblyDto>();
        }

        // Gather all non-deleted facts across patient documents
        var facts = await _db.ExtractedFacts
            .IgnoreQueryFilters()
            .Where(f => !f.IsDeleted && patientDocIds.Contains(f.DocumentId))
            .ToListAsync(ct);

        if (facts.Count == 0)
        {
            _logger.LogInformation(
                "CodeSuggestionPersistenceService: no extracted facts found for patient {PatientId}.", patientId);
            return Array.Empty<FactForAssemblyDto>();
        }

        var factProtector = _dataProtectionProvider.CreateProtector(FactTextPurpose);

        var dtos = new List<FactForAssemblyDto>(facts.Count);
        int errors = 0;

        foreach (var fact in facts)
        {
            string plainText;
            try
            {
                plainText = factProtector.Unprotect(fact.FactText);
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogWarning(ex,
                    "CodeSuggestionPersistenceService: decrypt failed for fact {FactId}; skipping.", fact.Id);
                continue;
            }

            dtos.Add(new FactForAssemblyDto(
                fact.Id,
                fact.DocumentId,
                fact.FactType.ToString(),
                plainText,
                fact.ConfidenceScore,
                fact.SourceCharOffset,
                fact.SourceCharLength));
        }

        if (errors > 0)
            _logger.LogWarning(
                "CodeSuggestionPersistenceService: {ErrorCount}/{Total} fact(s) failed decryption for patient {PatientId}.",
                errors, facts.Count, patientId);

        _logger.LogDebug(
            "CodeSuggestionPersistenceService: loaded {Count} fact(s) for patient {PatientId}.",
            dtos.Count, patientId);

        return dtos;
    }

    /// <inheritdoc />
    public async Task PersistAsync(
        Guid patientId,
        IReadOnlyList<CodeSuggestionResult> results,
        CancellationToken ct = default)
    {
        // DR-017: soft-delete existing active suggestions for idempotent re-generation
        await _db.CodeSuggestions
            .Where(c => c.PatientId == patientId && !c.IsDeleted)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, true), ct);

        if (results.Count == 0)
        {
            _logger.LogInformation(
                "CodeSuggestionPersistenceService: no suggestions to persist for patient {PatientId}.", patientId);
            return;
        }

        var now = DateTime.UtcNow;
        var entities = results.Select(r => new CodeSuggestion
        {
            Id               = Guid.NewGuid(),
            PatientId        = patientId,
            CodeType         = MapCodeType(r.CodeType),
            CodeValue        = r.Code,
            Description      = r.Description,
            ConfidenceScore  = r.ConfidenceScore,
            EvidenceFactIds  = r.EvidenceFactIds.ToArray(),
            StaffReviewed    = false,
            ReviewOutcome    = null,
            ReviewJustification = null,
            IsDeleted        = false,
            CreatedAt        = now,
        }).ToList();

        _db.CodeSuggestions.AddRange(entities);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "CodeSuggestionPersistenceService: persisted {Count} suggestion(s) for patient {PatientId}.",
            entities.Count, patientId);
    }

    private static CodeType MapCodeType(string codeTypeString)
        => codeTypeString.Replace("-", string.Empty, StringComparison.Ordinal)
                         .ToUpperInvariant() switch
        {
            "ICD10" => CodeType.Icd10,
            "CPT"   => CodeType.Cpt,
            _       => throw new ArgumentOutOfRangeException(nameof(codeTypeString),
                            $"Unrecognised code type '{codeTypeString}'.")
        };
}
