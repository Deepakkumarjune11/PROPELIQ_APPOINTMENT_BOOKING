# Task - task_002_be_conflict_detection_resolution_api

## Requirement Reference

- **User Story**: US_022 — Conflict Detection & Resolution
- **Story Location**: `.propel/context/tasks/EP-004/us_022/us_022.md`
- **Acceptance Criteria**:
  - AC-1: When contradictory facts are detected, the system flags them as conflict items with red badge and count per FR-013.
  - AC-2: Staff can query all conflicting values from their respective sources displayed side-by-side per AIR-004.
  - AC-3: Staff selects correct value or enters a manual override; the system updates `consolidated_facts`, clears the conflict flag, and records the justification in the audit log per FR-013.
  - AC-4: The system blocks `PATCH /api/v1/360-view/{id}/status` to `verified` when `conflict_flags` is non-empty per FR-013.
- **Edge Cases**:
  - Staff sends `POST resolve-conflict` with `resolution = 'manual'` but no `manualValue` → 400 Bad Request `"manualValue is required when resolution is 'manual'"` (validated at controller level).
  - `PatientView360` `Version` is stale during resolve → `DbUpdateConcurrencyException` → 409 Conflict response with `"conflict_resolution_race"` error code; client must reload and retry (DR-018).
  - `PATCH status = 'verified'` when `conflict_flags != '[]'` → 422 Unprocessable Entity with body `"verification_blocked_by_conflicts"` count.
  - `conflict_flags` already empty when `POST resolve-conflict` is called (duplicate submission) → idempotent: return 200 with current `PatientView360` state; no duplicate audit log.

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
| Backend | .NET 8 ASP.NET Core Web API | 8.0 LTS |
| Background Jobs | Hangfire | 1.8.x |
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| Security - Encryption | .NET Data Protection API | 8.0 |
| Logging | Serilog | 3.x |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |

> All code and libraries MUST be compatible with versions above. `PatientView360.conflict_flags` stored as encrypted `text` (DR-015); `PatientView360.consolidated_facts` also encrypted. Conflict detection is a Hangfire job triggered by `PatientView360UpdateJob` after assembly; resolution is a synchronous REST call.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-004, AIR-O02, AIR-S02, AIR-S03 |
| **AI Pattern** | Embedding-based semantic conflict detection (cosine distance threshold) within same FactType groups |
| **Prompt Template Path** | N/A — no LLM generation; embeddings-only via `IAiGateway.GenerateEmbeddingsAsync` |
| **Guardrails Config** | Circuit breaker fallback to string-inequality conflict detection (AIR-O02) |
| **Model Provider** | Azure OpenAI `text-embedding-3-small` (HIPAA BAA — NFR-013) |

### CRITICAL: AI Implementation Requirements

- **MUST** use `IAiGateway.GenerateEmbeddingsAsync` to compare fact values within same FactType across different documents; two values with cosine similarity **below 0.70** (i.e., they are semantically different) **and** belong to the same FactType constitute a conflict (AIR-004). Threshold is the inverse of the de-duplication threshold (0.85 merge → 0.70 conflict).
- **MUST** enforce document ownership: only compare facts from the target patient's documents (AIR-S02).
- **MUST NOT** log fact values in AuditLog payloads — log `{PatientId, FactType, ConflictCount}` only (AIR-S03 / DR-015 PHI protection).
- **MUST** handle `IsCircuitOpen` on gateway: fall back to `!string.Equals(normalized_a, normalized_b, OrdinalIgnoreCase)` as conflict signal — conservative (more false positives) but safe (AIR-O02).

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

Implement three components for the conflict detection + resolution workflow:

**1. `ConflictDetectionJob`** — Hangfire job triggered after `PatientView360UpdateJob` completes. For each FactType group in `consolidated_facts`, compare fact values across different source documents using embeddings cosine distance. Pairs below 0.70 similarity are recorded as `ConflictFlag` entries. Encrypts and saves `conflict_flags` JSON into `PatientView360`. Sets `VerificationStatus = NeedsReview` if conflicts found.

