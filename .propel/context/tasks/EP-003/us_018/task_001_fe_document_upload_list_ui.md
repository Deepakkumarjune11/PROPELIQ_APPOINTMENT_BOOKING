# Task - task_001_fe_document_upload_list_ui

## Requirement Reference

- **User Story**: US_018 — Document Upload & Storage
- **Story Location**: `.propel/context/tasks/EP-003/us_018/us_018.md`
- **Acceptance Criteria**:
  - AC-1: When I navigate to the document upload screen, I see a drag-and-drop upload zone that accepts PDF files up to 25MB per FR-010.
  - AC-2: When the upload begins, per-file progress bars display upload status; the system validates file type and size before persistence.
  - AC-3: When upload completes, the document list shows the newly added record with "queued" status badge.
  - AC-4: When an invalid file type is uploaded, the system displays "Unsupported file type" error and rejects the file without adding it to the list.
  - AC-5: After upload, the document status shows "queued" in the document list; status polling updates badges as processing progresses.
- **Edge Cases**:
  - File exceeds 25MB → validation toast "File size must be under 25MB. Please select a smaller file." (figma_spec.md edge case); file is not added to the upload queue.
  - Multiple concurrent uploads → each file has its own independent progress bar and status; one file failure does not block others.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-014-document-upload.html`, `.propel/context/wireframes/Hi-Fi/wireframe-SCR-015-document-list.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-014`, `.propel/context/docs/figma_spec.md#SCR-015` |
| **UXR Requirements** | UXR-101 (WCAG 2.2 AA), UXR-201 (responsive 320px/768px/1024px+), UXR-401 (<200ms loading feedback), UXR-402 (success/error toast for upload completion), UXR-404 (optimistic UI with rollback on failure), UXR-501 (actionable error messages with recovery paths) |
| **Design Tokens** | `designsystem.md#colors` (`primary.500: #2196F3` upload zone border/button; `success.main: #4CAF50` completed badge; `warning.main: #FF9800` processing/queued badge; `warning.700: #F57C00` manual-review badge; `error.main: #F44336` failed/error state), `designsystem.md#typography`, `designsystem.md#spacing` (8px grid) |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local file exists at specified path

### CRITICAL: Wireframe Implementation Requirement

- **MUST** open both wireframe files and match layout for:
  - **SCR-014 Document Upload**: `FileUpload` drag-drop zone (dashed border, upload icon, "Drag files here or click to browse"), stacked `LinearProgress` bars per uploading file, `Alert` for validation errors, `Button` (Browse Files + Upload).
  - **SCR-015 Document List**: `Table` with columns (filename, upload date, status `Badge`, actions `IconButton`); processing status badges: "Processing" (warning/yellow), "Completed" (success/green), "Manual Review" (warning-dark/orange), "Queued" (info/blue); `IconButton` delete with confirmation `Dialog`.
- **MUST** implement all required states:
  - **SCR-014**: Default (idle drop zone), Loading (file progress bars with % labels), Error (MUI `Alert` + Retry), Validation (file type / size error inline below the drop zone).
  - **SCR-015**: Default (document rows), Loading (`Skeleton` rows), Empty (no documents + "Upload your first document" CTA), Error (MUI `Alert` + Retry).
- **MUST** validate implementation against wireframes at breakpoints: 375px, 768px, 1440px.
- Run `/analyze-ux` after implementation to verify pixel-perfect alignment.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| UI Components | Material-UI (MUI) | 5.x |
| State Management | React Query (TanStack Query) | 4.x |
| State Management | Zustand | 4.x |
| HTTP Client | Axios (with upload progress) | 1.x |
| Routing | React Router DOM | 6.x |
| Language | TypeScript | 5.x |
| Build | Vite | 5.x |

> All code and libraries MUST be compatible with versions above. No third-party drag-drop libraries needed — use native HTML5 drag events + MUI `Box` styled drop zone (KISS principle, satisfies NFR-015).

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

Implement two patient-facing screens completing the document upload flow (FL-004):

**SCR-014 — Document Upload** (P1):
- Renders an MUI-styled drag-and-drop upload zone (native HTML5 `dragover`/`drop` events + hidden `<input type="file" multiple accept="application/pdf">`).
- Client-side validation before any API call: file type must be `application/pdf` (magic bytes check: first 4 bytes = `%PDF`); file size ≤ 25 971 520 bytes (25 × 1024 × 1024). Failures show MUI `Alert` (severity="error") inline below the drop zone: "Unsupported file type. Only PDF files are accepted." or "File size must be under 25MB. Please select a smaller file."
- For each valid file queued for upload, render a stacked `LinearProgress` component with filename label and `{n}%` text. Progress driven by Axios `onUploadProgress` callback.
- On upload success (201), transition to SCR-015 document list view with a success `Toast`.
- States: Default, Loading (progress bars), Error (Alert + Retry button), Validation (inline alert).

