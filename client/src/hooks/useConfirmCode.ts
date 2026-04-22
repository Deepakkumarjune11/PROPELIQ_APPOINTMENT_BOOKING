// React Query mutation for confirming (accepting/rejecting) a code suggestion (US_023, AC-3, AC-4).
// Uses optimistic setQueryData so the card transitions immediately without a refetch.
import { useMutation, useQueryClient } from '@tanstack/react-query';

import {
  type CodeSuggestionDto,
  type ConfirmCodePayload,
  confirmCode,
} from '@/api/codeSuggestions';
import { CODE_SUGGESTIONS_QUERY_KEY } from '@/hooks/useCodeSuggestions';

/**
 * Mutation to accept or reject a code suggestion.
 *
 * onSuccess: updates the cache entry for the given code in-place so the card transitions
 * to its reviewed state immediately, without waiting for a server refetch.
 */
export function useConfirmCode(patientId: string) {
  const queryClient = useQueryClient();

  return useMutation<void, Error, ConfirmCodePayload>({
    mutationFn: (payload) => confirmCode(payload),
    onSuccess: (_, variables) => {
      queryClient.setQueryData<CodeSuggestionDto[]>(
        CODE_SUGGESTIONS_QUERY_KEY(patientId),
        (old = []) =>
          old.map((c) =>
            c.id === variables.codeId
              ? {
                  ...c,
                  staffReviewed: true,
                  reviewOutcome: variables.reviewOutcome,
                  justification: variables.justification,
                }
              : c,
          ),
      );
    },
  });
}
