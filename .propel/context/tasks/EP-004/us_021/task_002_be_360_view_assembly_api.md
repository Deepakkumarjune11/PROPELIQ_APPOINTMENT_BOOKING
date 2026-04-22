# Task - task_002_be_360_view_assembly_api

## Requirement Reference

- **User Story**: US_021 — 360-Degree Patient View Assembly
- **Story Location**: `.propel/context/tasks/EP-004/us_021/us_021.md`
- **Acceptance Criteria**:
  - AC-1: The system assembles a consolidated 360-degree view with de-duplicated facts grouped by category (vitals, medications, history, diagnoses, procedures) per FR-012.
  - AC-2: The system de-duplicates overlapping facts using semantic similarity and entity resolution, presenting each consolidated value with all contributing source references per AIR-003.
  - AC-4: Optimistic concurrency control via version field prevents race conditions when concurrent staff members update `PatientView360` per DR-018.
- **Edge Cases**:
  - No `ExtractedFact` records for patient → `PatientView360.consolidated_facts` is an empty object `{}`; `GET 360-view` returns 200 with empty categories; no error thrown.
  - All facts from a single document (no cross-document duplicates) → embedding cosine similarity not needed; all facts pass through unchanged; assembly still completes.
  - `DbUpdateConcurrencyException` on `PatientView360` upsert → retry up to 3 times with fresh row read; 4th failure → log warning + skip (view will be rebuilt on next document completion).
  - `IAiGateway.GenerateEmbeddingsAsync` circuit open during de-duplication → fall back to string-equality-only de-duplication (normalised lowercase comparison); AuditLog `PatientView360AssembledWithFallback`.

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
| AI/ML - Embeddings | Azure OpenAI `text-embedding-3-small` | latest |
| AI/ML - Gateway | Custom .NET Middleware (`IAiGateway`) | custom |
| Security - Encryption | .NET Data Protection API | 8.0 |
| State Management | Zustand (store, N/A server-side) | 4.x |
| Logging | Serilog | 3.x |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |

> All code and libraries MUST be compatible with versions above. `PatientView360.consolidated_facts` is a PHI JSONB column encrypted at application layer via `.NET Data Protection API` (DR-015). De-duplication uses `IAiGateway.GenerateEmbeddingsAsync` with `text-embedding-3-small` (same gateway established in US_019/task_002).

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-003, AIR-O02, AIR-O04, AIR-S02, AIR-S03 |
| **AI Pattern** | Embedding-based semantic similarity (de-duplication within FactType groups) |
| **Prompt Template Path** | N/A — no LLM generation; embeddings-only |
| **Guardrails Config** | Circuit breaker fallback to string-equality de-duplication (AIR-O02); no GPT-4 call |
| **Model Provider** | Azure OpenAI `text-embedding-3-small` (HIPAA BAA — NFR-013) |

### CRITICAL: AI Implementation Requirements

- **MUST** enforce document ownership — only query `ExtractedFact` rows belonging to the target patient's documents (AIR-S02). Staff scope: staff can assemble any patient's view, but the data must come from that patient's `ClinicalDocument` records only.
- **MUST** call `IAiGateway.GenerateEmbeddingsAsync` for fact-value de-duplication within each FactType group (AIR-003). Only cross-document facts (same FactType, different `DocumentId`) need semantic comparison — within-document facts are already deduplicated at extraction time.
- **MUST** cache embeddings used in de-duplication via Redis (AIR-O04) — the gateway handles this transparently through SHA256-keyed 7-day TTL cache.
- **MUST** log `PatientView360AssemblyCompleted` to AuditLog after each successful assembly: `{PatientId, FactCount, DeduplicatedCount, Duration_ms}` — no PHI values in payload (AIR-S03).
- **MUST** handle `IsCircuitOpen` on `IAiGateway`: fall back to normalised string-equality de-duplication without crashing; AuditLog `PatientView360AssembledWithFallback` (AIR-O02).

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

Implement the full `PatientView360UpdateJob` (replacing the US_020 stub) and two REST endpoints for the staff verification workflow.

**`PatientView360UpdateJob.ExecuteAsync(Guid documentId)` — 5-stage pipeline:**
1. **Resolve patient**: Load `ClinicalDocument.PatientId` for the given `documentId`.
2. **Gather facts**: Query all non-deleted `ExtractedFact` rows across all of that patient's `ClinicalDocument` records.
3. **De-duplicate per FactType (AIR-003)**:
   - Group facts by `FactType`.
   - Within each group, if facts come from different documents: call `IAiGateway.GenerateEmbeddingsAsync` on decrypted values (batch ≤ 15); compute pairwise cosine similarity; merge fact pairs with similarity ≥ 0.85 (keep higher `ConfidenceScore`; union `Sources` list).
   - Circuit open fallback: use `string.Equals(normalized_a, normalized_b, OrdinalIgnoreCase)` instead.
