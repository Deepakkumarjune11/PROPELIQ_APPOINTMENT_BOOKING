# Task - task_002_be_document_upload_api

## Requirement Reference

- **User Story**: US_018 — Document Upload & Storage
- **Story Location**: `.propel/context/tasks/EP-003/us_018/us_018.md`
- **Acceptance Criteria**:
  - AC-2: When the upload begins, the system validates file type (PDF only) and size (≤ 25MB) before persistence; rejects invalid files without creating a record.
  - AC-3: When a valid document is uploaded, the original file is stored with a file reference URI, a `ClinicalDocument` record is created with patient and optional encounter association, and an audit log entry records the upload event.
  - AC-4: When an invalid file type is uploaded, the system returns an error response and rejects the file without creating a document record.
  - AC-5: After upload completes, a background extraction job is queued (Hangfire `BackgroundJob.Enqueue`) and the document status is set to `queued`.
- **Edge Cases**:
  - File exceeds 25MB → 422 Unprocessable Entity with `{ "error": "File size must be under 25MB. Please select a smaller file." }`.
  - Non-PDF file → 422 Unprocessable Entity with `{ "error": "Unsupported file type. Only PDF files are accepted." }` (validates both `Content-Type` header and magic bytes `%PDF`).
  - Concurrent uploads of multiple files → each request is independently handled; no shared mutable state between requests.
  - `GET /api/v1/documents` by a patient → only returns documents belonging to the authenticated patient (patient ownership enforced via JWT `sub` claim).

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
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| Background Jobs | Hangfire | 1.8.x |
| Security - Auth | ASP.NET Core Identity + JWT Bearer | 8.0 |
| Security - Encryption | .NET Data Protection API | 8.0 |
| Logging | Serilog | 3.x |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |
| API Documentation | Swagger / OpenAPI | 6.x |

> All code and libraries MUST be compatible with versions above. File storage uses `IFileStorageService` abstraction backed by local filesystem for phase-1 (NFR-015 free/OSS). PHI column encryption (`ClinicalDocument.FileUri`) uses .NET Data Protection API per DR-015.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

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

Implement three REST endpoints and a Hangfire job stub within the `ClinicalIntelligence` bounded context (patient sub-module) to support document upload, listing, and extraction job queuing:

1. **`POST /api/v1/documents/upload`** — accepts `multipart/form-data` with file + optional `encounterId`; validates type (PDF) and size (≤ 25MB) server-side; persists file via `IFileStorageService`; creates `ClinicalDocument` record with status `queued`; writes AuditLog; enqueues `DocumentExtractionJob`.
2. **`GET /api/v1/documents`** — returns paginated list of `ClinicalDocumentDto` for the authenticated patient; filters by `PatientId` from JWT `sub` claim (ownership enforced).
3. **`DELETE /api/v1/documents/{id}`** — soft-deletes the document (sets `IsDeleted = true`); validates patient ownership; writes AuditLog; does NOT delete the physical file in phase-1 (retained for audit per DR-013).
4. **`DocumentExtractionJob` stub** — Hangfire background job class queued on upload; sets `ExtractionStatus = Processing` then immediately exits (placeholder body for US_019 AI extraction pipeline); registered with `[Queue("document-extraction")]`.

All endpoints: `[Authorize(Roles = "Patient")]`. Validation returns 422 with structured error. AuditLog written for every create/delete.

---

## Dependent Tasks

- **task_003_db_clinical_document_schema.md** (US_018) — `ClinicalDocuments` table, `ClinicalDocument` entity, and `ExtractionDocumentStatus` enum must exist.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Commands/UploadDocumentCommand.cs` | CQRS Command + Handler: validate → store file → create record → audit → enqueue job |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Queries/GetPatientDocumentsQuery.cs` | CQRS Query + Handler: patient-owned document list |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Commands/DeleteDocumentCommand.cs` | CQRS Command + Handler: ownership check → soft delete → audit |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Dtos/ClinicalDocumentDto.cs` | `{ DocumentId, OriginalFileName, FileSizeBytes, UploadedAt, ExtractionStatus, EncounterId? }` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/DocumentExtractionJob.cs` | Hangfire job stub — updates status to `Processing`, placeholder for US_019 extraction |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Infrastructure/IFileStorageService.cs` | File storage abstraction: `StoreAsync(Stream, fileName) → string fileUri`, `ReadAsync(fileUri) → Stream` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Infrastructure/LocalFileStorageService.cs` | Phase-1 local disk implementation of `IFileStorageService` (writes to `./uploads/documents/`) |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/Controllers/DocumentsController.cs` | `[Authorize(Roles = "Patient")]` controller: POST upload, GET list, DELETE |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `IFileStorageService`, MediatR handlers, Hangfire queue |

---

## Implementation Plan

