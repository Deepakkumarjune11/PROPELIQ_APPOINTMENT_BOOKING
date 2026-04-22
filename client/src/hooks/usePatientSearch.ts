// React Query hook — debounced patient search for walk-in autocomplete (US_016, AC-2).
// Enabled only when the query is ≥ 2 characters; debounce is applied in the consumer component.
import { useQuery } from '@tanstack/react-query';

import { type PatientSearchResult, searchPatients } from '@/api/staff';

export function usePatientSearch(query: string) {
  const { data, isLoading, isError } = useQuery<PatientSearchResult[], Error>({
    queryKey: ['patients', 'search', query],
    queryFn: () => searchPatients(query),
    // Guard: skip network call until the user has typed a meaningful term (AC-2).
    enabled: query.trim().length >= 2,
    staleTime: 10_000,
  });

  return {
    results: data ?? [],
    isLoading,
    isError,
  };
}
