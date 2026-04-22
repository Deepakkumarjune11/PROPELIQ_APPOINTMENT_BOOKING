# Task - task_001_be_document_chunking_job

## Requirement Reference

- **User Story**: US_019 — Document Chunking & Embedding Pipeline
- **Story Location**: `.propel/context/tasks/EP-003/us_019/us_019.md`
- **Acceptance Criteria**:
  - AC-1: When the background worker processes a document, the PDF is parsed to text and chunked into 512-token segments with 25% overlap per AIR-R01.
  - AC-4: When the extraction_status is updated, the `ClinicalDocument` record transitions from `queued` → `processing`; UI polling via `GET /api/v1/documents` reflects the status change.
- **Edge Cases**:
  - PDF contains no extractable text (scanned image without OCR layer) → `UglyToad.PdfPig` returns empty `string` after text extraction; system sets `extraction_status = manual_review` and writes AuditLog entry `DocumentFlaggedForManualReview`; does NOT throw exception or retry.
  - Very large documents (100+ pages) → chunking proceeds normally; all resulting chunks are yielded via `IAsyncEnumerable<DocumentChunk>` to avoid loading all chunks into memory at once; `chunk_index` assigned sequentially starting at 0.

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
| PDF Parsing | UglyToad.PdfPig | 0.1.x (MIT, free/OSS — satisfies NFR-015) |
| Token Counting | SharpToken | 1.0.x (MIT, free/OSS — GPT-4 tokenizer for accurate 512-token chunks) |
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| Logging | Serilog | 3.x |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |

> All code and libraries MUST be compatible with versions above. `UglyToad.PdfPig` (MIT) and `SharpToken` (MIT) satisfy NFR-015 (OSS-only). No proprietary PDF libraries permitted.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | AIR-R01 |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

> This task implements the deterministic chunking pipeline (text extraction + sliding window chunking). AI model invocation (embedding generation) is in `task_002_ai_embedding_pipeline.md`.

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

Implement the deterministic phase of the `DocumentExtractionJob` Hangfire pipeline within the `ClinicalIntelligence` bounded context:

**Phase 1 — Status Transition (AC-4)**:
- On job start, update `ClinicalDocument.ExtractionStatus` from `Queued` → `Processing` via `ExecuteUpdateAsync` (single SQL UPDATE without entity load).

**Phase 2 — PDF Text Extraction (AC-1)**:
- Use `UglyToad.PdfPig` to open the file stream from `IFileStorageService` and extract text from all pages in order.
- Concatenate page texts with a newline separator preserving paragraph boundaries.
- Empty text output (scanned PDF) → immediately set status to `ManualReview`, write AuditLog, return without proceeding to chunking.

**Phase 3 — Token-based Chunking (AC-1, AIR-R01)**:
- Use `SharpToken` (`GptEncoding.GetEncoding("cl100k_base")`) to tokenize the full document text.
- Sliding window chunking: window size = 512 tokens, step size = 384 tokens (25% overlap = 128 tokens retained between chunks).
- For each chunk window: decode tokens back to string; record `chunk_text`, `chunk_index` (0-based), and `token_count`.
- Yield `DocumentChunk` records via `IAsyncEnumerable<DocumentChunk>` — allows `task_002` embedding job to consume and store each chunk without holding all in memory.

**Phase 4 — Job Coordination**:
- After chunking completes, enqueue `EmbeddingGenerationJob` (implemented in task_002) via `BackgroundJob.Enqueue` passing `documentId`.
- `DocumentExtractionJob` stub from US_018 `task_002` is replaced by this full implementation.

This task is purely deterministic — no AI model calls. The `DocumentChunk` record is a plain C# record; storage into pgvector occurs in task_002.

---

## Dependent Tasks