1. **`IFileStorageService` + `LocalFileStorageService`** — file storage abstraction:
   ```csharp
   public interface IFileStorageService
   {
       Task<string> StoreAsync(Stream fileStream, string originalFileName, CancellationToken ct = default);
       Task<Stream> ReadAsync(string fileUri, CancellationToken ct = default);
   }

   // Phase-1: LocalFileStorageService
   // - Stores files in ./uploads/documents/{patientId}/{newGuid}_{sanitizedFileName}
   // - Returns relative path as URI (e.g., "uploads/documents/{patientId}/{file}")
   // - SECURITY: Sanitize filename with Path.GetFileName() to prevent path traversal (OWASP A01)
   public class LocalFileStorageService : IFileStorageService { ... }
   ```

2. **Server-side validation in `UploadDocumentCommand.Handler`**:
   ```csharp
   const long MaxBytes = 25L * 1024 * 1024;  // 25MB

   // Validate size FIRST (cheap check)
   if (file.Length > MaxBytes)
       throw new ValidationException("File size must be under 25MB. Please select a smaller file.");

   // Validate PDF magic bytes (OWASP A05 — don't trust Content-Type alone)
   var headerBuffer = new byte[4];
   await file.OpenReadStream().ReadExactlyAsync(headerBuffer, 0, 4, cancellationToken);
   if (headerBuffer is not [0x25, 0x50, 0x44, 0x46])
       throw new ValidationException("Unsupported file type. Only PDF files are accepted.");
   ```

3. **`UploadDocumentCommand.Handler`** — full flow per UC-004 sequence:
   ```csharp
   // 1. Store file
   var fileUri = await _fileStorage.StoreAsync(fileStream, command.OriginalFileName, ct);
   // 2. Encrypt fileUri before persisting (DR-015 PHI column)
   var encryptedUri = _dataProtector.Protect(fileUri);
   // 3. Create ClinicalDocument entity
   var document = new ClinicalDocument(
       patientId: command.PatientId,
       encounterId: command.EncounterId,
       fileUri: encryptedUri,
       originalFileName: command.OriginalFileName,
       fileSizeBytes: command.FileSizeBytes,
       status: ExtractionDocumentStatus.Queued
   );
   _context.ClinicalDocuments.Add(document);
   // 4. AuditLog
   _auditLogger.Log(actor: command.PatientId, action: "DocumentUploaded", target: document.Id,
       payload: new { document.OriginalFileName, document.FileSizeBytes });
   await _context.SaveChangesAsync(ct);
   // 5. Enqueue extraction job
   BackgroundJob.Enqueue<DocumentExtractionJob>(j => j.ExecuteAsync(document.Id, CancellationToken.None));
   return document.Id;
   ```

4. **`GetPatientDocumentsQuery.Handler`** — patient-owned list with ownership enforcement:
   ```csharp
   return await _context.ClinicalDocuments
       .Where(d => d.PatientId == query.PatientId && !d.IsDeleted)
       .OrderByDescending(d => d.UploadedAt)
       .Select(d => new ClinicalDocumentDto(
           d.Id, d.OriginalFileName, d.FileSizeBytes, d.UploadedAt,
           d.ExtractionStatus.ToString(), d.EncounterId))
       .ToListAsync(cancellationToken);
   ```

5. **`DocumentExtractionJob` stub** — Hangfire placeholder:
   ```csharp
   [Queue("document-extraction")]
   public class DocumentExtractionJob(AppDbContext context, ILogger<DocumentExtractionJob> logger)
   {
       public async Task ExecuteAsync(Guid documentId, CancellationToken ct)
       {
           // Update status to Processing (signals UI badge transition)
           await context.ClinicalDocuments
               .Where(d => d.Id == documentId)
               .ExecuteUpdateAsync(s => s.SetProperty(d => d.ExtractionStatus,
                   ExtractionDocumentStatus.Processing), ct);
           // TODO US_019: implement AI extraction pipeline (RAG + fact persistence)
           logger.LogInformation("DocumentExtractionJob queued for {DocumentId}. AI pipeline not yet implemented.", documentId);
       }
   }
   ```

6. **`DocumentsController`** — endpoints with `[Authorize(Roles = "Patient")]`:
   ```csharp
   [HttpPost("upload")]
   [RequestSizeLimit(26_214_400)]   // 25MB + overhead; hard-stop at web server level
   [Consumes("multipart/form-data")]
   public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] Guid? encounterId)

   [HttpGet]
   public async Task<IActionResult> GetDocuments()

   [HttpDelete("{id:guid}")]
   public async Task<IActionResult> Delete(Guid id)
   ```

---

## Current Project State

