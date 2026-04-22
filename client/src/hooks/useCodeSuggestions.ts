// React Query hook for fetching ICD-10/CPT code suggestions (US_023, FR-014).
import { useQuery } from '@tanstack/react-query';

import { type CodeSuggestionDto, getCodeSuggestions } from '@/api/codeSuggestions';

export const CODE_SUGGESTIONS_QUERY_KEY = (patientId: string) =>
  ['codeSuggestions', patientId] as const;

/**
 * Fetches all code suggestions for the given patient.
 *
 * staleTime: 60 s — suggestions change only after staff review; tolerate slight staleness.
 * enabled: patientId must be non-empty before firing.
 */
export function useCodeSuggestions(patientId: string) {
  return useQuery<CodeSuggestionDto[], Error>({
    queryKey: CODE_SUGGESTIONS_QUERY_KEY(patientId),
    queryFn: () => getCodeSuggestions(patientId),
    staleTime: 60_000,
    enabled: patientId.length > 0,
  });
}
