// React Query hook for fetching unresolved conflicts for a patient (US_022, FR-013).
import { useQuery } from '@tanstack/react-query';

import { type ConflictItemDto, getConflicts } from '@/api/conflicts';

export const CONFLICTS_QUERY_KEY = (patientId: string) =>
  ['conflicts', patientId] as const;

/**
 * Fetches the list of unresolved conflicts for the given patient.
 *
 * staleTime: 30 s — conflicts change only after a resolution POST or a new assembly run.
 * retry: 2 — transient network failures should be retried silently.
 */
export function useConflicts(patientId: string) {
  return useQuery<ConflictItemDto[], Error>({
    queryKey: CONFLICTS_QUERY_KEY(patientId),
    queryFn: () => getConflicts(patientId),
    staleTime: 30_000,
    retry: 2,
    enabled: patientId.length > 0,
  });
}
