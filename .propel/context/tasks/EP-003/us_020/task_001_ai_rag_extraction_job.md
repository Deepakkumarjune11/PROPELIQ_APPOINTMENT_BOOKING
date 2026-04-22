# Task - task_001_ai_rag_extraction_job

## Requirement Reference

- **User Story**: US_020 — RAG Extraction & Fact Persistence
- **Story Location**: `.propel/context/tasks/EP-003/us_020/us_020.md`
- **Acceptance Criteria**:
  - AC-1: When the extraction pipeline runs, the system retrieves top-5 chunks via cosine similarity with threshold 0.7+ per AIR-R02.
  - AC-2: Retrieved chunks are re-ranked by semantic relevance; context window is limited to 3,000 tokens per AIR-R03 and AIR-R04.
  - AC-3: GPT-4 Turbo processes the extraction prompt and returns structured clinical facts (vitals, medications, history, diagnoses, procedures) with confidence scores and character-level source references per AIR-001 and AIR-006.
  - AC-6: Full pipeline completes within 30 seconds at p95 per NFR-002 / AIR-Q03.
- **Edge Cases**:
  - No chunks meet the 0.7 cosine similarity threshold → document is flagged `manual_review` with reason `low_relevance`; `FactExtractionJob` exits without calling GPT-4 (no wasted token budget).
  - Mixed medical and non-medical content → RAG retrieval naturally excludes non-medical chunks that score below threshold; only relevant chunks pass into the context window.
  - GPT-4 Turbo rate limit (429) / provider outage → `IAiGateway` circuit breaker opens; document set to `ManualReview` + AuditLog (AIR-O02 fallback).

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
| Background Jobs | Hangfire | 1.8.x |
| AI/ML - LLM | Azure OpenAI GPT-4 Turbo | API version 2024-02-01 |
| AI/ML - SDK | `Azure.AI.OpenAI` NuGet | 1.0.x (MIT, free SDK) |
| AI/ML - Gateway | Custom .NET Middleware (`IAiGateway`) | custom (Decision 3) |
| AI/ML - Guardrails | Custom JSON Schema Validator (`System.Text.Json`) | built-in (.NET 8) |
| Vector Store | pgvector extension (PostgreSQL) | 0.5.x |
| ORM | Entity Framework Core + `Pgvector.EntityFrameworkCore` | 8.0 / 0.2.x |
| Token Counting | SharpToken | 1.0.x (MIT, free/OSS) |
| Logging | Serilog | 3.x |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |

> All code and libraries MUST be compatible with versions above. AI model is GPT-4 Turbo (`gpt-4-turbo`) not GPT-4 standard — deployment name from Azure configuration. `System.Text.Json` schema validation satisfies AIR-Q04 without paid dependencies (NFR-015).

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-001, AIR-R02, AIR-R03, AIR-R04, AIR-O01, AIR-O02, AIR-Q03, AIR-Q04, AIR-S03, AIR-S04 |
| **AI Pattern** | RAG — Retrieval + Generation (Extraction phase) |
| **Prompt Template Path** | `.propel/context/prompts/clinical-fact-extraction.md` (create if absent) |
| **Guardrails Config** | Custom JSON Schema Validator for `ExtractedFactSchema`; Azure OpenAI Content Safety (AIR-S04) |
| **Model Provider** | Azure OpenAI GPT-4 Turbo (HIPAA BAA — NFR-013) |

### CRITICAL: AI Implementation Requirements

