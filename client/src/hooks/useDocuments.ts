// React Query hook for document list with conditional polling (US_018, AC-3, AC-5).
// Polls every 5 seconds while any document is in 'queued' or 'processing' state.
// Stops polling once all documents are in terminal states (completed, manual_review, failed).
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { type DocumentRecord, deleteDocument, getDocuments } from '@/api/documents';

export const DOCUMENTS_QUERY_KEY = ['documents'] as const;

// ── useDocuments ─────────────────────────────────────────────────────────────

/**
 * Fetches the authenticated patient's document list.
 *
 * Polling behaviour (AC-5):
 * - Polls every 5 seconds while any document has `queued` or `processing` status.
 * - Stops polling (returns `false`) when all documents are in terminal states.
 * - React Query v4 `refetchInterval` accepts a function for this conditional logic.
 */
export function useDocuments() {
  return useQuery<DocumentRecord[], Error>({
    queryKey: DOCUMENTS_QUERY_KEY,
    queryFn: getDocuments,
    refetchInterval(data) {
      const needsPolling = data?.some(
        (d) => d.extractionStatus === 'queued' || d.extractionStatus === 'processing',
      );
      return needsPolling ? 5_000 : false;
    },
    retry: 1,
  });
}

// ── useDeleteDocument ─────────────────────────────────────────────────────────

/**
 * Mutation: soft-deletes a document and invalidates the document list cache.
 * On success the list refetches automatically — no optimistic update needed
 * since delete is destructive and immediate BE confirmation is required (UXR-404).
 */
export function useDeleteDocument() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, string>({
    mutationFn: deleteDocument,
    onSuccess() {
      void queryClient.invalidateQueries({ queryKey: DOCUMENTS_QUERY_KEY });
    },
  });
}