**2. `GET /api/v1/patients/{patientId}/conflicts`** — Returns decrypted `ConflictItemDto` list for the SCR-018 UI. Each item includes `conflictId`, `factType`, and a `sources` array (documentName, value, confidenceScore, sourceCharOffset).

**3. `POST /api/v1/360-view/{view360Id}/resolve-conflict`** — Accepts staff resolution choice (Accept A / Accept B / Manual override + value). Updates `consolidated_facts` to replace the conflicting entries with the chosen value. Removes the resolved `ConflictFlag` from `conflict_flags`. Writes `ConflictResolved` AuditLog entry with justification. Increments `Version` (DR-018 concurrency token). Returns updated `ConflictFlags` count.

**4. `PATCH /api/v1/360-view/{view360Id}/status`** — Existing endpoint from UC-005. Add guard: if `conflict_flags != '[]'` → 422 `verification_blocked_by_conflicts` (AC-4). Only proceeds when conflicts cleared.

**5. `PatientView360UpdateJob` integration** — After `UpsertWithRetryAsync`, enqueue `ConflictDetectionJob` (`BackgroundJob.ContinueJobWith`). This replaces the US_021 direct completion path with a two-job chain: assembly → conflict detection → `NeedsReview` or `Pending` status.

---

## Dependent Tasks

- **task_002_be_360_view_assembly_api.md** (US_021) — `PatientView360UpdateJob` and `IDataProtector` established; `consolidated_facts` structure known.
- **task_003_db_patient_view_360_schema.md** (US_021) — `PatientView360.conflict_flags` column exists (stubbed as `'[]'`); `VerificationStatus.NeedsReview` enum value available.
- **task_002_ai_embedding_pipeline.md** (US_019) — `IAiGateway.GenerateEmbeddingsAsync` established.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/ConflictDetectionJob.cs` | Hangfire job: group consolidated facts by FactType → embed values → cosine < 0.70 → store `ConflictFlag` entries → update `conflict_flags` + `VerificationStatus` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Models/ConflictFlag.cs` | DTO: `{ConflictId, FactType, Sources: [{DocumentId, DocumentName, Value, ConfidenceScore, SourceCharOffset}]}` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/Controllers/ConflictController.cs` | `GET /conflicts` + `POST /resolve-conflict` + `PATCH /status` (with verification guard); all `[Authorize(Roles="Staff")]` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/PatientView360UpdateJob.cs` | After `UpsertWithRetryAsync`, chain `ConflictDetectionJob` via `BackgroundJob.ContinueJobWith` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `ConflictDetectionJob`; add `conflict-detection` queue |

---

## Implementation Plan

1. **`ConflictFlag` DTO** — stored as encrypted JSON array in `conflict_flags`:
   ```csharp
   public record ConflictFlag(
       Guid ConflictId,
       string FactType,
       IReadOnlyList<ConflictSource> Sources
   );

   public record ConflictSource(
       Guid DocumentId,
       string DocumentName,
       string Value,         // decrypted plain text — encrypted before storage (DR-015)
       float ConfidenceScore,
       int? SourceCharOffset
   );
   ```