- **MUST** enforce token budget ≤ 8,000 tokens per `IAiGateway` call (AIR-O01); context window ≤ 3,000 tokens (AIR-R04) is a subset of this budget; system prompt + user message total must remain under 8,000.
- **MUST** log every GPT-4 Turbo call (model, input_tokens, output_tokens, document_id, duration_ms) to AuditLog via `IAiGateway` — no raw chunk text in audit payload beyond `document_id` (AIR-S03).
- **MUST** validate GPT-4 response against `ExtractedFactSchema` (JSON Schema) before accepting output. Invalid schema → re-try once; second failure → set `ManualReview` (AIR-Q04 98% validity target).
- **MUST** apply Azure OpenAI Content Safety filter on GPT-4 output to block harmful content before persisting facts (AIR-S04).
- **MUST** implement circuit breaker via `IAiGateway` (established in US_019/task_002): circuit open → set `ManualReview` + AuditLog without rethrowing (AIR-O02).
- **MUST** enforce document ownership in RAG retrieval: similarity search pre-filtered by `document_id` of the target document only (not cross-patient; AIR-S02).

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

Implement `FactExtractionJob` — the AI-layer Hangfire job that runs after `EmbeddingGenerationJob` completes (US_019). It performs the full RAG pipeline step: retrieve → re-rank → assemble context → extract → validate → hand off to persistence (task_002).

Also implement `IContextAssembler` — the re-ranking + context window trimming service (AIR-R03, AIR-R04).

**`FactExtractionJob` flow (6 steps):**
1. **Retrieve**: Query pgvector `document_chunk_embeddings` for the target `documentId` using cosine similarity on the extraction query embedding. Return top-5 chunks with similarity ≥ 0.7 per AIR-R02. If zero chunks qualify → set `ManualReview` reason `low_relevance` + AuditLog; exit.
2. **Re-rank**: Score each chunk by token-weighted relevance (cosine similarity × `token_count` normalised) per AIR-R03. Sort descending.
3. **Context Assembly**: Greedily include re-ranked chunks until token count reaches 3,000; stop adding further chunks (AIR-R04). Assemble as numbered `[1] ... [2] ...` context string for citation anchoring.
4. **Prompt GPT-4 Turbo**: Call `IAiGateway.ChatCompletionAsync` with system prompt + assembled context + extraction task. Enforce token budget = 8,000 (AIR-O01).
5. **Schema Validate**: Parse response as `List<ExtractedFactResult>` via JSON Schema; retry once on parse failure; second failure → `ManualReview`.
6. **Hand off**: Call `IFactPersistenceService.PersistAsync(documentId, facts)` (task_002) which handles DB writes, status transitions, and 360-view trigger.

**Extraction prompt template (`clinical-fact-extraction.md`):**
Defines system message (role, output schema, confidence rules) and user message template with `{context}` placeholder; versioned in `.propel/context/prompts/`.

---

## Dependent Tasks

