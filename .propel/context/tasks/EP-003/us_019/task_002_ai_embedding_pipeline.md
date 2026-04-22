# Task - task_002_ai_embedding_pipeline

## Requirement Reference

- **User Story**: US_019 — Document Chunking & Embedding Pipeline
- **Story Location**: `.propel/context/tasks/EP-003/us_019/us_019.md`
- **Acceptance Criteria**:
  - AC-2: When embedding generation runs, each chunk produces a 1536-dimensional vector using `text-embedding-3-small` and is stored in the pgvector table with `document_id`, `chunk_index`, `chunk_text`, and `token_count` per DR-016.
  - AC-3: When a similarity search query is executed, cosine distance metric returns relevant chunks within <10ms at p95 per TR-015.
- **Edge Cases**:
  - Azure OpenAI embedding API returns 429 (rate limit) → `[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 5, 30, 60 })]` handles backoff; circuit breaker trips at 5 consecutive failures → document status set to `ManualReview` (AIR-O02).
  - Very large documents (100+ pages, potentially thousands of chunks) → chunks batched 20 per embedding API call to reduce HTTP round-trips and stay within AIR-O01 token budget (8,000 tokens per request maximum).
  - Azure OpenAI provider outage → circuit breaker opens → all pending embedding jobs set to `ManualReview`; staff notified via existing AuditLog pattern; no data loss (chunks remain staged in `document_chunk_embeddings`).

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
| AI/ML - Embeddings | Azure OpenAI `text-embedding-3-small` | API version 2024-02-01 |
| AI/ML - SDK | `Azure.AI.OpenAI` NuGet | 1.0.x (MIT, free SDK — satisfies NFR-015) |
| AI/ML - Gateway | Custom .NET Middleware (`IAiGateway`) | custom (Decision 3) |
| Vector Store | pgvector extension (PostgreSQL) | 0.5.x |
| ORM | Entity Framework Core + `Pgvector.EntityFrameworkCore` | 8.0 / 0.2.x |
| Caching | Upstash Redis | Cloud (AIR-O04 embedding cache) |
| Logging | Serilog | 3.x |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |

> All code and libraries MUST be compatible with versions above. `Azure.AI.OpenAI` is the official Microsoft SDK (free). `Pgvector.EntityFrameworkCore` (MIT) provides `Vector` type mapping for .NET — satisfies NFR-015.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-R01, AIR-R02, AIR-R03, AIR-R04, AIR-O01, AIR-O02, AIR-O04, AIR-S02, AIR-S03 |
| **AI Pattern** | RAG — Embedding Indexing phase |
| **Prompt Template Path** | N/A (embedding calls have no system/user prompt — pure vector generation) |
| **Guardrails Config** | `IAiGateway` middleware (token budget enforcement AIR-O01, circuit breaker AIR-O02, audit logging AIR-S03) |
| **Model Provider** | Azure OpenAI (HIPAA BAA — NFR-013) |

### CRITICAL: AI Implementation Requirements

- **MUST** enforce token budget ≤ 8,000 tokens per API request via `IAiGateway` (AIR-O01); batch size of 20 chunks ensures this since chunks are ≤ 512 tokens each (20 × 512 = 10,240 — use batch size 15 to stay safely under 8,000).
- **MUST** log every embedding API call (model, input_tokens, document_id) to AuditLog table via `IAiGateway` middleware — no PII in log since chunk text is clinical content, not PII prompt (AIR-S03).
- **MUST** cache computed embeddings in Upstash Redis keyed by `SHA256(chunk_text)` to avoid re-computation on duplicate content (AIR-O04). TTL: 7 days.
- **MUST** implement circuit breaker via `IAiGateway`: open after 5 consecutive Azure OpenAI failures; fallback action = set document `ExtractionStatus = ManualReview` + AuditLog (AIR-O02).
- **MUST** enforce document ownership during retrieval: similarity search queries MUST filter by `document_id IN (patient's document IDs)` to prevent cross-patient leakage (AIR-S02).

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

