// React Query hook for fetching source citation text for a specific fact (US_021, AC-3).
// Lazy by design: `enabled: false` so the query only runs when the user clicks the citation button.
// Caller invokes `refetch()` to trigger the fetch after setting the factId.
import { useQuery } from '@tanstack/react-query';

import { type SourceCitationDto, getFactSource } from '@/api/patientView360';

export const FACT_SOURCE_QUERY_KEY = (factId: string | null) =>
  ['factSource', factId] as const;

/**
 * Lazily fetches the source citation document text for a fact.
 *
 * enabled: false — must call `refetch()` manually on citation button click.
 * staleTime: 300 s — source documents do not change once uploaded.
 */
export function useFactSource(factId: string | null) {
  return useQuery<SourceCitationDto, Error>({
    queryKey: FACT_SOURCE_QUERY_KEY(factId),
    queryFn: () => getFactSource(factId!),
    enabled: false,       // triggered manually via refetch()
    staleTime: 300_000,
  });
}