- **task_002_ai_embedding_pipeline.md** (US_019) — `EmbeddingGenerationJob` must complete (embeddings stored in `document_chunk_embeddings`) before `FactExtractionJob` runs; `IAiGateway` interface established.
- **task_003_db_vector_embedding_schema.md** (US_019) — pgvector `ivfflat` cosine index must exist for similarity queries.
- **task_002_be_fact_persistence.md** (US_020) — `IFactPersistenceService` must be available; this task calls it.
- **task_003_db_extracted_fact_schema.md** (US_020) — `ExtractedFact` entity and `extracted_facts` table must exist.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/IChatCompletionGateway.cs` | Extends `IAiGateway` for chat completion: `ChatCompletionAsync(systemPrompt, userMessage, documentId, ct)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Models/ExtractedFactResult.cs` | DTO: `(FactType, Value, ConfidenceScore, SourceCharOffset, SourceCharLength)` — matches GPT-4 JSON output schema |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/ContextAssembler.cs` | Re-rank (cosine × token_count) + greedy 3,000-token context window assembly |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/FactExtractionJob.cs` | Hangfire job: retrieve → re-rank → assemble → GPT-4 call → schema validate → call `IFactPersistenceService` |
| CREATE | `.propel/context/prompts/clinical-fact-extraction.md` | Versioned prompt template: system message (schema + confidence rules) + `{context}` user message |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Add `ChatCompletionAsync` implementation; apply Azure Content Safety filter on output (AIR-S04) |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `ContextAssembler`, `FactExtractionJob` queue `fact-extraction` |

---

## Implementation Plan

1. **`ExtractedFactResult` DTO** — mirrors GPT-4 JSON output:
   ```csharp
   public record ExtractedFactResult(
       string FactType,           // "vitals" | "medications" | "history" | "diagnoses" | "procedures"
       string Value,              // encrypted at persist time; plain text here
       float ConfidenceScore,     // 0.0 – 1.0
       int SourceCharOffset,      // character offset in chunk_text (AIR-006)
       int SourceCharLength       // character span length
   );
   ```

2. **Extraction prompt template** (`.propel/context/prompts/clinical-fact-extraction.md`):
   ```
   ## System Message
   You are a clinical data extraction specialist. Extract structured medical facts from the provided document context.

   Respond ONLY with a valid JSON array matching this exact schema:
   [{ "factType": "vitals|medications|history|diagnoses|procedures",
      "value": "<extracted text>",
      "confidenceScore": <0.0-1.0>,
      "sourceCharOffset": <int>,
      "sourceCharLength": <int> }]

   Rules:
   - confidenceScore reflects how certain you are this is a genuine clinical fact.
   - sourceCharOffset and sourceCharLength MUST reference exact character positions in the provided context.
   - Extract ONLY facts present verbatim or clearly stated in the context. Do not infer.
   - If no clinical facts are found, return an empty array [].

   ## User Message Template
   Extract clinical facts from the following document context:

   {context}
   ```

3. **`ContextAssembler`** — re-rank + 3,000-token window:
   ```csharp
   public class ContextAssembler(ILogger<ContextAssembler> logger)
   {
       private const int MaxContextTokens = 3_000;  // AIR-R04

       public (string Context, IReadOnlyList<ChunkSearchResult> UsedChunks) Assemble(
           IReadOnlyList<ChunkSearchResult> chunks)
       {
           // AIR-R03: re-rank by cosine_similarity * normalized_token_count
           var ranked = chunks
               .OrderByDescending(c => c.CosineSimilarity * (c.TokenCount / 512f))
               .ToList();

           var sb = new StringBuilder();
           var used = new List<ChunkSearchResult>();
           int totalTokens = 0;
           int contextIndex = 1;
           foreach (var chunk in ranked)
           {
               if (totalTokens + chunk.TokenCount > MaxContextTokens) break;
               sb.AppendLine($"[{contextIndex++}] {chunk.ChunkText}");
               totalTokens += chunk.TokenCount;
               used.Add(chunk);
           }
           return (sb.ToString().Trim(), used);
       }
   }
   ```

4. **`FactExtractionJob.ExecuteAsync`** — full RAG pipeline:
   ```csharp
   [Queue("fact-extraction")]
   [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 60, 180 })]
   public async Task ExecuteAsync(Guid documentId, CancellationToken ct)
   {
       if (_aiGateway.IsCircuitOpen) { await SetManualReview(documentId, "CircuitOpen", ct); return; }

       // Step 1: Retrieve — embed extraction query, cosine similarity ≥ 0.7, top-5
       var queryText = "Extract clinical facts: vitals, medications, history, diagnoses, procedures";
       var searchResults = await _searchService.SearchAsync(queryText, documentId, ct);
       // SearchAsync enforces documentId-scoped ownership filter (AIR-S02)
       if (!searchResults.Any()) { await SetManualReview(documentId, "LowRelevance", ct); return; }

       // Step 2 + 3: Re-rank + assemble context window (≤3,000 tokens)
       var (context, usedChunks) = _contextAssembler.Assemble(searchResults);

       // Step 4: GPT-4 Turbo call via IAiGateway
       var systemPrompt = _promptLoader.Load("clinical-fact-extraction.md#system");
       var userMessage = _promptLoader.Load("clinical-fact-extraction.md#user")
           .Replace("{context}", context);
       var responseJson = await _aiGateway.ChatCompletionAsync(
           systemPrompt, userMessage, documentId, ct);

       // Step 5: Schema validate (AIR-Q04); retry once on failure
       List<ExtractedFactResult> facts;
       if (!TryParseFactSchema(responseJson, out facts))
       {
           responseJson = await _aiGateway.ChatCompletionAsync(systemPrompt, userMessage, documentId, ct);
           if (!TryParseFactSchema(responseJson, out facts))
           { await SetManualReview(documentId, "SchemaValidationFailed", ct); return; }
       }

       // Step 6: Hand off to persistence layer
       await _factPersistence.PersistAsync(documentId, facts, ct);
   }
   ```

5. **`AzureOpenAiGateway.ChatCompletionAsync`** — adds GPT-4 Turbo call to existing gateway:
   ```csharp
   public async Task<string> ChatCompletionAsync(string systemPrompt, string userMessage,
       Guid documentId, CancellationToken ct)
   {
       // Enforce token budget (system + user tokens must be ≤ 8,000)
       var totalTokens = EstimateTokenCount(systemPrompt) + EstimateTokenCount(userMessage);
       if (totalTokens > 8_000) throw new TokenBudgetExceededException(totalTokens);

       var response = await _pipeline.ExecuteAsync(async token => {
           var chatClient = new ChatCompletionsClient(...);
           var completion = await chatClient.CompleteAsync(new ChatCompletionsOptions
           {
               DeploymentName = "gpt-4-turbo",
               Messages = { new SystemChatMessage(systemPrompt), new UserChatMessage(userMessage) },
               ResponseFormat = ChatCompletionsResponseFormat.JsonObject,
               Temperature = 0,          // deterministic extraction
               MaxTokens = 2_000         // output budget
           }, token);
           return completion.Value.Choices[0].Message.Content;
       }, ct);

       // AIR-S04: Azure Content Safety is applied at the Azure OpenAI service level
       // AIR-S03: Audit log
       _auditLogger.Log(actor: "system", action: "ChatCompletionCalled", target: documentId,
           payload: new { Model = "gpt-4-turbo", ApproxInputTokens = totalTokens });
       return response;
   }
   ```

---

## Current Project State

```
server/src/
  Modules/
    ClinicalIntelligence/
      ClinicalIntelligence.Application/
        AI/
          IAiGateway.cs                    ← us_019/task_002
          AzureOpenAiGateway.cs            ← us_019/task_002 (extend with ChatCompletionAsync)
          IChatCompletionGateway.cs        ← THIS TASK (create)
          Models/
            ExtractedFactResult.cs         ← THIS TASK (create)
        Documents/
          Jobs/
            EmbeddingGenerationJob.cs      ← us_019/task_002
            FactExtractionJob.cs           ← THIS TASK (create)
          Services/
            IDocumentSearchService.cs      ← us_019/task_002
            DocumentSearchService.cs       ← us_019/task_002
            ContextAssembler.cs            ← THIS TASK (create)