```
server/src/
  Modules/
    ClinicalIntelligence/
      ClinicalIntelligence.Application/
        Documents/
          Commands/                  ← THIS TASK (create)
          Queries/                   ← THIS TASK (create)
          Dtos/                      ← THIS TASK (create)
          Jobs/                      ← THIS TASK (create)
        Infrastructure/
          IFileStorageService.cs     ← THIS TASK (create)
          LocalFileStorageService.cs ← THIS TASK (create)
      ClinicalIntelligence.Domain/   ← extended by task_003 (entity exists)
      ClinicalIntelligence.Presentation/
        Controllers/
          DocumentsController.cs     ← THIS TASK (create)
        ServiceCollectionExtensions.cs ← extend
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Commands/UploadDocumentCommand.cs` | Validate → store → create record → audit log → enqueue job |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Queries/GetPatientDocumentsQuery.cs` | Patient-owned document list query |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Commands/DeleteDocumentCommand.cs` | Ownership-validated soft delete + audit log |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Dtos/ClinicalDocumentDto.cs` | Response DTO with 6 fields |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Jobs/DocumentExtractionJob.cs` | Hangfire stub — sets status `Processing`; placeholder for US_019 |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Infrastructure/IFileStorageService.cs` | File storage abstraction interface |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Infrastructure/LocalFileStorageService.cs` | Phase-1 local disk implementation with path traversal prevention |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/Controllers/DocumentsController.cs` | POST upload, GET list, DELETE with `[Authorize(Roles = "Patient")]` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `IFileStorageService` → `LocalFileStorageService`, MediatR handlers, Hangfire `document-extraction` queue |

---

## External References

- [ASP.NET Core 8 — `IFormFile` multipart upload with size limits](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-8.0)
- [.NET Data Protection API — `IDataProtector.Protect/Unprotect`](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/using-data-protection?view=aspnetcore-8.0)
- [Hangfire — `BackgroundJob.Enqueue<T>` with typed job classes](https://docs.hangfire.io/en/latest/background-methods/calling-methods-in-background.html)
- [Hangfire — Named queues `[Queue("document-extraction")]`](https://docs.hangfire.io/en/latest/background-methods/using-multiple-queues.html)
- [EF Core 8 — `ExecuteUpdateAsync` bulk update](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-7.0/whatsnew#executeupdate-and-executedelete-bulk-updates)
- [OWASP A01 — Path Traversal prevention: `Path.GetFileName()`](https://owasp.org/www-community/attacks/Path_Traversal)
- [OWASP A05 — Magic bytes file type validation (beyond MIME header)](https://owasp.org/www-project-web-security-testing-guide/)
- [DR-004 — ClinicalDocument entity spec](../.propel/context/docs/design.md#DR-004)
- [DR-015 — PHI column encryption for ClinicalDocument.FileUri](../.propel/context/docs/design.md#DR-015)
- [NFR-002 — document upload to verified view < 2 minutes](../.propel/context/docs/design.md#NFR-002)
- [TR-009 — Hangfire for document extraction jobs](../.propel/context/docs/design.md#TR-009)

---

## Build Commands

```bash
cd server && dotnet restore ; dotnet build
dotnet run --project src/PropelIQ.Api/PropelIQ.Api.csproj
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] Unit test: size > 25MB returns `ValidationException` before any DB/file I/O
- [ ] Unit test: non-PDF magic bytes return `ValidationException` (Content-Type spoof rejected)
- [ ] Unit test: valid PDF upload creates `ClinicalDocument` with `ExtractionStatus = Queued`, stores file, writes AuditLog, and enqueues `DocumentExtractionJob`
- [ ] Unit test: `GetPatientDocumentsQuery` only returns documents for the requesting `patientId`
- [ ] Unit test: `DeleteDocumentCommand` with wrong `patientId` throws `ForbiddenException` (ownership enforced)
- [ ] Integration test: `POST /api/v1/documents/upload` with non-patient JWT returns 403
- [ ] Verify `LocalFileStorageService` uses `Path.GetFileName()` to prevent path traversal (OWASP A01)
- [ ] Verify `DocumentExtractionJob` updates status to `Processing` and is discoverable by Hangfire dashboard

---

## Implementation Checklist

- [ ] Create `IFileStorageService` interface and `LocalFileStorageService` phase-1 implementation (path traversal prevention via `Path.GetFileName()`)
- [ ] Implement `UploadDocumentCommand` handler: magic bytes + size validation → store file → encrypt URI (DR-015) → create `ClinicalDocument` (status=Queued) → AuditLog → `BackgroundJob.Enqueue<DocumentExtractionJob>`
- [ ] Implement `GetPatientDocumentsQuery` handler with `PatientId` ownership filter and `IsDeleted = false` predicate
- [ ] Implement `DeleteDocumentCommand` handler with ownership validation, `IsDeleted = true` soft delete, and AuditLog
- [ ] Create `DocumentExtractionJob` stub with `[Queue("document-extraction")]`; sets status to `Processing` via `ExecuteUpdateAsync`; logs placeholder TODO for US_019
- [ ] Create `DocumentsController` with `[Authorize(Roles = "Patient")]`, `[RequestSizeLimit(26214400)]` on upload, and correct 201/200/204 status codes
- [ ] Register `IFileStorageService`, all MediatR handlers, and Hangfire `document-extraction` queue in `ServiceCollectionExtensions.cs`
- [ ] Verify `ClinicalDocument.FileUri` is encrypted with `.NET Data Protection API` before DB persist (DR-015)