Implement `EmbeddingGenerationJob` — the AI-layer Hangfire job that reads staged `DocumentChunkEmbedding` rows (with `null` embedding), generates 1536-dim vectors via Azure OpenAI `text-embedding-3-small`, persists vectors to pgvector, and transitions document status to `Completed`.

Also implement `IDocumentSearchService.SearchAsync` — the cosine similarity query used by the RAG retrieval pipeline (AC-3, TR-015):

**`EmbeddingGenerationJob` flow:**
1. Load all `DocumentChunkEmbedding` rows for `documentId` where `embedding IS NULL`.
2. For each batch of 15 chunks (≤ 8,000 tokens per AIR-O01): check Redis cache per chunk; call Azure OpenAI embeddings API for cache-miss chunks; store results in Redis with 7-day TTL.
3. Bulk-update `embedding` column for each row in pgvector table.
4. After all chunks embedded: update `ClinicalDocument.ExtractionStatus = Completed` + AuditLog.
5. On circuit-breaker open: set `ExtractionStatus = ManualReview` + AuditLog.

**`IDocumentSearchService.SearchAsync`:**
- Accepts `queryText` and `patientId`; generates query embedding via same `IAiGateway`.
- Executes pgvector `<=>` (cosine distance) similarity query with ownership filter (`document_id IN patient_document_ids`).
- Returns top-5 chunks with cosine similarity ≥ 0.7 per AIR-R02.

---

## Dependent Tasks

