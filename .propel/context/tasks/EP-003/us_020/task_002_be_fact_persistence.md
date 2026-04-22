# Task - task_002_be_fact_persistence

## Requirement Reference

- **User Story**: US_020 — RAG Extraction & Fact Persistence
- **Story Location**: `.propel/context/tasks/EP-003/us_020/us_020.md`
- **Acceptance Criteria**:
  - AC-4: When extraction produces facts with confidence ≥ 70%, `ExtractedFact` records are created with `document_id`, `fact_type`, `value`, `confidence_score`, `source_char_offset`, and `source_char_length`; `extraction_status` is set to `completed` per AIR-007.
  - AC-5: When confidence is below 70%, `extraction_status` is set to `manual_review` and the document is flagged for staff review without discarding extracted data per AIR-007.
- **Edge Cases**:
  - All extracted facts have confidence < 70% (all facts rejected) → no `ExtractedFact` rows created; status set to `manual_review`; AuditLog written with `FactCount = 0, Reason = AllFactsBelowThreshold`.
  - Partial confidence split (some ≥ 70%, some < 70%) → persist only confident facts; document status is `completed` if ANY fact meets threshold; `manual_review` only if ALL fail. Document notes low-confidence items in AuditLog payload.
  - Re-processing (re-queued document) → existing `ExtractedFact` rows for `document_id` are soft-deleted before new facts are persisted (idempotent re-run).

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | .NET 8 ASP.NET Core | 8.0 LTS |
| Background Jobs | Hangfire | 1.8.x |
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| Security - Encryption | .NET Data Protection API | 8.0 |
| Logging | Serilog | 3.x |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |

> All code and libraries MUST be compatible with versions above. `ExtractedFact.Value` is a PHI column — encrypted using `.NET Data Protection API` before DB persist per DR-015.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | AIR-007 |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

> This task is deterministic — it persists AI-generated output but makes no AI model calls itself.

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Task Overview

Implement `IFactPersistenceService` and `FactPersistenceService` — the persistence layer called by `FactExtractionJob` (task_001) after successful GPT-4 extraction. This service is responsible for:

1. **Confidence threshold split (AIR-007)**: Partition `ExtractedFactResult` list into `confident` (≥ 0.70) and `lowConfidence` (< 0.70) buckets.
2. **Idempotent cleanup**: Soft-delete existing `ExtractedFact` rows for the `documentId` before inserting new rows (handles re-processing).
3. **PHI encryption (DR-015)**: Encrypt `ExtractedFact.Value` via `.NET Data Protection API` before inserting.
4. **Bulk insert**: Insert all confident `ExtractedFact` rows via `AddRange` + `SaveChangesAsync`.
5. **Status transition**: Set `ClinicalDocument.ExtractionStatus = Completed` if any confident facts; otherwise `ManualReview` (AIR-007).
6. **AuditLog**: Single `FactsExtracted` AuditLog entry with `DocumentId`, `ConfidentCount`, `LowConfidenceCount`, `Status`.
7. **360-view trigger**: Enqueue `PatientView360UpdateJob` (stub for US_021) via `BackgroundJob.Enqueue` after successful completion.

---

## Dependent Tasks

- **task_001_ai_rag_extraction_job.md** (US_020) — calls `IFactPersistenceService.PersistAsync` with the `List<ExtractedFactResult>` output.
- **task_003_db_extracted_fact_schema.md** (US_020) — `ExtractedFact` entity and `extracted_facts` table must exist.
- **task_003_db_clinical_document_schema.md** (US_018) — `ClinicalDocument.ExtractionStatus` must be available for update.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/IFactPersistenceService.cs` | Interface: `PersistAsync(documentId, facts, ct)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/FactPersistenceService.cs` | Full implementation: threshold split → soft-delete old → encrypt value → bulk insert → status update → AuditLog → enqueue 360 job |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/PatientView360UpdateJob.cs` | Hangfire stub for US_021 — sets `PatientView360.LastUpdated`; placeholder body |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `IFactPersistenceService` → `FactPersistenceService`; Hangfire queue `view360-update` |

