// Per-file upload queue management hook (US_018, AC-2).
// Handles: validation, concurrent uploads, per-file progress, error isolation.
import { useCallback, useState } from 'react';

import { type DocumentRecord, uploadDocument, validatePdf } from '@/api/documents';

// ── Types ─────────────────────────────────────────────────────────────────────

export type FileUploadStatus = 'pending' | 'uploading' | 'done' | 'error' | 'rejected';

export interface FileUploadEntry {
  /** Stable random ID for React key and state lookup. */
  id: string;
  file: File;
  /** 0–100 — driven by Axios onUploadProgress (AC-2). */
  progress: number;
  status: FileUploadStatus;
  result?: DocumentRecord;
  errorMsg?: string;
}

export interface UseDocumentUploadReturn {
  queue: FileUploadEntry[];
  /** Add files to the queue; runs magic-bytes + size validation before enqueueing. */
  addFiles: (files: FileList | File[]) => Promise<void>;
  /** Trigger upload for all `pending` entries in the queue. */
  uploadAll: (onComplete?: (records: DocumentRecord[]) => void) => Promise<void>;
  /** Remove a single entry from the queue (before or after upload). */
  removeEntry: (id: string) => void;
  /** Clears entries with `done` status only (post-upload cleanup). */
  clearCompleted: () => void;
  /** True when at least one entry is in `uploading` state. */
  isUploading: boolean;
  /** Inline validation message to display below the drop zone. */
  validationError: string | null;
  clearValidationError: () => void;
}

// ── Hook ──────────────────────────────────────────────────────────────────────

/**
 * Manages per-file upload queue with client-side validation and Axios progress tracking.
 *
 * Key behaviours:
 * - Validates each file (magic bytes + 25 MB) before adding to queue (AC-4)
 * - Uploads fire concurrently — one failure never cancels other files (edge case)
 * - Progress callbacks update individual entries without re-mounting other rows (AC-2)
 */
export function useDocumentUpload(): UseDocumentUploadReturn {
  const [queue, setQueue] = useState<FileUploadEntry[]>([]);
  const [validationError, setValidationError] = useState<string | null>(null);

  // ── State helpers ────────────────────────────────────────────────────────

  const patchEntry = useCallback(
    (id: string, patch: Partial<FileUploadEntry>) =>
      setQueue((prev) =>
        prev.map((e) => (e.id === id ? { ...e, ...patch } : e)),
      ),
    [],
  );

  // ── addFiles ─────────────────────────────────────────────────────────────

  const addFiles = useCallback(
    async (files: FileList | File[]) => {
      setValidationError(null);
      const fileArray = Array.from(files);

      const entries: FileUploadEntry[] = await Promise.all(
        fileArray.map(async (file) => {
          const validation = await validatePdf(file);

          if (validation === 'too_large') {
            return {
              id: crypto.randomUUID(),
              file,
              progress: 0,
              status: 'rejected' as FileUploadStatus,
              errorMsg: 'File size must be under 25MB. Please select a smaller file.',
            };
          }

          if (validation === 'invalid_type') {
            return {
              id: crypto.randomUUID(),
              file,
              progress: 0,
              status: 'rejected' as FileUploadStatus,
              errorMsg: 'Unsupported file type. Only PDF files are accepted.',
            };
          }

          return {
            id: crypto.randomUUID(),
            file,
            progress: 0,
            status: 'pending' as FileUploadStatus,
          };
        }),
      );

      // Surface the first rejection error inline (AC-4)
      const firstRejected = entries.find((e) => e.status === 'rejected');
      if (firstRejected?.errorMsg) {
        setValidationError(firstRejected.errorMsg);
      }

      // Only enqueue valid (pending) files — do NOT add rejected entries to the list
      const validEntries = entries.filter((e) => e.status === 'pending');
      if (validEntries.length > 0) {
        setQueue((prev) => [...prev, ...validEntries]);
      }
    },
    [],
  );

  // ── uploadAll ────────────────────────────────────────────────────────────

  const uploadAll = useCallback(
    async (onComplete?: (records: DocumentRecord[]) => void) => {
      const pending = queue.filter((e) => e.status === 'pending');
      if (pending.length === 0) return;

      // Mark all pending as uploading before any async work
      setQueue((prev) =>
        prev.map((e) =>
          e.status === 'pending' ? { ...e, status: 'uploading' as FileUploadStatus } : e,
        ),
      );

      // Concurrent uploads — independent Promise.allSettled so one failure
      // does not cancel others (edge case requirement).
      const results = await Promise.allSettled(
        pending.map(async (entry) => {
          try {
            const record = await uploadDocument(entry.file, (pct) =>
              patchEntry(entry.id, { progress: pct }),
            );
            patchEntry(entry.id, { status: 'done', progress: 100, result: record });
            return record;
          } catch (err) {
            const msg =
              err instanceof Error ? err.message : 'Upload failed. Please try again.';
            patchEntry(entry.id, { status: 'error', errorMsg: msg });
            throw err;
          }
        }),
      );

      // Collect successfully uploaded records for caller (e.g. navigate to list)
      const uploaded = results
        .filter((r): r is PromiseFulfilledResult<DocumentRecord> => r.status === 'fulfilled')
        .map((r) => r.value);

      onComplete?.(uploaded);
    },
    [queue, patchEntry],
  );

  // ── Misc helpers ─────────────────────────────────────────────────────────

  const removeEntry = useCallback(
    (id: string) => setQueue((prev) => prev.filter((e) => e.id !== id)),
    [],
  );

  const clearCompleted = useCallback(
    () => setQueue((prev) => prev.filter((e) => e.status !== 'done')),
    [],
  );

  const clearValidationError = useCallback(() => setValidationError(null), []);

  const isUploading = queue.some((e) => e.status === 'uploading');

  return {
    queue,
    addFiles,
    uploadAll,
    removeEntry,
    clearCompleted,
    isUploading,
    validationError,
    clearValidationError,
  };
}