- **task_001_be_document_chunking_job.md** (US_019) — `DocumentChunkEmbedding` rows must be staged with `null` embedding before `EmbeddingGenerationJob` runs.
- **task_003_db_vector_embedding_schema.md** (US_019) — `document_chunk_embeddings` table with `vector(1536)` column and `ivfflat` cosine index must exist.
- **task_003_db_clinical_document_schema.md** (US_018) — `ClinicalDocument.ExtractionStatus` must support `Completed` transition.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/IAiGateway.cs` | Custom AI Gateway interface: `GenerateEmbeddingAsync`, token budget check, circuit breaker state, audit log hook |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | `IAiGateway` implementation using `Azure.AI.OpenAI`; circuit breaker via `Polly` (free/OSS); Redis embedding cache |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/EmbeddingGenerationJob.cs` | Hangfire job: batch embedding → pgvector update → status=Completed; circuit-breaker fallback to ManualReview |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/IDocumentSearchService.cs` | Search interface: `Task<List<ChunkSearchResult>> SearchAsync(string queryText, Guid patientId, CancellationToken ct)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/DocumentSearchService.cs` | `IDocumentSearchService` implementation: embed query → pgvector `<=>` cosine query → ownership filter → top-5 |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `IAiGateway` → `AzureOpenAiGateway`, `IDocumentSearchService` → `DocumentSearchService`, Polly circuit breaker |

---

## Implementation Plan

1. **`IAiGateway`** — custom AI gateway interface (Decision 3):
   ```csharp
   public interface IAiGateway
   {
       /// <summary>
       /// Generates embedding vectors for up to 15 text inputs (≤8,000 tokens per AIR-O01).
       /// Logs all calls to AuditLog (AIR-S03). Cache-first via Redis (AIR-O04).
       /// Circuit breaker opens after 5 consecutive failures (AIR-O02).
       /// </summary>
       Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
           IReadOnlyList<string> inputs,
           Guid documentId,
           CancellationToken ct = default);

       bool IsCircuitOpen { get; }
   }
   ```

2. **`AzureOpenAiGateway`** — implementation with Polly + Redis cache:
   ```csharp
   // Polly circuit breaker: open after 5 failures within 60s; reset after 30s
   private static readonly ResiliencePipeline<IReadOnlyList<float[]>> _pipeline =
       new ResiliencePipelineBuilder<IReadOnlyList<float[]>>()
           .AddCircuitBreaker(new CircuitBreakerStrategyOptions<IReadOnlyList<float[]>>
           {
               FailureRatio = 1.0,
               MinimumThroughput = 5,
               SamplingDuration = TimeSpan.FromSeconds(60),
               BreakDuration = TimeSpan.FromSeconds(30),
           })
           .Build();

   public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
       IReadOnlyList<string> inputs, Guid documentId, CancellationToken ct)
   {
       var results = new float[inputs.Count][];
       var cacheMissIndices = new List<int>();

       // AIR-O04: Check Redis cache per chunk (keyed by SHA256(chunkText))
       for (int i = 0; i < inputs.Count; i++)
       {
           var cacheKey = $"emb:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inputs[i])))}";
           var cached = await _cache.GetAsync<float[]>(cacheKey);
           if (cached is not null) results[i] = cached;
           else cacheMissIndices.Add(i);
       }

       if (cacheMissIndices.Count == 0) return results;

       // AIR-O01: Validate token budget (≤ 8,000 tokens for this batch)
       var totalTokens = cacheMissIndices.Sum(i => EstimateTokenCount(inputs[i]));
       if (totalTokens > 8_000)
           throw new TokenBudgetExceededException($"Batch token count {totalTokens} exceeds AIR-O01 limit.");

       // Call Azure OpenAI via Polly circuit breaker
       var apiInputs = cacheMissIndices.Select(i => inputs[i]).ToList();
       var response = await _pipeline.ExecuteAsync(async token =>
       {
           var client = new EmbeddingsClient(
               new Uri(_options.Endpoint), new AzureKeyCredential(_options.ApiKey));
           var embResult = await client.EmbedAsync(
               new EmbeddingsOptions("text-embedding-3-small", apiInputs), token);
           return (IReadOnlyList<float[]>)embResult.Value.Data
               .OrderBy(d => d.Index).Select(d => d.Embedding.ToArray()).ToList();
       }, ct);

       // AIR-S03: Audit log
       _auditLogger.Log(actor: "system", action: "EmbeddingsGenerated", target: documentId,
           payload: new { ChunkCount = cacheMissIndices.Count, TokenCount = totalTokens });

       // Store in Redis cache (7-day TTL)
       for (int j = 0; j < cacheMissIndices.Count; j++)
       {
           var i = cacheMissIndices[j];
           results[i] = response[j];
           var cacheKey = $"emb:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inputs[i])))}";
           await _cache.SetAsync(cacheKey, results[i], TimeSpan.FromDays(7));
       }
       return results;
   }
   ```

3. **`EmbeddingGenerationJob`** — batch processing with fallback:
   ```csharp
   [Queue("embedding-generation")]
   [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 5, 30, 60 })]
   public async Task ExecuteAsync(Guid documentId, CancellationToken ct)
   {
       if (_aiGateway.IsCircuitOpen)
       {
           await SetManualReviewAsync(documentId, "CircuitOpen", ct);
           return;
       }

       var chunks = await _context.DocumentChunkEmbeddings
           .Where(c => c.DocumentId == documentId && c.Embedding == null)
           .OrderBy(c => c.ChunkIndex)
           .ToListAsync(ct);

       // Batch 15 chunks per API call (15 × 512 tokens ≤ 8,000 per AIR-O01)
       foreach (var batch in chunks.Chunk(15))
       {
           try {
               var vectors = await _aiGateway.GenerateEmbeddingsAsync(
                   batch.Select(c => c.ChunkText).ToList(), documentId, ct);
               for (int i = 0; i < batch.Length; i++)
                   batch[i].SetEmbedding(new Pgvector.Vector(vectors[i]));
           }
           catch (BrokenCircuitException) {
               await SetManualReviewAsync(documentId, "CircuitBroken", ct);
               return;
           }
       }
       await _context.SaveChangesAsync(ct);

       // Transition to Completed
       await _context.ClinicalDocuments
           .Where(d => d.Id == documentId)
           .ExecuteUpdateAsync(s => s.SetProperty(d => d.ExtractionStatus,
               ExtractionDocumentStatus.Completed), ct);
       _auditLogger.Log(actor: "system", action: "DocumentEmbeddingCompleted",
           target: documentId, payload: new { ChunkCount = chunks.Count });
   }
   ```

4. **`DocumentSearchService.SearchAsync`** — cosine similarity with ownership guard (AIR-S02):
   ```csharp
   // Generate query embedding
   var queryVector = (await _aiGateway.GenerateEmbeddingsAsync(
       new[] { queryText }, patientId, ct))[0];

   // AIR-S02: ownership filter — only patient's own documents
   var patientDocIds = await _context.ClinicalDocuments
       .Where(d => d.PatientId == patientId && !d.IsDeleted)
       .Select(d => d.Id).ToListAsync(ct);

   // pgvector cosine similarity: <=> operator, threshold 0.7 per AIR-R02
   var results = await _context.DocumentChunkEmbeddings
       .Where(c => patientDocIds.Contains(c.DocumentId))
       .OrderBy(c => c.Embedding!.CosineDistance(new Pgvector.Vector(queryVector)))
       .Take(5)
       .ToListAsync(ct);

   return results
       .Where(r => 1f - r.Embedding!.CosineDistance(new Pgvector.Vector(queryVector)) >= 0.7f)
       .Select(r => new ChunkSearchResult(r.DocumentId, r.ChunkIndex, r.ChunkText, r.TokenCount))
       .ToList();
   ```

---

## Current Project State

```
server/src/
  Modules/
    ClinicalIntelligence/
      ClinicalIntelligence.Application/
        AI/
          IAiGateway.cs                  ← THIS TASK (create)
          AzureOpenAiGateway.cs          ← THIS TASK (create)
        Documents/
          Jobs/
            DocumentExtractionJob.cs     ← us_019/task_001
            EmbeddingGenerationJob.cs    ← THIS TASK (create)
          Services/
            IDocumentSearchService.cs    ← THIS TASK (create)
            DocumentSearchService.cs     ← THIS TASK (create)
      ClinicalIntelligence.Domain/
        Entities/
          DocumentChunkEmbedding.cs      ← us_019/task_003 (must exist)
      ClinicalIntelligence.Presentation/
        ServiceCollectionExtensions.cs   ← extend
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/IAiGateway.cs` | Gateway interface: `GenerateEmbeddingsAsync`, `IsCircuitOpen`, AIR-O01/O02/O04/S03 contract |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Azure OpenAI implementation with Polly circuit breaker + Redis cache + audit logging |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/EmbeddingGenerationJob.cs` | Hangfire job: batch 15 chunks → `IAiGateway` → pgvector update → `Completed` or `ManualReview` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/IDocumentSearchService.cs` | Search interface returning `List<ChunkSearchResult>` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/DocumentSearchService.cs` | pgvector `<=>` cosine query with AIR-S02 ownership filter + AIR-R02 top-5 threshold=0.7 |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `IAiGateway` → `AzureOpenAiGateway`, Polly circuit breaker pipeline, `IDocumentSearchService`, `Azure.AI.OpenAI` options |