4. **Serialize + encrypt**: Build `Dictionary<FactType, List<ConsolidatedFactEntry>>` → serialize as JSON → encrypt via `IDataProtector.Protect`.
5. **Upsert with optimistic concurrency (DR-018)**: Update `PatientView360.ConsolidatedFacts` + increment `Version`; catch `DbUpdateConcurrencyException`, reload row, retry up to 3 times; AuditLog after commit.

**REST endpoints (ClinicalIntelligence.Presentation, `[Authorize(Roles="Staff")]`):**
- `GET /api/v1/patients/{patientId}/360-view` — returns `PatientView360Dto` (decrypted + deserialized consolidated facts grouped by category).
- `GET /api/v1/facts/{factId}/source` — returns `SourceCitationDto` (source text + `sourceCharOffset` + `sourceCharLength` + document name) for citation drawer.

---

## Dependent Tasks

- **task_002_be_fact_persistence.md** (US_020) — `PatientView360UpdateJob` stub must exist; `ExtractedFact` records must be persisted.
- **task_003_db_patient_view_360_schema.md** (US_021) — `PatientView360` entity + `patient_view_360` table must exist.
- **task_002_ai_embedding_pipeline.md** (US_019) — `IAiGateway.GenerateEmbeddingsAsync` must be implemented.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/PatientView360UpdateJob.cs` | Replace US_020 stub with full assembly pipeline (AIR-003 de-duplication + optimistic concurrency upsert) |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/PatientView360Assembler.cs` | Stateless service: de-duplicate facts by FactType using embeddings cosine similarity ≥ 0.85 |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Models/ConsolidatedFactEntry.cs` | DTO: `{FactType, Value, ConfidenceScore, Sources: [{DocumentId, SourceCharOffset, SourceCharLength, DocumentName}]}` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/Controllers/PatientView360Controller.cs` | `GET /api/v1/patients/{patientId}/360-view` + `GET /api/v1/facts/{factId}/source`; `[Authorize(Roles="Staff")]` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `PatientView360Assembler` as scoped |

---

## Implementation Plan

1. **`ConsolidatedFactEntry` DTO** — canonical shape for JSONB storage and API response:
   ```csharp
   public record ConsolidatedFactEntry(
       string FactType,
       string Value,              // decrypted plain text (PHI, encrypted before storage)
       float ConfidenceScore,
       IReadOnlyList<FactSourceRef> Sources
   );

   public record FactSourceRef(
       Guid DocumentId,
       string DocumentName,
       int? SourceCharOffset,
       int? SourceCharLength
   );
   ```

2. **`PatientView360Assembler.DeduplicateAsync`** — semantic similarity pass:
   ```csharp
   public async Task<List<ConsolidatedFactEntry>> DeduplicateAsync(
       IReadOnlyList<ExtractedFact> facts, IDataProtector protector,
       IReadOnlyDictionary<Guid, string> docNames, CancellationToken ct)
   {
       var result = new List<ConsolidatedFactEntry>();
       var byType = facts.GroupBy(f => f.FactType);
       foreach (var group in byType)
       {
           var decrypted = group
               .Select(f => (fact: f, value: protector.Unprotect(f.Value)))
               .ToList();

           // Only call embeddings when multiple documents contribute to same FactType
           var multiDoc = decrypted.Select(x => x.fact.DocumentId).Distinct().Count() > 1;
           IReadOnlyList<ConsolidatedFactEntry> consolidated;
           if (multiDoc && !_aiGateway.IsCircuitOpen)
               consolidated = await SemanticDeduplicateAsync(decrypted, docNames, ct);
           else
               consolidated = StringDeduplicateAsync(decrypted, docNames);

           result.AddRange(consolidated);
       }
       return result;
   }

   private async Task<IReadOnlyList<ConsolidatedFactEntry>> SemanticDeduplicateAsync(...) {
       // 1. Embed values in batch ≤15 (AIR-O01 limit; fact values are short, << 8000 tokens)
       var embeddings = await _aiGateway.GenerateEmbeddingsAsync(
           decrypted.Select(x => x.value).ToList(), ct);
       // 2. Union-Find / greedy merge: cosine similarity ≥ 0.85 → merge into single entry
       // 3. Keep highest ConfidenceScore; union Sources lists
       // ...
   }
   ```

