# Task - task_002_ai_code_suggestion_job

## Requirement Reference

- **User Story**: US_023 — ICD-10/CPT Code Suggestion & Verification
- **Story Location**: `.propel/context/tasks/EP-004/us_023/us_023.md`
- **Acceptance Criteria**:
  - AC-1: `CodeSuggestionJob` retrieves aggregated patient facts, constructs a token-bounded prompt (≤ 8,000 tokens), and calls GPT-4 Turbo with `ResponseFormat = JsonObject` and `Temperature = 0` per FR-014, AIR-005, AIR-O01.
  - AC-2: GPT-4 Turbo response is schema-validated; invalid JSON triggers a single retry with an error-correction prompt. If retry fails, job logs an error and exits without persisting per AIR-Q04 pattern.
  - AC-3: Each suggested code includes at least one `EvidenceFactId` from the patient's extracted facts; suggestions with zero evidence facts are flagged `ConfidenceScore < 0.50` as a hallucination guard per AIR-Q01.
  - AC-4: All AI calls are audit-logged with `operation = "CodeSuggestionGenerated"`, `patientId`, `tokenCount`, `codeCount` — no PHI in AuditLog payload per AIR-S03.
  - AC-5: If `IAiGateway.IsCircuitOpen` is `true`, job skips GPT call, logs `operation = "CodeSuggestionSkipped"` with `reason = "circuit_open"`, and exits without persisting (deterministic fallback per AIR-O02).
  - AC-6: `GET /api/v1/patients/{patientId}/code-suggestions` returns all `CodeSuggestion` rows for patient ordered by `ConfidenceScore` descending.
  - AC-7: `POST /api/v1/code-suggestions/confirm` sets `StaffReviewed = true`, `ReviewOutcome`, optional `ReviewJustification`, `ReviewedAt = UtcNow`; reject without justification returns 400; audit-logs `operation = "CodeReviewed"` with `reviewOutcome` per FR-014 and NFR-018.
  - AC-8: `PATCH /api/v1/360-view/{id}/status` 422 gate UPDATED to also block finalization when any `CodeSuggestion` row for patient has `StaffReviewed = false` (extends US_022/task_002 guard).
- **Edge Cases**:
  - GPT-4 Turbo returns fewer codes than expected → persist whatever codes returned; no minimum count requirement.
  - Reject with empty `justification` → `400 BadRequest` with `error_code = "justification_required"`.
  - Token budget exceeded (patient has many facts) → `ContextAssembler` trims to most-recent 20 facts with highest `ConfidenceScore`; logs truncation via `ILogger`.
  - Agreement rate metric increment fails (App Insights unavailable) → log warning, continue; do NOT fail the request.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | .NET | 8 LTS |
| API Framework | ASP.NET Core Web API | 8.0 |
| Background Jobs | Hangfire | 1.8.x |
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| AI Provider | Azure OpenAI GPT-4 Turbo | 2024-02-01 |
| Observability | Azure Application Insights SDK | latest |
| Architecture | CQRS + MediatR — ClinicalIntelligence bounded context | — |
| Language | C# | 12 |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-005, AIR-O01, AIR-O02, AIR-Q01, AIR-Q05, AIR-S03 |
| **AI Pattern** | RAG — aggregated `ExtractedFact` rows as context; GPT-4 Turbo for code generation |
| **Prompt Template Path** | `.propel/context/prompts/code-suggestion.md` |
| **Guardrails Config** | Temperature=0; ResponseFormat=JsonObject; TokenBudget≤8,000; ZeroEvidence→ConfidenceScore<0.50 |
| **Model Provider** | Azure OpenAI GPT-4 Turbo (2024-02-01) |

### CRITICAL: AI Safety Guardrails (MUST implement all three)

1. **Token Budget** (AIR-O01): Pass combined patient facts to `ContextAssembler.BuildCodeContext(facts, maxTokens: 8_000)`. Trim to top-20 facts by `ConfidenceScore DESC` if token count exceeds budget. Log truncation count.
2. **Circuit Breaker** (AIR-O02): Check `_aiGateway.IsCircuitOpen` **before** any GPT call. If `true`, skip job, audit-log `reason = "circuit_open"`, return without persisting.
3. **Hallucination Guard** (AIR-Q01): After schema validation, for each code suggestion where `EvidenceFactIds.Count == 0`, set `ConfidenceScore = Math.Min(suggestion.ConfidenceScore, 0.49f)` and log a warning.

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

