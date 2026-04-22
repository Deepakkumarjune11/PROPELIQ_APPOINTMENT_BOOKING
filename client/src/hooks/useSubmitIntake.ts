// React Query mutation hook for POST /api/v1/patients/{patientId}/intake.
// Navigates to /appointments/confirmation on success; surfaces error message on failure.
import { useMutation } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';

import { submitIntake, type IntakeSubmissionRequest } from '@/api/intake';
import { useIntakeStore } from '@/stores/intake-store';

interface UseSubmitIntakeOptions {
  /** Called with a human-readable error message on submission failure. */
  onError: (message: string) => void;
}

export function useSubmitIntake({ onError }: UseSubmitIntakeOptions) {
  const navigate = useNavigate();
  const { clearIntake } = useIntakeStore();

  return useMutation<
    void,
    Error,
    { patientId: string; payload: IntakeSubmissionRequest }
  >({
    mutationFn: ({ patientId, payload }) => submitIntake(patientId, payload).then(() => undefined),

    onSuccess: () => {
      // Clear intake answers from sessionStorage after successful submission
      clearIntake();
      navigate('/appointments/confirmation');
    },

    onError: () => {
      onError('Failed to submit intake. Please try again.');
    },
  });
}
