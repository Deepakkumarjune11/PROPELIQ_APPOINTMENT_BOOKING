// React Query mutation hook — books a walk-in appointment or places patient on wait queue.
// Dispatches appropriate toast on success (booked vs wait-queue) then navigates to queue (US_016).
import { useMutation } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import type { AxiosError } from 'axios';

import {
  type WalkInBookingRequest,
  type WalkInBookingResult,
  bookWalkIn,
} from '@/api/staff';

interface UseBookWalkInOptions {
  /** Called on any outcome so the page can show the appropriate Snackbar message. */
  onSuccess: (message: string, severity: 'success' | 'info') => void;
  onError: (detail: string) => void;
}

export function useBookWalkIn({ onSuccess, onError }: UseBookWalkInOptions) {
  const navigate = useNavigate();

  return useMutation<WalkInBookingResult, AxiosError<{ detail?: string }>, WalkInBookingRequest>({
    mutationFn: bookWalkIn,

    onSuccess: (result) => {
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