3. **`PatientView360UpdateJob.ExecuteAsync`** — full pipeline:
   ```csharp
   [Queue("view360-update")]
   [AutomaticRetry(Attempts = 3)]
   [DisableConcurrentExecution(60)]
   public async Task ExecuteAsync(Guid documentId, CancellationToken ct)
   {
       // Stage 1: resolve patient
       var doc = await _context.ClinicalDocuments.FindAsync(new object[] { documentId }, ct)
           ?? throw new InvalidOperationException($"Document {documentId} not found");
       var patientId = doc.PatientId;

       // Stage 2: gather all non-deleted facts for patient (AIR-S02 ownership)
       var facts = await _context.ExtractedFacts
           .Where(f => !f.IsDeleted && _context.ClinicalDocuments
               .Where(d => d.PatientId == patientId).Select(d => d.Id).Contains(f.DocumentId))
           .ToListAsync(ct);

       // Stage 3: de-duplicate (AIR-003)
       var docNames = await _context.ClinicalDocuments
           .Where(d => d.PatientId == patientId)
           .ToDictionaryAsync(d => d.Id, d => d.FileName, ct);
       var consolidated = await _assembler.DeduplicateAsync(facts, _protector, docNames, ct);

       // Stage 4: serialize + encrypt
       var json = JsonSerializer.Serialize(consolidated);
       var encryptedJson = _protector.Protect(json);

       // Stage 5: upsert with optimistic concurrency (DR-018)
       await UpsertWithRetryAsync(patientId, encryptedJson, ct);

       _auditLogger.Log(actor: "system", action: "PatientView360AssemblyCompleted",
           target: patientId,
           payload: new { FactCount = facts.Count, DeduplicatedCount = consolidated.Count });
   }
   ```

4. **Optimistic concurrency upsert** (DR-018):
   ```csharp
   private async Task UpsertWithRetryAsync(Guid patientId, string encryptedJson, CancellationToken ct)
   {
       for (int attempt = 0; attempt < 3; attempt++)
       {
           try {
               var view360 = await _context.PatientView360s
                   .FirstOrDefaultAsync(v => v.PatientId == patientId, ct);
               if (view360 is null) {
                   _context.PatientView360s.Add(new PatientView360(patientId, encryptedJson));
               } else {
                   view360.Update(encryptedJson);  // increments Version internally
               }
               await _context.SaveChangesAsync(ct);
               return;
           }
           catch (DbUpdateConcurrencyException) when (attempt < 2) {
               _logger.LogWarning("PatientView360 concurrency conflict for patient {PatientId}, retry {Attempt}", patientId, attempt + 1);
           }
       }
       _logger.LogWarning("PatientView360 upsert gave up after 3 retries for patient {PatientId}", patientId);
   }
   ```