### Part A — `CodeSuggestionJob` (Hangfire background job)

Add new `CodeSuggestionJob` in `ClinicalIntelligence.Application/Jobs/`. It is chained from `ConflictDetectionJob` completion via `BackgroundJob.ContinueJobWith` (US_022/task_002 MODIFY) **when no unresolved conflicts remain**, or triggered directly when `VerificationStatus = Pending` (no conflicts).

**Pipeline:**
1. Load `ExtractedFact[]` for `patientId` where `IsDeleted = false`, ordered by `ConfidenceScore DESC`.
2. Check circuit breaker → skip if open.
3. Build token-bounded context: `ContextAssembler.BuildCodeContext(facts, maxTokens: 8_000)` — truncate to top-20 by confidence if needed; log truncation.
4. Render prompt from `.propel/context/prompts/code-suggestion.md` (inject context).
5. Call `IChatCompletionGateway.CompleteAsync(prompt, temperature: 0, responseFormat: JsonObject)`.
6. Schema-validate JSON response as `CodeSuggestionResult[]` — single retry with error-correction prompt on failure; exit on second failure.
7. Hallucination guard: clamp confidence to < 0.50 for zero-evidence codes.
8. Persist via `ICodeSuggestionPersistenceService.PersistAsync(patientId, results)`.
9. AuditLog `CodeSuggestionGenerated` with `{patientId, tokenCount, codeCount}`.

### Part B — `ICodeSuggestionPersistenceService.PersistAsync`

- Soft-delete existing `CodeSuggestion` rows for patient (idempotent re-generation support).
- `AddRange` new `CodeSuggestion` entities constructed from `CodeSuggestionResult[]`.
- `SaveChangesAsync` + `CommitAsync`.
- Enqueue `ConflictDetectionJob` is already chained; no further job chaining from here.

### Part C — `CodeSuggestionController` endpoints

**`GET /api/v1/patients/{patientId}/code-suggestions`**
- `[Authorize(Roles = "Staff")]`
- Returns `CodeSuggestionDto[]` ordered by `ConfidenceScore DESC` for patient.
- Includes `EvidenceFacts: [{factId, factSummary}]` projected from `ExtractedFact` rows.

**`POST /api/v1/code-suggestions/confirm`**
- `[Authorize(Roles = "Staff")]`
- Body: `{ codeId: Guid, reviewOutcome: "accepted" | "rejected", justification?: string }`.
- Reject without justification → 400.
- Sets `StaffReviewed = true`, `ReviewOutcome`, `ReviewJustification`, `ReviewedAt = UtcNow`.
- AuditLog `CodeReviewed { codeId, reviewOutcome }`.
- Increment Application Insights custom metrics: `AgreementRate_Total++`; if `reviewOutcome == "accepted"` → `AgreementRate_Agreed++` (AIR-Q05, NFR-018).
- Returns 200 `CodeSuggestionDto`.

### Part D — `PATCH /api/v1/360-view/{id}/status` guard update (MODIFY existing endpoint from US_022/task_002)

Add additional 422 check: if any `CodeSuggestion` for patient has `StaffReviewed = false`, return 422 `{ error_code: "verification_blocked_by_unreviewed_codes" }`.

---

## Dependent Tasks