**SCR-015 — Document List** (P1):
- Fetches documents via `GET /api/v1/documents` (React Query, 5s refetch interval when any document has status `queued` or `processing`, otherwise 0 / manual refetch only).
- Renders MUI `Table` with columns: filename, upload date (formatted `dd MMM yyyy HH:mm`), status `Chip`, actions (`IconButton` delete).
- Status badge colour mapping: `queued` → info (blue), `processing` → warning (orange), `completed` → success (green), `manual_review` → warning-dark (amber), `failed` → error (red).
- Delete: `IconButton` triggers confirmation `Dialog` ("Are you sure you want to delete this document?"); on confirm calls `DELETE /api/v1/documents/{id}`.
- Breadcrumb: `Home > My Documents` (per UXR-002 scope — patient nav).
- States: Default, Loading (Skeleton rows × 3), Empty ("Upload your first document" + CTA), Error (Alert + Retry).

---

## Dependent Tasks

- **task_002_be_document_upload_api.md** (US_018) — `POST /api/v1/documents/upload`, `GET /api/v1/documents`, `DELETE /api/v1/documents/{id}` must be available.
- **task_003_db_clinical_document_schema.md** (US_018) — `ClinicalDocument` table and `ExtractionDocumentStatus` enum must exist for API responses.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/documents/DocumentUploadPage.tsx` | SCR-014 — drag-drop zone, per-file progress bars, validation alerts, all states |
| CREATE | `client/src/pages/documents/DocumentListPage.tsx` | SCR-015 — status badge table, Skeleton loading, Empty state, delete confirmation dialog |
| CREATE | `client/src/hooks/useDocumentUpload.ts` | Manages per-file upload queue state; Axios multipart POST with progress tracking |
| CREATE | `client/src/hooks/useDocuments.ts` | React Query hook for `GET /api/v1/documents`; polling when any doc is queued/processing |
| CREATE | `client/src/api/documents.ts` | Typed API functions: `uploadDocument`, `getDocuments`, `deleteDocument` |
| MODIFY | `client/src/App.tsx` | Add `/documents` and `/documents/upload` routes inside `<AuthenticatedLayout>` |

---

## Implementation Plan

1. **`documents.ts` API layer** — typed functions with Axios:
   ```typescript
   export interface DocumentRecord {
     documentId: string;
     originalFileName: string;
     fileSizeBytes: number;
     uploadedAt: string;          // ISO 8601
     extractionStatus: 'queued' | 'processing' | 'completed' | 'manual_review' | 'failed';
     encounterId: string | null;
   }

   // POST /api/v1/documents/upload — multipart/form-data, returns DocumentRecord
   export async function uploadDocument(
     file: File,
     onProgress: (pct: number) => void,
     encounterId?: string
   ): Promise<DocumentRecord>

   // GET /api/v1/documents — returns DocumentRecord[]
   export async function getDocuments(): Promise<DocumentRecord[]>

   // DELETE /api/v1/documents/{id} — soft delete
   export async function deleteDocument(documentId: string): Promise<void>
   ```

2. **Client-side validation helper**:
   ```typescript
   const MAX_BYTES = 25 * 1024 * 1024; // 25MB
   // Check magic bytes (first 4): %PDF = [0x25, 0x50, 0x44, 0x46]
   async function validatePdf(file: File): Promise<'ok' | 'invalid_type' | 'too_large'> {
     if (file.size > MAX_BYTES) return 'too_large';
     const buf = await file.slice(0, 4).arrayBuffer();
     const header = new Uint8Array(buf);
     if (header[0] !== 0x25 || header[1] !== 0x50 || header[2] !== 0x44 || header[3] !== 0x46)
       return 'invalid_type';
     return 'ok';
   }
   ```

3. **`useDocumentUpload.ts`** — per-file upload state management:
   ```typescript
   interface FileUploadEntry {
     id: string;          // crypto.randomUUID()
     file: File;
     progress: number;    // 0–100
     status: 'pending' | 'uploading' | 'done' | 'error' | 'rejected';
     errorMsg?: string;
   }
   // useState<FileUploadEntry[]> for the queue
   // For each valid file: call uploadDocument() with onProgress callback
   // Files validated with validatePdf() before adding to queue
   ```

4. **`DocumentUploadPage.tsx`** — drag-drop + progress stack:
   ```tsx
   // onDrop / onChange → validatePdf → add to queue or show Alert
   // MUI Box with sx={{ border: '2px dashed', borderColor: isDragOver ? 'primary.main' : 'grey.400' }}
   // Stack of LinearProgress per uploading file
   // "Browse Files" Button + hidden <input type="file" multiple accept=".pdf,application/pdf">
   ```

5. **`useDocuments.ts`** — conditional polling:
   ```typescript
   const { data, isLoading, isError, refetch } = useQuery({
     queryKey: ['documents'],
     queryFn: getDocuments,
     refetchInterval: (data) =>
       data?.some(d => d.extractionStatus === 'queued' || d.extractionStatus === 'processing')
         ? 5000 : false,
   });
   ```

6. **`DocumentListPage.tsx`** — table + delete dialog:
   ```tsx
   // MUI Table with columns: filename, formatted date, status Chip, delete IconButton
   // statusColorMap: queued→'info', processing→'warning', completed→'success', manual_review→'warning' (sx.color='#F57C00'), failed→'error'
   // Delete confirmation: Dialog → onConfirm → deleteDocument(id) → queryClient.invalidateQueries(['documents'])
   ```

---

## Current Project State

```
client/src/
  pages/
    LoginPage.tsx                  ← existing
    staff/                         ← us_016, us_017
    documents/                     ← THIS TASK (create)
  api/
    staff.ts                       ← us_016/task_001
    documents.ts                   ← THIS TASK (create)
  hooks/
    useSameDayQueue.ts             ← us_017/task_001
    useDocumentUpload.ts           ← THIS TASK (create)
    useDocuments.ts                ← THIS TASK (create)
  components/
    layout/
      AuthenticatedLayout.tsx      ← existing
  App.tsx                          ← add document routes
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/documents/DocumentUploadPage.tsx` | SCR-014: drag-drop zone, per-file progress bars, validation, all 4 states |
| CREATE | `client/src/pages/documents/DocumentListPage.tsx` | SCR-015: status table, Skeleton, Empty state, delete dialog |
| CREATE | `client/src/hooks/useDocumentUpload.ts` | Per-file upload queue with Axios progress tracking |
| CREATE | `client/src/hooks/useDocuments.ts` | React Query hook with conditional 5s polling |
| CREATE | `client/src/api/documents.ts` | `DocumentRecord` type + `uploadDocument`, `getDocuments`, `deleteDocument` |
| MODIFY | `client/src/App.tsx` | Add `/documents` and `/documents/upload` routes inside `<AuthenticatedLayout>` |

---

## External References

- [Axios — upload progress with `onUploadProgress`](https://axios-http.com/docs/req_config)
- [HTML5 File API — magic bytes validation (ArrayBuffer slice)](https://developer.mozilla.org/en-US/docs/Web/API/FileReader)
- [React Query v4 — `refetchInterval` conditional polling](https://tanstack.com/query/v4/docs/react/reference/useQuery)
- [MUI LinearProgress — determinate with value prop](https://mui.com/material-ui/react-progress/#linear-determinate)
- [MUI FileUpload drag-and-drop pattern (no library)](https://mui.com/material-ui/react-box/#system-properties)
- [UXR-401 — loading feedback <200ms](../.propel/context/docs/figma_spec.md)
- [UXR-404 — optimistic UI + rollback](../.propel/context/docs/figma_spec.md)
- [NFR-003 — TLS in transit, PHI handling](../.propel/context/docs/design.md#NFR-003)

---

## Build Commands

```bash
cd client
npm run dev
npm run type-check
npm run build
```

---

## Implementation Validation Strategy

- [ ] Client-side PDF magic bytes check rejects non-PDF files before any API call
- [ ] Files > 25MB show "File size must be under 25MB. Please select a smaller file." toast (UXR-501)
- [ ] Per-file `LinearProgress` updates in real-time with Axios `onUploadProgress`
- [ ] Multiple concurrent uploads proceed independently; one failure does not cancel others
- [ ] Document list polls every 5s while any document is `queued` or `processing`; stops polling when all `completed`
- [ ] Delete confirmation dialog appears; cancel does NOT delete; confirm soft-deletes and refreshes list
- [ ] **[UI Tasks]** Visual comparison against wireframes at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment

---

## Implementation Checklist

- [x] Create `client/src/api/documents.ts` with `DocumentRecord` type, `uploadDocument` (multipart + progress), `getDocuments`, `deleteDocument`
- [x] Create `useDocumentUpload.ts` managing `FileUploadEntry[]` queue with per-file Axios upload and progress callbacks
- [x] Implement PDF client-side validation (magic bytes + 25MB size limit) before adding files to upload queue
- [x] Create `DocumentUploadPage.tsx` (SCR-014): drag-drop Box, stacked `LinearProgress`, validation `Alert`, all 4 states (Default/Loading/Error/Validation)
- [x] Create `useDocuments.ts` React Query hook with `refetchInterval` conditional 5s polling on queued/processing statuses
- [x] Create `DocumentListPage.tsx` (SCR-015): `Table` with status `Chip` colour mapping, `Skeleton` loading, Empty state CTA, delete `IconButton` + `Dialog`
- [x] Add `/documents` and `/documents/upload` routes to `App.tsx` inside `<AuthenticatedLayout>`
- [x] **[UI Tasks - MANDATORY]** Reference wireframes from Design References table during implementation
- [x] **[UI Tasks - MANDATORY]** Validate UI matches wireframes before marking task complete