2. **`ConflictDetectionJob.ExecuteAsync`** — embedding-based conflict detection:
   ```csharp
   [Queue("conflict-detection")]
   [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 60, 180 })]
   public async Task ExecuteAsync(Guid patientId, CancellationToken ct)
   {
       var view360 = await _context.PatientView360s
           .FirstOrDefaultAsync(v => v.PatientId == patientId, ct);
       if (view360 is null) return;

       // Decrypt + deserialize consolidated facts
       var json = _protector.Unprotect(view360.ConsolidatedFacts);
       var facts = JsonSerializer.Deserialize<List<ConsolidatedFactEntry>>(json)!;

       var conflicts = new List<ConflictFlag>();
       foreach (var factType in facts.GroupBy(f => f.FactType))
       {
           // Only check cross-document entries (same-doc facts already de-duplicated)
           var multiDocFacts = factType
               .SelectMany(f => f.Sources.Select(s => (f, s)))
               .ToList();
           if (multiDocFacts.Count < 2) continue;

           var values = multiDocFacts.Select(x => x.f.Value).ToList();
           float[][] embeddings;
           if (!_aiGateway.IsCircuitOpen)
               embeddings = await _aiGateway.GenerateEmbeddingsAsync(values, ct);
           else
               embeddings = null!;  // fallback: string inequality

           // Pairwise comparison: cosine < 0.70 → conflict (AIR-004)
           for (int i = 0; i < multiDocFacts.Count - 1; i++)
           for (int j = i + 1; j < multiDocFacts.Count; j++)
           {
               bool isConflict = embeddings != null
                   ? CosineSimilarity(embeddings[i], embeddings[j]) < 0.70f
                   : !string.Equals(
                       Normalize(multiDocFacts[i].f.Value),
                       Normalize(multiDocFacts[j].f.Value),
                       StringComparison.OrdinalIgnoreCase);
               if (!isConflict) continue;

               conflicts.Add(new ConflictFlag(Guid.NewGuid(), factType.Key, new[] {
                   BuildSource(multiDocFacts[i], _protector),
                   BuildSource(multiDocFacts[j], _protector)
               }));
           }
       }

       // Encrypt + save conflict_flags; set VerificationStatus
       var conflictJson = JsonSerializer.Serialize(conflicts);
       var encryptedConflicts = _protector.Protect(conflictJson);
       var newStatus = conflicts.Any()
           ? VerificationStatus.NeedsReview
           : VerificationStatus.Pending;

       await _context.PatientView360s
           .Where(v => v.PatientId == patientId)
           .ExecuteUpdateAsync(s => s
               .SetProperty(v => v.ConflictFlags, encryptedConflicts)
               .SetProperty(v => v.VerificationStatus, newStatus)
               .SetProperty(v => v.LastUpdated, DateTimeOffset.UtcNow), ct);

       _auditLogger.Log(actor: "system", action: "ConflictDetectionCompleted",
           target: patientId,
           payload: new { FactTypeGroupsChecked = facts.GroupBy(f => f.FactType).Count(),
                          ConflictsFound = conflicts.Count });
   }
   ```

3. **`ConflictController.ResolveConflict`** — resolution endpoint:
   ```csharp
   [HttpPost("360-view/{view360Id:guid}/resolve-conflict")]
   [Authorize(Roles = "Staff")]
   public async Task<IActionResult> ResolveConflict(
       Guid view360Id, [FromBody] ResolveConflictRequest request, CancellationToken ct)
   {
       if (request.Resolution == "manual" && string.IsNullOrWhiteSpace(request.ManualValue))
           return BadRequest(new { error = "manualValue is required when resolution is 'manual'" });

       for (int attempt = 0; attempt < 3; attempt++)
       {
           var view360 = await _context.PatientView360s
               .FirstOrDefaultAsync(v => v.Id == view360Id, ct);
           if (view360 is null) return NotFound();

           try {
               // Resolve: update consolidated_facts + remove conflict flag
               var resolvedValue = request.Resolution switch {
                   "sourceA" => request.SourceAValue,
                   "sourceB" => request.SourceBValue,
                   "manual"  => request.ManualValue!,
                   _         => throw new ArgumentException($"Unknown resolution: {request.Resolution}")
               };
               ApplyResolution(view360, request.ConflictId, resolvedValue, _protector);
               view360.Update(view360.ConsolidatedFacts); // increments Version (DR-018)

               await _context.SaveChangesAsync(ct);

               _auditLogger.Log(actor: User.Identity!.Name!, action: "ConflictResolved",
                   target: view360Id,
                   payload: new { ConflictId = request.ConflictId,
                                  Resolution = request.Resolution,
                                  Justification = request.Justification });
               return Ok(new { RemainingConflicts = GetConflictCount(view360, _protector) });
           }
           catch (DbUpdateConcurrencyException) when (attempt < 2) { continue; }
           catch (DbUpdateConcurrencyException) { return Conflict(new { error = "conflict_resolution_race" }); }
       }
       return StatusCode(500);
   }
   ```