5. **`PatientView360Controller`** — REST endpoints:
   ```csharp
   [ApiController]
   [Route("api/v1")]
   [Authorize(Roles = "Staff")]
   public class PatientView360Controller(AppDbContext context, IDataProtector protector, ...) : ControllerBase
   {
       [HttpGet("patients/{patientId:guid}/360-view")]
       public async Task<IActionResult> Get360View(Guid patientId, CancellationToken ct)
       {
           var view360 = await context.PatientView360s
               .FirstOrDefaultAsync(v => v.PatientId == patientId, ct);
           if (view360 is null) return NotFound();
           var json = protector.Unprotect(view360.ConsolidatedFacts);
           var facts = JsonSerializer.Deserialize<List<ConsolidatedFactEntry>>(json)!;
           return Ok(new PatientView360Dto(patientId, GroupByCategory(facts), view360.VerificationStatus.ToString()));
       }

       [HttpGet("facts/{factId:guid}/source")]
       public async Task<IActionResult> GetFactSource(Guid factId, CancellationToken ct)
       {
           var fact = await context.ExtractedFacts
               .Include(f => f.Document)
               .FirstOrDefaultAsync(f => f.Id == factId && !f.IsDeleted, ct);
           if (fact is null) return NotFound();
           // Source text comes from the chunk containing this fact's char offset
           var chunk = await context.DocumentChunkEmbeddings
               .Where(c => c.DocumentId == fact.DocumentId &&
                           c.ChunkIndex == EstimateChunkIndex(fact.SourceCharOffset))
               .Select(c => c.ChunkText)
               .FirstOrDefaultAsync(ct) ?? string.Empty;
           return Ok(new SourceCitationDto(chunk, fact.SourceCharOffset, fact.SourceCharLength,
               fact.Document.FileName));
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
          Jobs/
            PatientView360UpdateJob.cs     ← MODIFY (replace us_020 stub with full pipeline)
          Services/
            PatientView360Assembler.cs     ← THIS TASK (create)
          Models/
            ConsolidatedFactEntry.cs       ← THIS TASK (create)
      ClinicalIntelligence.Presentation/
        Controllers/
          PatientView360Controller.cs      ← THIS TASK (create)
        ServiceCollectionExtensions.cs     ← extend
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/PatientView360UpdateJob.cs` | Full pipeline: resolve patient → gather facts → AIR-003 de-duplicate → encrypt → upsert with DR-018 concurrency retry → AuditLog |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/PatientView360Assembler.cs` | Semantic de-duplication (embedding cosine ≥ 0.85) + string-equality fallback when circuit open |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Models/ConsolidatedFactEntry.cs` | DTO used for JSONB serialization and API responses |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/Controllers/PatientView360Controller.cs` | Two endpoints: `GET 360-view` (decrypt + deserialize + group) + `GET facts/{id}/source` (citation drawer data) |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `PatientView360Assembler` as scoped |

---

## External References

- [AIR-003 — 360-degree de-duplication via semantic similarity + entity resolution](../.propel/context/docs/design.md#AIR-003)
- [AIR-O02 — circuit breaker + fallback to deterministic mode](../.propel/context/docs/design.md#AIR-O02)
- [AIR-O04 — embedding cache (SHA256 keyed, 7-day TTL via Redis)](../.propel/context/docs/design.md#AIR-O04)
- [AIR-S02 — ownership enforcement in RAG retrieval](../.propel/context/docs/design.md#AIR-S02)
- [AIR-S03 — audit log for all AI calls](../.propel/context/docs/design.md#AIR-S03)
- [DR-006 — PatientView360 entity definition: JSONB consolidated_facts, conflict_flags, verification_status](../.propel/context/docs/design.md#DR-006)
- [DR-015 — PHI column encryption for PatientView360.consolidated_facts](../.propel/context/docs/design.md#DR-015)
- [DR-018 — optimistic concurrency for PatientView360 updates](../.propel/context/docs/design.md#DR-018)
- [EF Core 8 — `DbUpdateConcurrencyException` handling with retry](https://learn.microsoft.com/en-us/ef/core/saving/concurrency?tabs=data-annotations)
- [Hangfire — `[DisableConcurrentExecution]` attribute for job exclusivity](https://docs.hangfire.io/en/latest/background-processing/throttling.html)

---

## Build Commands

```bash
cd server && dotnet restore ; dotnet build
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] Unit test: `PatientView360Assembler.DeduplicateAsync` with two facts of same FactType from different documents and cosine similarity 0.90 → merged into one entry with both sources
- [ ] Unit test: circuit-open path → falls back to string-equality de-duplication without throwing
- [ ] Unit test: `UpsertWithRetryAsync` with first `DbUpdateConcurrencyException` → retries and succeeds on second attempt
- [ ] **[AI Tasks]** Prompt templates validated with test inputs (N/A — no LLM prompt; embeddings model only)
- [ ] **[AI Tasks]** Guardrails tested: circuit-open fallback path produces valid `PatientView360` record; AuditLog `PatientView360AssembledWithFallback` entry written
- [ ] **[AI Tasks]** `GET 360-view` returns 200 with decrypted grouped facts; no PHI in AuditLog payload (document IDs only)
- [ ] Integration test: `GET /api/v1/patients/{patientId}/360-view` requires `Staff` role; 401 without auth; 403 with `Patient` role

---

## Implementation Checklist

- [x] MODIFY `PatientView360UpdateJob.ExecuteAsync`: resolve `patientId` from `documentId` → gather all patient's non-deleted `ExtractedFact` records (AIR-S02 ownership) → call `PatientView360Assembler.DeduplicateAsync` → serialize + encrypt consolidated facts → call `UpsertWithRetryAsync` → AuditLog `PatientView360AssemblyCompleted`
- [x] Implement `PatientView360Assembler.DeduplicateAsync`: group by FactType; for multi-document groups, batch-embed values via `IAiGateway.GenerateEmbeddingsAsync` (≤15 per batch, AIR-O04 cache); union-find merge on cosine ≥ 0.85; fallback to `OrdinalIgnoreCase` equality when `IsCircuitOpen` (AIR-O02); keep higher `ConfidenceScore` in merged entry
- [x] Implement `UpsertWithRetryAsync`: 3-retry loop on `DbUpdateConcurrencyException`; reload row before each retry; log warning on final give-up; never rethrow (DR-018)
- [x] Create `PatientView360Controller` with `GET patients/{patientId}/360-view` (decrypt + deserialize → `PatientView360Dto`) and `GET facts/{factId}/source` (return `SourceCitationDto`); both `[Authorize(Roles="Staff")]`; AuditLog `PatientView360Viewed` on GET
- [x] Register `PatientView360Assembler` in `ServiceCollectionExtensions.cs`
- [x] **[AI Tasks - MANDATORY]** Reference prompt templates from AI References table during implementation
- [x] **[AI Tasks - MANDATORY]** Implement and test guardrails (circuit breaker fallback) before marking task complete
- [x] **[AI Tasks - MANDATORY]** Verify AIR-003/AIR-O02/AIR-O04/AIR-S02/AIR-S03 requirements met; no PHI values in audit log payloads