- **task_002_be_conflict_detection_resolution_api.md** (US_022) — `ConflictDetectionJob` chained here via `BackgroundJob.ContinueJobWith`; `PATCH /360-view/{id}/status` endpoint to MODIFY.
- **task_003_db_code_suggestion_schema.md** (US_023) — `CodeSuggestion` table, `DbSet`, EF Core config must exist.
- **`IAiGateway`** and **`IChatCompletionGateway`** — already registered in DI (US_020/task_001).
- **`ContextAssembler`** — already exists (US_020/task_001); extend with `BuildCodeContext` overload.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Jobs/CodeSuggestionJob.cs` | Hangfire job — circuit check → context build → GPT-4 Turbo → validate → hallucination guard → persist → audit |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Services/ICodeSuggestionPersistenceService.cs` | Interface: `PersistAsync(Guid patientId, IEnumerable<CodeSuggestionResult> results, CancellationToken ct)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Services/CodeSuggestionPersistenceService.cs` | Implementation: soft-delete existing → AddRange → SaveChangesAsync |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/DTOs/CodeSuggestionResult.cs` | GPT response DTO: `CodeType`, `Code`, `Description`, `ConfidenceScore`, `EvidenceFactIds[]` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/Controllers/CodeSuggestionController.cs` | `GET /api/v1/patients/{patientId}/code-suggestions`; `POST /api/v1/code-suggestions/confirm` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Jobs/ConflictDetectionJob.cs` | Add `BackgroundJob.ContinueJobWith<CodeSuggestionJob>(parentJobId)` after conflict_flags persisted (when no conflicts) |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/Controllers/ConflictController.cs` | Extend `PATCH /360-view/{id}/status` 422 gate to also check `unreviewed CodeSuggestion` rows |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Utilities/ContextAssembler.cs` | Add `BuildCodeContext(IEnumerable<ExtractedFact> facts, int maxTokens)` overload |
| CREATE | `.propel/context/prompts/code-suggestion.md` | GPT-4 Turbo system + user prompt template for ICD-10/CPT code generation |

---

## Implementation Plan

1. **`code-suggestion.md` prompt template** — create at `.propel/context/prompts/code-suggestion.md`:
   ```
   ## System
   You are a certified medical coder. Given clinical facts extracted from a patient's records, suggest the most relevant ICD-10 and CPT codes. For each code provide the evidence fact IDs that support it. Output ONLY a JSON array.

   ## Output Schema
   [
     {
       "codeType": "ICD-10" | "CPT",
       "code": "<standard code>",
       "description": "<brief description>",
       "confidenceScore": <float 0.0–1.0>,
       "evidenceFactIds": ["<uuid>", ...]
     }
   ]

   ## Rules
   - Include only codes supported by ≥ 1 evidence fact.
   - ConfidenceScore must reflect strength of evidence.
   - Return an empty array [] if evidence is insufficient.

   ## Patient Facts
   {{PATIENT_FACTS_CONTEXT}}
   ```

2. **`ContextAssembler.BuildCodeContext` overload**:
   ```csharp
   public string BuildCodeContext(IEnumerable<ExtractedFact> facts, int maxTokens)
   {
       var ordered = facts.OrderByDescending(f => f.ConfidenceScore).ToList();
       var sb = new StringBuilder();
       int tokenCount = 0;
       int included = 0;
       foreach (var fact in ordered.Take(20))
       {
           var line = $"[{fact.Id}] [{fact.FactType}] {_dataProtector.Unprotect(fact.Value)} (confidence: {fact.ConfidenceScore:F2})";
           int lineTokens = line.Length / 4; // approx 4 chars/token
           if (tokenCount + lineTokens > maxTokens) break;
           sb.AppendLine(line);
           tokenCount += lineTokens;
           included++;
       }
       if (included < ordered.Count)
           _logger.LogWarning("CodeSuggestionJob: truncated facts from {Total} to {Included}", ordered.Count, included);
       return sb.ToString();
   }
   ```

3. **`CodeSuggestionJob`**:
   ```csharp
   [Queue("code-suggestion")]
   [AutomaticRetry(Attempts = 2)]
   [DisableConcurrentExecution(timeoutInSeconds: 300)]
   public class CodeSuggestionJob(
       IDbContext dbContext,
       IChatCompletionGateway chatGateway,
       IAiGateway aiGateway,
       ICodeSuggestionPersistenceService persistenceService,
       ContextAssembler contextAssembler,
       IAuditLogger auditLogger,
       ILogger<CodeSuggestionJob> logger)
   {
       public async Task ExecuteAsync(Guid patientId, CancellationToken ct = default)
       {
           if (aiGateway.IsCircuitOpen)
           {
               await auditLogger.LogAsync("CodeSuggestionSkipped", patientId, new { reason = "circuit_open" }, ct);
               logger.LogWarning("CodeSuggestionJob skipped for {PatientId}: circuit open", patientId);
               return;
           }

           var facts = await dbContext.ExtractedFacts
               .Where(f => f.PatientId == patientId && !f.IsDeleted)
               .OrderByDescending(f => f.ConfidenceScore)
               .ToListAsync(ct);

           var context = contextAssembler.BuildCodeContext(facts, maxTokens: 8_000);
           var prompt = CodeSuggestionPromptBuilder.Build(context);

           CodeSuggestionResult[]? results = null;
           var response = await chatGateway.CompleteAsync(prompt, temperature: 0f, responseFormat: ResponseFormat.JsonObject, ct);
           results = TryDeserialize(response);
           if (results is null)
           {
               logger.LogWarning("CodeSuggestionJob: schema validation failed for {PatientId}, retrying once", patientId);
               var retryPrompt = CodeSuggestionPromptBuilder.BuildRetry(context, response);
               var retryResponse = await chatGateway.CompleteAsync(retryPrompt, temperature: 0f, responseFormat: ResponseFormat.JsonObject, ct);
               results = TryDeserialize(retryResponse);
           }
           if (results is null)
           {
               logger.LogError("CodeSuggestionJob: schema validation failed after retry for {PatientId}", patientId);
               return;
           }

           // Hallucination guard (AIR-Q01)
           foreach (var r in results.Where(r => r.EvidenceFactIds.Count == 0))
           {
               r.ConfidenceScore = Math.Min(r.ConfidenceScore, 0.49f);
               logger.LogWarning("CodeSuggestionJob: zero-evidence code {Code} for patient {PatientId} clamped to low confidence", r.Code, patientId);
           }

           await persistenceService.PersistAsync(patientId, results, ct);
           await auditLogger.LogAsync("CodeSuggestionGenerated", patientId,
               new { tokenCount = context.Length / 4, codeCount = results.Length }, ct);
       }

       private static CodeSuggestionResult[]? TryDeserialize(string json)
       {
           try { return JsonSerializer.Deserialize<CodeSuggestionResult[]>(json); }
           catch { return null; }
       }
   }
   ```

4. **`POST /api/v1/code-suggestions/confirm`** with Application Insights metrics:
   ```csharp
   [HttpPost("confirm")]
   [Authorize(Roles = "Staff")]
   public async Task<IActionResult> ConfirmCode([FromBody] ConfirmCodeRequest request, CancellationToken ct)
   {
       if (request.ReviewOutcome == ReviewOutcome.Rejected && string.IsNullOrWhiteSpace(request.Justification))
           return BadRequest(new { error_code = "justification_required" });

       var suggestion = await _dbContext.CodeSuggestions.FindAsync([request.CodeId], ct);
       if (suggestion is null) return NotFound();

       suggestion.Review(request.ReviewOutcome, request.Justification);
       await _dbContext.SaveChangesAsync(ct);

       await _auditLogger.LogAsync("CodeReviewed", suggestion.PatientId,
           new { codeId = suggestion.Id, reviewOutcome = request.ReviewOutcome }, ct);

       _telemetry.TrackMetric("AgreementRate_Total", 1);
       if (request.ReviewOutcome == ReviewOutcome.Accepted)
           _telemetry.TrackMetric("AgreementRate_Agreed", 1);

       return Ok(_mapper.Map<CodeSuggestionDto>(suggestion));
   }
   ```

5. **`PATCH /360-view/{id}/status` guard update** (MODIFY `ConflictController.cs`):
   ```csharp
   // After existing conflict_flags check — add:
   bool hasUnreviewedCodes = await _dbContext.CodeSuggestions
       .AnyAsync(c => c.PatientId == view360.PatientId && !c.StaffReviewed, ct);
   if (hasUnreviewedCodes)
       return UnprocessableEntity(new { error_code = "verification_blocked_by_unreviewed_codes" });
   ```

---

## Current Project State

```
server/src/Modules/ClinicalIntelligence/
  ClinicalIntelligence.Application/
    Jobs/
      FactExtractionJob.cs                              ← us_020/task_001
      ConflictDetectionJob.cs                           ← us_022/task_002 (MODIFY — add ContinueJobWith)
      CodeSuggestionJob.cs                              ← THIS TASK (create)
    Services/
      IFactPersistenceService.cs                        ← us_020/task_002
      FactPersistenceService.cs                         ← us_020/task_002
      ICodeSuggestionPersistenceService.cs              ← THIS TASK (create)
      CodeSuggestionPersistenceService.cs               ← THIS TASK (create)
    DTOs/
      ExtractedFactResult.cs                            ← us_020/task_001
      ConflictFlag.cs                                   ← us_022/task_002
      CodeSuggestionResult.cs                           ← THIS TASK (create)
    Utilities/
      ContextAssembler.cs                               ← us_020/task_001 (MODIFY — add BuildCodeContext)
  ClinicalIntelligence.Presentation/
    Controllers/
      ConflictController.cs                             ← us_022/task_002 (MODIFY — extend 422 gate)
      CodeSuggestionController.cs                       ← THIS TASK (create)
