// React Query mutation hook — books a walk-in appointment or places patient on wait queue.
// Dispatches appropriate toast on success (booked vs wait-queue) then navigates to queue (US_016).
import { useMutation } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';

import {
  type WalkInBookingRequest,
  type WalkInBookingResult,
  StaffApiError,
  bookWalkIn,
} from '@/api/staff';

interface UseBookWalkInOptions {
  /** Called on any outcome so the page can show the appropriate Snackbar message. */
  onSuccess: (message: string, severity: 'success' | 'info') => void;
  onError: () => void;
}

export function useBookWalkIn({ onSuccess, onError }: UseBookWalkInOptions) {
  const navigate = useNavigate();

  return useMutation<WalkInBookingResult, StaffApiError, WalkInBookingRequest>({
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

    onError: () => {
      onError();
    },
  });
}