- **task_002_be_document_upload_api.md** (US_018) — `DocumentExtractionJob` stub must exist (this task replaces/implements it); `IFileStorageService` available to read file bytes.
- **task_003_db_clinical_document_schema.md** (US_018) — `ClinicalDocument` entity and `ExtractionDocumentStatus` enum must exist.
- **task_003_db_vector_embedding_schema.md** (US_019) — `DocumentChunkEmbedding` entity must exist for task_002 to persist chunks.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Models/DocumentChunk.cs` | Plain C# record: `(Guid DocumentId, int ChunkIndex, string ChunkText, int TokenCount)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/PdfTextExtractor.cs` | PdfPig-based service: extracts ordered text from all PDF pages |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/DocumentChunker.cs` | Sliding window chunker using SharpToken; yields `DocumentChunk` via `IAsyncEnumerable` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/DocumentExtractionJob.cs` | Replace stub body: status transition → PDF extract → chunk → enqueue `EmbeddingGenerationJob` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `PdfTextExtractor`, `DocumentChunker` as scoped services |

---

## Implementation Plan

1. **`DocumentChunk` record** (shared model between BE and AI tasks):
   ```csharp
   public record DocumentChunk(
       Guid DocumentId,
       int ChunkIndex,
       string ChunkText,
       int TokenCount
   );
   ```

2. **`PdfTextExtractor`** — ordered text extraction with empty-doc detection:
   ```csharp
   public class PdfTextExtractor(IFileStorageService fileStorage, ILogger<PdfTextExtractor> logger)
   {
       public async Task<string> ExtractTextAsync(string fileUri, CancellationToken ct)
       {
           await using var stream = await fileStorage.ReadAsync(fileUri, ct);
           using var pdf = PdfDocument.Open(stream);
           var sb = new StringBuilder();
           foreach (var page in pdf.GetPages())
           {
               // PdfPig: GetWords() preserves reading order; join with spaces
               var pageText = string.Join(" ", page.GetWords().Select(w => w.Text));
               sb.AppendLine(pageText);
           }
           return sb.ToString().Trim();
       }
   }
   ```

3. **`DocumentChunker`** — sliding window tokenizer (AIR-R01):
   ```csharp
   public class DocumentChunker
   {
       private const int WindowSize = 512;
       private const int StepSize  = 384;   // 25% overlap = 128 tokens retained

       public async IAsyncEnumerable<DocumentChunk> ChunkAsync(
           Guid documentId, string text,
           [EnumeratorCancellation] CancellationToken ct = default)
       {
           var encoding = GptEncoding.GetEncoding("cl100k_base");  // SharpToken
           var tokens = encoding.Encode(text);
           int chunkIndex = 0;
           for (int start = 0; start < tokens.Count; start += StepSize)
           {
               ct.ThrowIfCancellationRequested();
               var window = tokens.Skip(start).Take(WindowSize).ToList();
               if (window.Count == 0) break;
               yield return new DocumentChunk(
                   documentId, chunkIndex++,
                   encoding.Decode(window),
                   window.Count
               );
           }
       }
   }
   ```

4. **`DocumentExtractionJob.ExecuteAsync`** — full orchestration (replaces US_018 stub):
   ```csharp
   [Queue("document-extraction")]
   [AutomaticRetry(Attempts = 3)]
   public async Task ExecuteAsync(Guid documentId, CancellationToken ct)
   {
       // Step 1: queued → processing
       await _context.ClinicalDocuments
           .Where(d => d.Id == documentId)
           .ExecuteUpdateAsync(s => s.SetProperty(d => d.ExtractionStatus,
               ExtractionDocumentStatus.Processing), ct);

       var doc = await _context.ClinicalDocuments
           .FirstAsync(d => d.Id == documentId, ct);

       // Step 2: extract text
       var decryptedUri = _dataProtector.Unprotect(doc.FileUri);
       var text = await _pdfExtractor.ExtractTextAsync(decryptedUri, ct);

       if (string.IsNullOrWhiteSpace(text))
       {
           // Scanned PDF — flag for manual review
           await _context.ClinicalDocuments
               .Where(d => d.Id == documentId)
               .ExecuteUpdateAsync(s => s.SetProperty(d => d.ExtractionStatus,
                   ExtractionDocumentStatus.ManualReview), ct);
           _auditLogger.Log(actor: "system", action: "DocumentFlaggedForManualReview",
               target: documentId, payload: new { Reason = "EmptyTextExtraction" });
           return;
       }

       // Step 3: chunk text → store chunks to staging table for embedding
       // (DocumentChunkEmbedding rows created with null embedding — filled by task_002)
       int chunkCount = 0;
       await foreach (var chunk in _chunker.ChunkAsync(documentId, text, ct))
       {
           _context.DocumentChunkEmbeddings.Add(new DocumentChunkEmbedding(
               documentId: chunk.DocumentId,
               chunkIndex: chunk.ChunkIndex,
               chunkText: chunk.ChunkText,
               tokenCount: chunk.TokenCount,
               embedding: null  // populated by EmbeddingGenerationJob (task_002)
           ));
           chunkCount++;
       }
       await _context.SaveChangesAsync(ct);

       // Step 4: enqueue embedding job
       BackgroundJob.Enqueue<EmbeddingGenerationJob>(j => j.ExecuteAsync(documentId, CancellationToken.None));
       _logger.LogInformation("DocumentExtractionJob complete: {DocumentId} → {ChunkCount} chunks queued.", documentId, chunkCount);
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
            DocumentExtractionJob.cs      ← us_018/task_002 stub → REPLACE body here
          Models/
            DocumentChunk.cs              ← THIS TASK (create)
          Services/
            PdfTextExtractor.cs           ← THIS TASK (create)
            DocumentChunker.cs            ← THIS TASK (create)
      ClinicalIntelligence.Domain/
        Entities/
          ClinicalDocument.cs             ← us_018/task_003
          DocumentChunkEmbedding.cs       ← us_019/task_003 (must exist)
      ClinicalIntelligence.Presentation/
        ServiceCollectionExtensions.cs    ← extend
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Models/DocumentChunk.cs` | Shared record: `(DocumentId, ChunkIndex, ChunkText, TokenCount)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/PdfTextExtractor.cs` | PdfPig text extractor with ordered page text concatenation |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/DocumentChunker.cs` | SharpToken sliding window chunker; `IAsyncEnumerable<DocumentChunk>` output |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/DocumentExtractionJob.cs` | Replace stub body with: status transition → PDF extract → chunk → stage rows → enqueue `EmbeddingGenerationJob` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `PdfTextExtractor` and `DocumentChunker` as scoped services; add `UglyToad.PdfPig` + `SharpToken` NuGet references |

---

## External References

- [UglyToad.PdfPig — .NET PDF parser (MIT)](https://github.com/UglyToad/PdfPig)
- [UglyToad.PdfPig — `GetWords()` reading-order extraction](https://github.com/UglyToad/PdfPig/wiki/Words)
- [SharpToken — .NET GPT tokenizer `cl100k_base`](https://github.com/dmitry-brazhenko/SharpToken)
- [AIR-R01 — 512-token chunks with 25% overlap](../.propel/context/docs/design.md#AIR-R01)
- [Hangfire — `[AutomaticRetry(Attempts = 3)]`](https://docs.hangfire.io/en/latest/background-methods/dealing-with-exceptions.html)
- [EF Core 8 — `ExecuteUpdateAsync` single-field update](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-7.0/whatsnew#executeupdate-and-executedelete-bulk-updates)
- [NFR-002 — document upload to verified view < 2 minutes](../.propel/context/docs/design.md#NFR-002)
- [NFR-015 — OSS-only library constraint](../.propel/context/docs/design.md#NFR-015)

---

## Build Commands

```bash
cd server
dotnet add src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application package UglyToad.PdfPig
dotnet add src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application package SharpToken
dotnet build
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] Unit test: `DocumentChunker` with 1024-token input yields 3 chunks at indices 0/1/2 with correct 128-token overlap between consecutive chunks
- [ ] Unit test: `DocumentChunker` with 100-token input yields 1 chunk (single window, no step needed)
- [ ] Unit test: `PdfTextExtractor` returns empty string for a minimal scanned-image PDF (no text layer)
- [ ] Unit test: `DocumentExtractionJob` with empty text → sets status `ManualReview`, writes AuditLog, does NOT call `BackgroundJob.Enqueue`
- [ ] Unit test: `DocumentExtractionJob` with valid text → stages `DocumentChunkEmbedding` rows with `null` embedding, then calls `BackgroundJob.Enqueue<EmbeddingGenerationJob>`
- [ ] Integration test: full job run on a real test PDF → `DocumentChunkEmbedding` row count matches expected chunk count
- [ ] Verify `cl100k_base` encoding is used (GPT-4 tokenizer matches `text-embedding-3-small`)

---

## Implementation Checklist

- [x] Add `UglyToad.PdfPig` and `SharpToken` NuGet packages to `ClinicalIntelligence.Application`
- [x] Create `DocumentChunk` record with 4 fields: `DocumentId`, `ChunkIndex`, `ChunkText`, `TokenCount`
- [x] Implement `PdfTextExtractor.ExtractTextAsync` using `PdfDocument.Open(stream).GetPages()` with `GetWords()` for reading-order text
- [x] Implement `DocumentChunker.ChunkAsync` with `cl100k_base` encoding, window=512, step=384; yield `IAsyncEnumerable<DocumentChunk>`
- [x] Replace `DocumentExtractionJob` stub body: `Queued→Processing` update → PDF extract → empty-text guard (→`ManualReview` + AuditLog) → chunk loop → stage chunks via `IChunkStagingService` → enqueue `EmbeddingGenerationJob`
- [x] Register `PdfTextExtractor`, `DocumentChunker`, `IChunkStagingService` (→`NullChunkStagingService` stub), and `EmbeddingGenerationJob` in `ServiceCollectionExtensions.cs`
- [x] Verify `[AutomaticRetry(Attempts = 3)]` and `[Queue("document-extraction")]` attributes on `DocumentExtractionJob`
