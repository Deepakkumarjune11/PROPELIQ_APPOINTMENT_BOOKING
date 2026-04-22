// React Query mutation hook — registers a preferred slot on the swap watchlist.
// POST /api/v1/appointments/{appointmentId}/preferred-slot (US_015, FR-004).
//
// Callers supply onSuccess / onError callbacks (see useRegisterPatient pattern) so
// toast display stays at the page layer while query invalidation lives here.
import { useMutation, useQueryClient } from '@tanstack/react-query';

import {
  AppointmentsApiError,
  registerPreferredSlot,
} from '@/api/appointments';

interface RegisterPreferredSlotPayload {
  appointmentId: string;
  preferredSlotDatetime: string;
}

interface UseRegisterPreferredSlotOptions {
  /** Called after successful registration; use to show success toast + navigate back. */
  onSuccess: () => void;
  /**
   * Called on failure.
   * @param is422 - true when the slot is no longer eligible (AC-5); show specific warning toast.
   */
  onError: (is422: boolean) => void;
}

export function useRegisterPreferredSlot({
  onSuccess,
  onError,
}: UseRegisterPreferredSlotOptions) {
  const queryClient = useQueryClient();

  return useMutation<void, AppointmentsApiError, RegisterPreferredSlotPayload>({
    mutationFn: ({ appointmentId, preferredSlotDatetime }) =>
      registerPreferredSlot(appointmentId, preferredSlotDatetime),

    onSuccess: () => {
      // Refresh appointment list so SCR-008 shows the updated watchlist badge (AC-4).
      void queryClient.invalidateQueries({ queryKey: ['appointments'] });
      onSuccess();
    },

    onError: (error) => {
      onError(error instanceof AppointmentsApiError && error.status === 422);
    },
  });
}