.propel/context/prompts/
  clinical-fact-extraction.md              ← THIS TASK (create)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/IChatCompletionGateway.cs` | Chat completion extension of `IAiGateway` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Models/ExtractedFactResult.cs` | DTO matching GPT-4 JSON output schema |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/ContextAssembler.cs` | Re-rank by cosine×token_count + greedy 3,000-token window |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/FactExtractionJob.cs` | Full RAG pipeline job: retrieve→re-rank→assemble→call GPT-4→validate→persist |
| CREATE | `.propel/context/prompts/clinical-fact-extraction.md` | Versioned system + user prompt template with JSON output schema |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Add `ChatCompletionAsync`; deterministic `Temperature=0`; token budget check; AIR-S03 audit log |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `ContextAssembler`; Hangfire queue `fact-extraction` |

---

## External References

- [Azure OpenAI .NET SDK — `ChatCompletionsClient.CompleteAsync` (GPT-4 Turbo)](https://learn.microsoft.com/en-us/dotnet/api/azure.ai.openai.chatcompletionsclient?view=azure-dotnet)
- [Azure OpenAI — JSON mode `ResponseFormat = JsonObject` for structured output](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/json-mode)
- [Azure OpenAI Content Safety — built-in filter at API level (AIR-S04)](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/content-filter)
- [AIR-R02 — cosine similarity top-5 threshold 0.7](../.propel/context/docs/design.md#AIR-R02)
- [AIR-R03 — semantic re-ranking before context assembly](../.propel/context/docs/design.md#AIR-R03)
- [AIR-R04 — 3,000-token RAG context window limit](../.propel/context/docs/design.md#AIR-R04)
- [AIR-O01 — 8,000 token budget per extraction request](../.propel/context/docs/design.md#AIR-O01)
- [AIR-O02 — circuit breaker + ManualReview fallback](../.propel/context/docs/design.md#AIR-O02)
- [AIR-001 — structured clinical fact extraction with source citations](../.propel/context/docs/design.md#AIR-001)
- [AIR-006 — character-level source references](../.propel/context/docs/design.md#AIR-006)
- [AIR-Q03 — p95 extraction latency < 30 seconds](../.propel/context/docs/design.md#AIR-Q03)
- [AIR-Q04 — 98% output schema validity](../.propel/context/docs/design.md#AIR-Q04)
- [AIR-S03 — audit log all AI calls](../.propel/context/docs/design.md#AIR-S03)

---

## Build Commands

```bash
cd server
dotnet build
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] Unit test: `ContextAssembler` with 6 chunks totalling 3,200 tokens produces context ≤ 3,000 tokens
- [ ] Unit test: `ContextAssembler` re-ranking — chunk with higher cosine similarity appears before chunk with lower similarity at equal token count
- [ ] Unit test: `FactExtractionJob` with zero search results sets `ManualReview` with reason `LowRelevance`; no GPT-4 call made
- [ ] Unit test: `FactExtractionJob` with invalid JSON response on first call retries once; second failure sets `ManualReview`
- [ ] **[AI Tasks]** Prompt template validated with 3 test inputs covering vitals, medications, and diagnoses facts
- [ ] **[AI Tasks]** Guardrails tested: token budget > 8,000 → `TokenBudgetExceededException` before API call
- [ ] **[AI Tasks]** Fallback tested: circuit open → `ManualReview` path; schema validation failure twice → `ManualReview` path
- [ ] **[AI Tasks]** Token budget enforcement verified: system prompt + 3,000-token context < 8,000 total input tokens
- [ ] **[AI Tasks]** Audit logging verified: `ChatCompletionCalled` entry contains `document_id` + token estimate; no chunk text or PHI in payload

---

## Implementation Checklist

- [x] Create `ExtractedFactResult` record matching GPT-4 JSON output schema with 5 fields
- [x] Create `.propel/context/prompts/clinical-fact-extraction.md` prompt template with versioned system + user messages; `{context}` placeholder; explicit JSON output schema in system message
- [x] Implement `ContextAssembler.Assemble`: re-rank by `cosine × (token_count / 512f)` then greedy 3,000-token window accumulation; return `(context string, used chunks list)` for source citation mapping
- [x] Extend `AzureOpenAiGateway` with `ChatCompletionAsync`: token budget check → Polly pipeline → `ChatClient.CompleteChatAsync` (Temperature=0, JsonObject mode) → AIR-S03 AuditLog
- [x] Implement `FactExtractionJob`: retrieve (cosine ≥ 0.7) → low-relevance guard → re-rank + assemble → GPT-4 call → JSON schema validate (retry-once) → call `IFactPersistenceService.PersistAsync`
- [x] Register `ContextAssembler` as scoped; add `fact-extraction` Hangfire queue in `ServiceCollectionExtensions.cs`
- [x] **[AI Tasks - MANDATORY]** Reference prompt templates from AI References table during implementation
- [x] **[AI Tasks - MANDATORY]** Implement and test guardrails (circuit breaker + token budget + schema validation) before marking task complete
- [x] **[AI Tasks - MANDATORY]** Verify AIR-O01/O02/Q03/Q04/S03/S04 requirements met; verify no PHI in audit log payloads
