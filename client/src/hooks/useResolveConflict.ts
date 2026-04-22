// React Query mutation for resolving a single conflict (US_022, FR-013, AC-3).
// On success: removes the resolved conflict from the cached list (no full refetch)
// and invalidates the patientView360 query so conflictCount refreshes on SCR-017.
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { type ConflictItemDto, type ResolveConflictPayload, resolveConflict } from '@/api/conflicts';
import { PATIENT_VIEW_360_QUERY_KEY } from '@/hooks/usePatientView360';
import { CONFLICTS_QUERY_KEY } from '@/hooks/useConflicts';

/**
 * Mutation hook for resolving a conflict.
 *
 * @param patientId — used to update the two query caches (conflicts + patientView360).
 */
export function useResolveConflict(patientId: string) {
  const queryClient = useQueryClient();

  return useMutation<void, Error, ResolveConflictPayload>({
    mutationFn: (payload) => resolveConflict(payload),

    onSuccess: (_data, variables) => {
      // Optimistically remove the resolved conflict from the list — no page navigation.
      queryClient.setQueryData<ConflictItemDto[]>(
        CONFLICTS_QUERY_KEY(patientId),
        (old = []) => old.filter((c) => c.conflictId !== variables.conflictId),
      );

      // Refresh the 360-view so conflictCount on SCR-017 stays accurate.
      void queryClient.invalidateQueries({
        queryKey: PATIENT_VIEW_360_QUERY_KEY(patientId),
      });
    },
  });
}