---

## Implementation Plan

1. **`IFactPersistenceService`** interface:
   ```csharp
   public interface IFactPersistenceService
   {
       /// <summary>
       /// Persists AI-extracted facts with confidence threshold filtering (AIR-007).
       /// Encrypts PHI value column (DR-015). Triggers 360-view update on completion.
       /// </summary>
       Task PersistAsync(
           Guid documentId,
           IReadOnlyList<ExtractedFactResult> facts,
           CancellationToken ct = default);
   }
   ```

2. **`FactPersistenceService.PersistAsync`** — threshold split + bulk insert:
   ```csharp
   public async Task PersistAsync(Guid documentId,
       IReadOnlyList<ExtractedFactResult> facts, CancellationToken ct)
   {
       const float ConfidenceThreshold = 0.70f;  // AIR-007

       var confident = facts.Where(f => f.ConfidenceScore >= ConfidenceThreshold).ToList();
       var lowConf   = facts.Where(f => f.ConfidenceScore <  ConfidenceThreshold).ToList();

       using var tx = await _context.Database.BeginTransactionAsync(ct);

       // Idempotent: soft-delete existing facts for this document (re-processing support)
       await _context.ExtractedFacts
           .Where(f => f.DocumentId == documentId && !f.IsDeleted)
           .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsDeleted, true)
                                     .SetProperty(f => f.DeletedAt, DateTimeOffset.UtcNow), ct);

       // Persist confident facts with encrypted value (DR-015)
       if (confident.Count > 0)
       {
           var entities = confident.Select(f => new ExtractedFact(
               documentId:       documentId,
               factType:         Enum.Parse<FactType>(f.FactType, ignoreCase: true),
               encryptedValue:   _dataProtector.Protect(f.Value),
               confidenceScore:  f.ConfidenceScore,
               sourceCharOffset: f.SourceCharOffset,
               sourceCharLength: f.SourceCharLength
           )).ToList();
           _context.ExtractedFacts.AddRange(entities);
       }

       // Status transition: Completed if any confident facts; ManualReview if none (AIR-007)
       var newStatus = confident.Count > 0
           ? ExtractionDocumentStatus.Completed
           : ExtractionDocumentStatus.ManualReview;

       await _context.ClinicalDocuments
           .Where(d => d.Id == documentId)
           .ExecuteUpdateAsync(s => s.SetProperty(d => d.ExtractionStatus, newStatus), ct);

       await _context.SaveChangesAsync(ct);
       await tx.CommitAsync(ct);

       // AuditLog after commit (immutable per DR-012)
       _auditLogger.Log(actor: "system", action: "FactsExtracted", target: documentId,
           payload: new {
               ConfidentCount = confident.Count,
               LowConfidenceCount = lowConf.Count,
               Status = newStatus.ToString()
           });

       // Trigger 360-view update (stub for US_021)
       if (newStatus == ExtractionDocumentStatus.Completed)
           BackgroundJob.Enqueue<PatientView360UpdateJob>(
               j => j.ExecuteAsync(documentId, CancellationToken.None));
   }
   ```

3. **`PatientView360UpdateJob` stub** — placeholder for US_021:
   ```csharp
   [Queue("view360-update")]
   [AutomaticRetry(Attempts = 3)]
   public class PatientView360UpdateJob(AppDbContext context, ILogger<PatientView360UpdateJob> logger)
   {
       public async Task ExecuteAsync(Guid documentId, CancellationToken ct)
       {
           // TODO US_021: Implement 360-degree view aggregation, de-duplication, and conflict detection.
           // Placeholder: log and exit — 360-view assembly deferred to US_021.
           logger.LogInformation("PatientView360UpdateJob queued for document {DocumentId}. Assembly not yet implemented.", documentId);
           await Task.CompletedTask;
       }
   }
   ```

---

## Current Project State