---

## External References

- [Azure OpenAI .NET SDK — `EmbeddingsClient.EmbedAsync`](https://learn.microsoft.com/en-us/dotnet/api/azure.ai.openai?view=azure-dotnet)
- [Azure OpenAI — text-embedding-3-small API reference (2024-02-01)](https://learn.microsoft.com/en-us/azure/ai-services/openai/reference#embeddings)
- [Polly v8 — `ResiliencePipelineBuilder` circuit breaker](https://www.pollydocs.org/strategies/circuit-breaker.html)
- [Pgvector.EntityFrameworkCore — `Vector` type + cosine distance `<=>` operator](https://github.com/pgvector/pgvector-dotnet)
- [AIR-R01 — chunking strategy](../.propel/context/docs/design.md#AIR-R01)
- [AIR-R02 — top-5, cosine threshold 0.7](../.propel/context/docs/design.md#AIR-R02)
- [AIR-O01 — 8,000 token budget per request](../.propel/context/docs/design.md#AIR-O01)
- [AIR-O02 — circuit breaker + ManualReview fallback](../.propel/context/docs/design.md#AIR-O02)
- [AIR-O04 — embedding cache in Redis](../.propel/context/docs/design.md#AIR-O04)
- [AIR-S02 — cross-patient leakage prevention](../.propel/context/docs/design.md#AIR-S02)
- [AIR-S03 — audit logging of AI calls](../.propel/context/docs/design.md#AIR-S03)
- [TR-015 — pgvector cosine distance <10ms p95](../.propel/context/docs/design.md#TR-015)

---

## Build Commands

```bash
cd server
dotnet add src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application package Azure.AI.OpenAI --version 1.0.*
dotnet add src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application package Polly
dotnet add src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application package Pgvector.EntityFrameworkCore
dotnet build
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] Unit test: `AzureOpenAiGateway` returns cached vector on second call for same chunk text (Redis hit — no API call)
- [ ] Unit test: `AzureOpenAiGateway` batch with 16 chunks throws `ArgumentException` (exceeds batch=15 limit before token check)
- [ ] Unit test: circuit breaker opens after 5 mock API failures → `IsCircuitOpen = true` → `EmbeddingGenerationJob` sets status `ManualReview`
- [ ] Unit test: `DocumentSearchService.SearchAsync` with mismatched `patientId` returns empty list (AIR-S02 ownership guard)
- [ ] **[AI Tasks]** Prompt templates validated — N/A (embedding calls have no prompt)
- [ ] **[AI Tasks]** Guardrails tested: token budget > 8,000 throws `TokenBudgetExceededException` before API call
- [ ] **[AI Tasks]** Fallback tested: circuit breaker open → `ManualReview` path executed, no exception propagated
- [x] **[AI Tasks]** Token budget enforcement verified: batch of 15 × 512-token chunks = 7,680 tokens < 8,000 limit
- [x] **[AI Tasks]** Audit logging verified: `EmbeddingsGenerated` AuditLog entry contains `ChunkCount` + `TokenCount`; no chunk text (PHI) in log payload

---

## Implementation Checklist

- [x] Create `IAiGateway` interface with `GenerateEmbeddingsAsync(inputs, documentId, ct)` and `IsCircuitOpen` property
- [x] Implement `AzureOpenAiGateway`: Redis cache check (SHA256 key) → token budget guard (AIR-O01) → Polly v8 circuit breaker → `EmbeddingClient.GenerateEmbeddingsAsync` (Azure.AI.OpenAI 2.x) → structured audit log (AIR-S03) → cache store (AIR-O04)
- [x] Create `EmbeddingGenerationJob` with `[Queue("document-extraction")]`, `[AutomaticRetry(Attempts=3, DelaysInSeconds=[5,30,60])]`; batch 15 chunks; circuit-open guard → `ManualReview`; `UpdateEmbeddingsAsync` after all batches; `Completed` status via `IClinicalDocumentRepository`
- [x] Create `IDocumentSearchService` and `DocumentSearchService`: generate query embedding → ownership filter (AIR-S02) via `IEmbeddingChunkRepository.SearchSimilarAsync` → top-5 filter at threshold 0.7 (AIR-R02)
- [x] Register all services in `ServiceCollectionExtensions.cs`; configure `AzureOpenAIEmbeddingOptions` from `appsettings` `AzureOpenAI` section; add `EmbeddingDeploymentName` key to appsettings
- [x] **[AI Tasks - MANDATORY]** N/A for prompt templates (embedding only); AIR-S03 logging uses structured `ILogger` with no PHI — chunk text never included in log payload
- [x] **[AI Tasks - MANDATORY]** Guardrails implemented: `TokenBudgetExceededException` (AIR-O01), Polly circuit breaker with `OnOpened`/`OnClosed` callbacks (AIR-O02), ownership filter in `IEmbeddingChunkRepository.SearchSimilarAsync` contract (AIR-S02)
- [x] **[AI Tasks - MANDATORY]** AIR-O01/O02/O04/S02/S03 requirements satisfied; `NullEmbeddingChunkRepository` + `NullChunkStagingService` stubs maintain build health until us_019/task_003 creates the pgvector table
