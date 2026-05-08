// React Query mutation hook — books a walk-in appointment or places patient on wait queue.
// Dispatches appropriate toast on success (booked vs wait-queue) then navigates to queue (US_016).
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import type { AxiosError } from 'axios';

import {
  type WalkInBookingRequest,
  type WalkInBookingResult,
  bookWalkIn,
} from '@/api/staff';
import { QUEUE_QUERY_KEY } from './useSameDayQueue';

interface UseBookWalkInOptions {
  /** Called on any outcome so the page can show the appropriate Snackbar message. */
  onSuccess: (message: string, severity: 'success' | 'info') => void;
  onError: (detail: string) => void;
}

export function useBookWalkIn({ onSuccess, onError }: UseBookWalkInOptions) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  return useMutation<WalkInBookingResult, AxiosError<{ detail?: string }>, WalkInBookingRequest>({
    mutationFn: bookWalkIn,

    onSuccess: (result) => {
      // Eagerly invalidate queue and dashboard summary so counts are current before
      // the SignalR broadcast arrives (or in case the user is not yet on the queue page).
      void queryClient.invalidateQueries({ queryKey: QUEUE_QUERY_KEY });
      void queryClient.invalidateQueries({ queryKey: ['staff', 'dashboard'] });

      if (result.waitQueue) {
        onSuccess(
          `No slots available. Patient added to wait queue at position ${result.queuePosition}.`,
          'info',
        );
      } else {
        onSuccess(`Walk-in booked! Queue position: ${result.queuePosition}.`, 'success');
      }
      void navigate('/staff/queue');
    },

    onError: (error) => {
      const detail = error.response?.data?.detail ?? 'Walk-in booking failed. Please try again.';
      onError(detail);
    },
  });
}
