// Optimistic mutation hook for appointment status changes (US_017, AC-3, UXR-404).
// Pattern: cancel in-flight queue fetches → snapshot current cache → apply optimistic update.
// On API error: revert snapshot and show error toast.
// On success: show success toast; SignalR QueueUpdated event handles cache invalidation.
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { type QueueEntry, StaffApiError, updateAppointmentStatus } from '@/api/staff';
import { QUEUE_QUERY_KEY } from './useSameDayQueue';

interface StatusMutationVars {
  appointmentId: string;
  status: 'arrived' | 'in-room' | 'left';
}

interface UseUpdateAppointmentStatusOptions {
  onSuccess?: (message: string) => void;
  onError?: (message: string) => void;
}

export function useUpdateAppointmentStatus({
  onSuccess,
  onError,
}: UseUpdateAppointmentStatusOptions = {}) {
  const queryClient = useQueryClient();

  return useMutation<void, StaffApiError, StatusMutationVars, { previous: QueueEntry[] | undefined }>({
    mutationFn: ({ appointmentId, status }) =>
      updateAppointmentStatus(appointmentId, status),

    // ── Optimistic update (UXR-404) ────────────────────────────────────────
    onMutate: async ({ appointmentId, status }) => {
      // Cancel any in-flight refetches so they don't overwrite the optimistic data.
      await queryClient.cancelQueries({ queryKey: QUEUE_QUERY_KEY });

      // Snapshot previous value for rollback.
      const previous = queryClient.getQueryData<QueueEntry[]>(QUEUE_QUERY_KEY);

      // Apply optimistic update immediately.
      queryClient.setQueryData<QueueEntry[]>(QUEUE_QUERY_KEY, (old) =>
        old?.map((e) =>
          e.appointmentId === appointmentId ? { ...e, status } : e,
        ) ?? [],
      );

      return { previous };
    },

    // ── Rollback on error (UXR-404) ────────────────────────────────────────
    onError: (_err, _vars, context) => {
      if (context?.previous !== undefined) {
        queryClient.setQueryData(QUEUE_QUERY_KEY, context.previous);
      }
      onError?.('Status update failed. Please try again.');
    },

    onSuccess: () => {
      onSuccess?.('Patient status updated.');
    },
  });
}