.propel/context/prompts/
  clinical-fact-extraction.md                          ← us_020/task_001
  code-suggestion.md                                   ← THIS TASK (create)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/.../Jobs/CodeSuggestionJob.cs` | Hangfire job — circuit check → context → GPT-4 Turbo → validate → hallucination guard → persist → audit |
| CREATE | `server/.../Services/ICodeSuggestionPersistenceService.cs` | Interface with `PersistAsync` |
| CREATE | `server/.../Services/CodeSuggestionPersistenceService.cs` | Soft-delete → AddRange → SaveChangesAsync |
| CREATE | `server/.../DTOs/CodeSuggestionResult.cs` | GPT response schema DTO |
| CREATE | `server/.../Controllers/CodeSuggestionController.cs` | GET suggestions + POST confirm |
| MODIFY | `server/.../Jobs/ConflictDetectionJob.cs` | Add `BackgroundJob.ContinueJobWith<CodeSuggestionJob>` when no conflicts |
| MODIFY | `server/.../Controllers/ConflictController.cs` | Extend 422 gate: unreviewed codes block finalization |
| MODIFY | `server/.../Utilities/ContextAssembler.cs` | Add `BuildCodeContext` overload (top-20 by confidence, token-budget trimming) |
| CREATE | `.propel/context/prompts/code-suggestion.md` | System + user prompt template with `{{PATIENT_FACTS_CONTEXT}}` placeholder |

---

## External References

- [Azure OpenAI GPT-4 Turbo — ResponseFormat JsonObject](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/json-mode)
- [Hangfire — `BackgroundJob.ContinueJobWith`](https://docs.hangfire.io/en/latest/background-methods/continuations.html)
- [Hangfire — `[AutomaticRetry]` + `[DisableConcurrentExecution]`](https://docs.hangfire.io/en/latest/background-processing/throttling.html)
- [Azure Application Insights SDK — `TelemetryClient.TrackMetric`](https://learn.microsoft.com/en-us/azure/azure-monitor/app/api-custom-events-metrics#trackmetric)
- [AIR-005 — ICD-10/CPT suggestion from aggregated facts](../.propel/context/docs/design.md)
- [AIR-Q01 — hallucination rate < 2%](../.propel/context/docs/design.md)
- [AIR-Q05 — AI-human agreement > 98%](../.propel/context/docs/design.md)
- [AIR-O01 — token budget ≤ 8,000](../.propel/context/docs/design.md)
- [AIR-O02 — Polly circuit breaker](../.propel/context/docs/design.md)
- [AIR-S03 — AuditLog all AI calls, no PHI in payload](../.propel/context/docs/design.md)
- [FR-014 — ICD-10/CPT code suggestion workflow with mandatory human confirmation](../.propel/context/docs/spec.md)
- [NFR-018 — operational metrics in Azure Application Insights](../.propel/context/docs/spec.md)

---

## Build Commands

```bash
cd server
dotnet restore PropelIQ.slnx
dotnet build PropelIQ.slnx --configuration Debug
```

---

## Implementation Validation Strategy

- [ ] Unit test: `CodeSuggestionJob.ExecuteAsync` with `IsCircuitOpen = true` → verify `PersistAsync` NOT called; AuditLog contains `reason = "circuit_open"`
- [ ] Unit test: GPT response with valid JSON → results persisted; AuditLog `CodeSuggestionGenerated` includes `codeCount`
- [ ] Unit test: GPT response with invalid JSON → single retry fired; second invalid JSON → return without persist; no exception thrown
- [ ] Unit test: code with `EvidenceFactIds.Count == 0` → `ConfidenceScore` clamped to ≤ 0.49
- [ ] Unit test: `ConfirmCode` with `reviewOutcome = "rejected"` and empty `justification` → 400 returned
- [ ] Unit test: `PATCH /360-view/{id}/status` with unreviewed code rows → 422 `verification_blocked_by_unreviewed_codes`
- [ ] **[AI Tasks - MANDATORY]** Verify all 3 AI safety guardrails are implemented: token budget (8,000), circuit breaker exit, zero-evidence confidence clamp
- [ ] **[AI Tasks - MANDATORY]** Verify AuditLog payload contains no PHI values (patient name, code values must not appear in log payload)
- [ ] **[AI Tasks - MANDATORY]** Verify Application Insights `AgreementRate_Total` incremented on every confirm; `AgreementRate_Agreed` incremented only on accepted

---

## Implementation Checklist

- [x] Create `CodeSuggestionJob.cs`: `[Queue("code-suggestion")]`; `IsCircuitOpen` check first; `BuildCodeContext(facts, 8_000)` with top-20 truncation + ILogger warning; GPT-4 Turbo `Temperature=0, JsonObject`; `TryDeserialize` + single retry; hallucination guard (zero-evidence → ConfidenceScore < 0.50); `PersistAsync`; AuditLog `CodeSuggestionGenerated`
- [x] Create `ICodeSuggestionPersistenceService` + `CodeSuggestionPersistenceService`: soft-delete existing rows → `AddRange` → `SaveChangesAsync + CommitAsync`
- [x] Create `.propel/context/prompts/code-suggestion.md` with system prompt, output schema, rules, and `{{PATIENT_FACTS_CONTEXT}}` placeholder
- [x] Create `CodeSuggestionController`: `GET /patients/{patientId}/code-suggestions` (ordered by `ConfidenceScore DESC`, include evidence facts projection); `POST /code-suggestions/confirm` (reject-without-justification → 400; `StaffReviewed = true`; AuditLog; structured log metrics `AgreementRate_Total` + conditional `AgreementRate_Agreed`)
- [x] Modify `ConflictDetectionJob`: changed `DetectAndSaveAsync` to return `Guid` (patientId); enqueue `CodeSuggestionJob` via `IBackgroundJobClient` after conflict detection completes
- [x] Modify `ConflictController` `PATCH /360-view/{id}/status`: add 422 gate for unreviewed `CodeSuggestion` rows (`verification_blocked_by_unreviewed_codes`)
- [x] Extend `ContextAssembler` with `BuildCodeContext` overload (plain-text `FactForAssemblyDto` input, top-20 by confidence, ~4-char/token estimate, truncation log)
- [x] Add `Description`, `ConfidenceScore`, `ReviewJustification`, `IsDeleted` to `CodeSuggestion` entity + EF config + migration (`AddCodeSuggestionFields`)
- [x] Register `ICodeSuggestionPersistenceService`, `CodeSuggestionPersistenceService`, `CodeSuggestionJob` in `ClinicalIntelligence.Presentation.ServiceCollectionExtensions`
- [x] Add `"code-suggestion"` to Hangfire queues array in `PatientAccess.Presentation.ServiceCollectionExtensions`
- [x] **[AI Tasks - MANDATORY]** Validate all 3 AI safety guardrails: token-budget enforcement (`BuildCodeContext` top-20 + budget cap), circuit-breaker exit path (returns immediately when `IsCircuitOpen`), hallucination confidence clamp (filters `ConfidenceScore < 0.50` and zero-evidence suggestions)