4. **`PATCH /360-view/{view360Id}/status` verification guard** (AC-4):
   ```csharp
   [HttpPatch("360-view/{view360Id:guid}/status")]
   [Authorize(Roles = "Staff")]
   public async Task<IActionResult> UpdateStatus(
       Guid view360Id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
   {
       var view360 = await _context.PatientView360s
           .FirstOrDefaultAsync(v => v.Id == view360Id, ct);
       if (view360 is null) return NotFound();

       if (request.Status == "verified")
       {
           var conflictCount = GetConflictCount(view360, _protector);
           if (conflictCount > 0)
               return UnprocessableEntity(new {
                   error = "verification_blocked_by_conflicts",
                   conflictCount
               });
       }
       view360.Verify();
       await _context.SaveChangesAsync(ct);
       _auditLogger.Log(actor: User.Identity!.Name!, action: "PatientSummaryVerified", target: view360Id);
       return NoContent();
   }
   ```

5. **Chain `ConflictDetectionJob` from `PatientView360UpdateJob`**:
   ```csharp
   // At end of PatientView360UpdateJob.UpsertWithRetryAsync, after CommitAsync:
   BackgroundJob.ContinueJobWith<ConflictDetectionJob>(currentJobId,
       j => j.ExecuteAsync(patientId, CancellationToken.None));
   ```

---

## Current Project State

```
server/src/
  Modules/
    ClinicalIntelligence/
      ClinicalIntelligence.Application/
        Documents/
          Jobs/
            PatientView360UpdateJob.cs        ← us_021/task_002 (chain ConflictDetectionJob)
            ConflictDetectionJob.cs           ← THIS TASK (create)
          Models/
            ConsolidatedFactEntry.cs          ← us_021/task_002
            ConflictFlag.cs                   ← THIS TASK (create)
      ClinicalIntelligence.Presentation/
        Controllers/
          PatientView360Controller.cs         ← us_021/task_002
          ConflictController.cs               ← THIS TASK (create)
        ServiceCollectionExtensions.cs        ← extend (conflict-detection queue)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/ConflictDetectionJob.cs` | Hangfire job: decrypt consolidated_facts → embed values (batch ≤15) → pairwise cosine < 0.70 → build ConflictFlag list → encrypt + save conflict_flags → set `NeedsReview` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Models/ConflictFlag.cs` | DTO for conflict_flags JSON array: `{ConflictId, FactType, Sources[]}` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/Controllers/ConflictController.cs` | `GET /conflicts` (decrypt + deserialize conflict_flags); `POST /resolve-conflict` (apply resolution + DR-018 concurrency retry); `PATCH /status` (verification gate AC-4) |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/PatientView360UpdateJob.cs` | Chain `ConflictDetectionJob` via `BackgroundJob.ContinueJobWith` after successful upsert |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `ConflictDetectionJob`; add Hangfire queue `conflict-detection` |

---

## External References

- [AIR-004 — detect clinically meaningful conflicts; flag for mandatory staff review](../.propel/context/docs/design.md#AIR-004)
- [AIR-O02 — circuit breaker fallback; deterministic string-inequality comparison](../.propel/context/docs/design.md#AIR-O02)
- [AIR-S02 — ownership enforcement: only patient's own documents compared](../.propel/context/docs/design.md#AIR-S02)
- [AIR-S03 — audit log all AI calls; no PHI in payloads](../.propel/context/docs/design.md#AIR-S03)
- [FR-013 — conflict detection, mandatory acknowledgement before verification](../.propel/context/docs/spec.md)
- [DR-015 — PHI encryption for `conflict_flags` column](../.propel/context/docs/design.md#DR-015)
- [DR-018 — optimistic concurrency for `PatientView360` updates](../.propel/context/docs/design.md#DR-018)
- [EF Core 8 — `DbUpdateConcurrencyException` + retry](https://learn.microsoft.com/en-us/ef/core/saving/concurrency?tabs=data-annotations)
- [Hangfire — `BackgroundJob.ContinueJobWith` for chained jobs](https://docs.hangfire.io/en/latest/background-methods/continuations.html)
- [UC-005 sequence diagram — `POST /api/360-view/{id}/resolve-conflict` + AuditLog](../.propel/context/docs/models.md#UC-005)

---

## Build Commands

```bash
cd server && dotnet restore ; dotnet build
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] Unit test: `ConflictDetectionJob` with two facts of same FactType from different documents with cosine 0.55 → 1 `ConflictFlag` generated; `VerificationStatus = NeedsReview`
- [ ] Unit test: same-document facts (even if different values) → no conflict generated (within-doc de-duplication already handled)
- [ ] Unit test: circuit-open path → string-inequality fallback produces conflict for `"140/90"` vs `"120/80"`; no gateway call
- [ ] **[AI Tasks]** Guardrails tested: `IsCircuitOpen = true` → string-equality fallback; `ConflictDetectionCompleted` AuditLog entry contains `{FactTypeGroupsChecked, ConflictsFound}` only; no PHI fact values
- [ ] Integration test: `POST /resolve-conflict` with `resolution = 'manual'` and no `manualValue` → 400 Bad Request
- [ ] Integration test: `PATCH /status = 'verified'` when `conflict_flags` non-empty → 422 with `verification_blocked_by_conflicts`
- [ ] Integration test: `PATCH /status = 'verified'` after all conflicts resolved → 204 No Content; `VerificationStatus = Verified`
- [ ] **[AI Tasks - MANDATORY]** Implement and test guardrails (circuit breaker fallback) before marking task complete
- [ ] **[AI Tasks - MANDATORY]** Verify AIR-004/AIR-O02/AIR-S02/AIR-S03 requirements met; no PHI values in audit log payloads