```
server/src/
  Modules/
    ClinicalIntelligence/
      ClinicalIntelligence.Application/
        Documents/
          Services/
            IFactPersistenceService.cs     ← THIS TASK (create)
            FactPersistenceService.cs      ← THIS TASK (create)
          Jobs/
            FactExtractionJob.cs           ← us_020/task_001 (calls IFactPersistenceService)
            PatientView360UpdateJob.cs     ← THIS TASK (create stub)
      ClinicalIntelligence.Domain/
        Entities/
          ExtractedFact.cs                 ← us_020/task_003 (must exist)
      ClinicalIntelligence.Presentation/
        ServiceCollectionExtensions.cs     ← extend
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/IFactPersistenceService.cs` | Persistence interface with `PersistAsync` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/FactPersistenceService.cs` | Threshold split → soft-delete idempotent cleanup → encrypt value → bulk insert → status update → AuditLog → 360 trigger |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/PatientView360UpdateJob.cs` | Hangfire stub for US_021; `[Queue("view360-update")]`; logs placeholder TODO |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `IFactPersistenceService`, `PatientView360UpdateJob` queue `view360-update` |

---

## External References

- [.NET Data Protection API — `IDataProtector.Protect/Unprotect`](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/using-data-protection?view=aspnetcore-8.0)
- [EF Core 8 — `ExecuteUpdateAsync` for bulk status transitions](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-7.0/whatsnew#executeupdate-and-executedelete-bulk-updates)
- [Hangfire — `BackgroundJob.Enqueue` for chained job dispatch](https://docs.hangfire.io/en/latest/background-methods/calling-methods-in-background.html)
- [AIR-007 — 70% confidence threshold fallback to manual review](../.propel/context/docs/design.md#AIR-007)
- [DR-005 — ExtractedFact entity attributes](../.propel/context/docs/design.md#DR-005)
- [DR-012 — AuditLog immutability (append only, written after commit)](../.propel/context/docs/design.md#DR-012)
- [DR-015 — PHI column encryption for ExtractedFact.Value](../.propel/context/docs/design.md#DR-015)

---

## Build Commands

```bash
cd server && dotnet restore ; dotnet build
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] Unit test: all facts confident (≥ 0.70) → all persisted; status = `Completed`; `PatientView360UpdateJob` enqueued
- [ ] Unit test: all facts low-confidence → no `ExtractedFact` rows inserted; status = `ManualReview`; no 360 job enqueued
- [ ] Unit test: partial mix (2 confident, 1 low) → 2 rows inserted; status = `Completed`; AuditLog `LowConfidenceCount = 1`
- [ ] Unit test: re-processing (existing facts for `documentId`) → old facts soft-deleted before new facts inserted
- [ ] Unit test: `ExtractedFact.Value` is stored encrypted (decrypted value differs from raw stored bytes)
- [ ] Integration test: full `PersistAsync` within DB transaction; rollback on `SaveChangesAsync` exception leaves no partial rows

---

## Implementation Checklist

- [x] Create `IFactPersistenceService` with `PersistAsync(documentId, facts, ct)` signature
- [x] Implement `FactPersistenceService`: confidence threshold split → idempotent hard-delete of old rows via `ExecuteDeleteAsync` → encrypt `value` with `IDataProtector` (DR-015) → `AddRange` confident facts → `ExecuteUpdateAsync` status → `SaveChangesAsync` → `CommitAsync` → AuditLog → enqueue `PatientView360UpdateJob` if `Completed`
- [x] Create `PatientView360UpdateJob` stub with `[Queue("view360-update")]` and `[AutomaticRetry(Attempts=3)]`; body logs `TODO US_021`
- [x] Register `IFactPersistenceService` → `FactPersistenceService` and `PatientView360UpdateJob` in `ServiceCollectionExtensions.cs`
- [x] Verify partial confidence split logic: `Completed` status requires at least 1 fact with score ≥ 0.70; `ManualReview` only when ALL facts below threshold
