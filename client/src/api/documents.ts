// API client for document upload endpoints (US_018).
// POST /api/v1/documents/upload — multipart/form-data, returns DocumentRecord
// GET  /api/v1/documents        — returns DocumentRecord[]
// DELETE /api/v1/documents/{id} — soft delete
import api from '@/lib/api';

// ── Domain types ─────────────────────────────────────────────────────────────

export type ExtractionStatus =
  | 'queued'
  | 'processing'
  | 'completed'
  | 'manual_review'
  | 'failed';

export interface DocumentRecord {
  documentId: string;
  originalFileName: string;
  fileSizeBytes: number;
  /** ISO 8601 datetime string e.g. "2026-04-20T09:00:00Z" */
  uploadedAt: string;
  extractionStatus: ExtractionStatus;
  encounterId: string | null;
}

/** Thrown on any non-2xx response from a document API call. */
export class DocumentApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'DocumentApiError';
  }
}

// ── Client-side validation ────────────────────────────────────────────────────

export const MAX_FILE_BYTES = 25 * 1024 * 1024; // 25 MB

/**
 * Validates a File before upload:
 * - magic bytes `%PDF` (0x25 0x50 0x44 0x46) — ensures actual PDF content
 * - file size ≤ 25 MB (FR-010)
 *
 * @returns `'ok'` | `'invalid_type'` | `'too_large'`
 */
export async function validatePdf(
  file: File,
): Promise<'ok' | 'invalid_type' | 'too_large'> {
  if (file.size > MAX_FILE_BYTES) return 'too_large';

  const buf = await file.slice(0, 4).arrayBuffer();
  const header = new Uint8Array(buf);

  // %PDF magic bytes check
  if (
    header[0] !== 0x25 ||
    header[1] !== 0x50 ||
    header[2] !== 0x44 ||
    header[3] !== 0x46
  ) {
    return 'invalid_type';
  }

  return 'ok';
}



// ── API functions ─────────────────────────────────────────────────────────────

/**
 * Uploads a single PDF file via multipart/form-data.
 * Uses Axios so `onUploadProgress` can drive per-file `LinearProgress` bars (AC-2).
 *
 * POST /api/v1/documents/upload
 */
export async function uploadDocument(
  file: File,
  onProgress: (pct: number) => void,
  encounterId?: string,
): Promise<DocumentRecord> {
  const formData = new FormData();
  formData.append('file', file);
  if (encounterId) formData.append('encounterId', encounterId);

  // Do NOT set Content-Type — axios sets multipart boundary automatically.
  // Authorization header is injected by the api interceptor in lib/api.ts.
  const response = await api.post<DocumentRecord>('/api/v1/documents/upload', formData, {
    onUploadProgress(progressEvent) {
      const total = progressEvent.total ?? file.size;
      const pct = Math.round((progressEvent.loaded / total) * 100);
      onProgress(pct);
    },
  });

  return response.data;
}

/**
 * Fetches the patient's document list (most recent first).
 * GET /api/v1/documents
 */
export async function getDocuments(): Promise<DocumentRecord[]> {
  const response = await api.get<DocumentRecord[]>('/api/v1/documents');
  return response.data;
}

/**
 * Soft-deletes a document by ID.
 * DELETE /api/v1/documents/{id}
 */
export async function deleteDocument(documentId: string): Promise<void> {
  await api.delete(`/api/v1/documents/${encodeURIComponent(documentId)}`);
}