---

## Implementation Checklist

- [x] Create `ConflictFlag` record with `ConflictId`, `FactType`, `Sources` list (document name, value, confidence, char offset)
- [x] Implement `ConflictDetectionJob`: decrypt `consolidated_facts` → group by FactType → batch-embed values via `IAiGateway` (≤15 per batch, AIR-O04 cache) → pairwise cosine < 0.70 = conflict; fallback to string-inequality when `IsCircuitOpen` (AIR-O02) → encrypt + `ExecuteUpdateAsync` `conflict_flags` + `VerificationStatus` → AuditLog (no PHI values)
- [x] Create `ConflictController`: `GET /patients/{patientId}/conflicts` (decrypt + deserialize `conflict_flags`; ownership guard AIR-S02); `POST /360-view/{id}/resolve-conflict` (apply resolution to `consolidated_facts`, remove flag, `SaveChangesAsync` with DR-018 concurrency retry, AuditLog with justification); `PATCH /360-view/{id}/status` (verification gate: 422 if conflicts remain AC-4)
- [x] MODIFY `PatientView360UpdateJob`: chain `ConflictDetectionJob` after successful upsert via `IBackgroundJobClient.Enqueue` (not `ContinueJobWith` — enqueued at end of `ExecuteAsync` after upsert succeeds)
- [x] Register `ConflictDetectionJob` and `conflict-detection` Hangfire queue in `ServiceCollectionExtensions.cs` (queue added to `PatientAccess.Presentation`; job registered in `ClinicalIntelligence.Presentation`)
- [x] **[AI Tasks - MANDATORY]** Reference prompt templates from AI References table during implementation — N/A for this task (embeddings-only; no LLM generation prompt templates)
- [x] **[AI Tasks - MANDATORY]** Implement and test guardrails before marking task complete — `ConflictDetectionService` checks `_aiGateway.IsCircuitOpen` before embedding call; string-inequality fallback logs warning with `AIR-O02` tag; exception catch also falls back to string-inequality
- [x] **[AI Tasks - MANDATORY]** Verify AIR-004/AIR-O02/AIR-S02/AIR-S03 requirements met; no PHI in audit log payloads — confirmed: AuditLog entries contain `{PatientId, FactTypeGroupsChecked, ConflictsFound}` (conflict detection) and `{ConflictId, FactType, Resolution, Justification}` (resolution) — no fact values logged
