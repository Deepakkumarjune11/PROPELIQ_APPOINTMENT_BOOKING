// React Query mutation hook for POST /api/v1/appointments/{slotId}/register.
// Handles 409 email-conflict mapping, 409 slot-conflict rollback (UXR-404), and generic API errors.
// Navigation is intentionally NOT performed here — the calling page controls when to proceed so it
// can show the insurance status check result before advancing to intake (AC-2, AC-3).
import { useMutation } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';

import {
  RegistrationApiError,
  registerPatient,
  type PatientRegistrationRequest,
  type PatientRegistrationResponse,
} from '@/api/registration';
import { useBookingStore } from '@/stores/booking-store';

interface UseRegisterPatientOptions {
  /** Called with the inline email-conflict message when API returns 409 with emailConflictMessage. */
  onEmailConflict: (message: string) => void;
  /** Called with a human-readable error message on non-409 API errors. */
  onError: (message: string) => void;
}

export function useRegisterPatient({ onEmailConflict, onError }: UseRegisterPatientOptions) {
  const navigate = useNavigate();
  const { setPatientDetails, setAppointmentId, clearSelectedSlot, setConflictError } = useBookingStore();

  return useMutation<
    PatientRegistrationResponse,
    RegistrationApiError,
    { slotId: string; payload: PatientRegistrationRequest }
  >({
    mutationFn: ({ slotId, payload }) => registerPatient(slotId, payload),

    onSuccess: (data, variables) => {
      // Persist patient details + server-assigned patientId into booking store for downstream steps
      setPatientDetails({ ...variables.payload, patientId: data.patientId });
      // Store the appointment ID for PDF download and calendar sync on the confirmation screen
      if (data.appointmentId) setAppointmentId(data.appointmentId);
      // Navigation is deliberately omitted here — the page shows InsuranceStatusAlert
      // and lets the patient confirm before proceeding to intake.
    },

    onError: (error) => {
      if (error instanceof RegistrationApiError && error.status === 409) {
        const body = error.body as { emailConflictMessage?: string } | null;
        if (body?.emailConflictMessage) {
          // Email already registered — surface inline validation message on the form
          onEmailConflict(body.emailConflictMessage);
        } else {
          // Slot claimed by a concurrent booking — revert to slot selection (UXR-404)
          clearSelectedSlot();
          setConflictError(true);
          navigate('/appointments/slot-selection');
        }
      } else {
        // Non-409 failure — navigate to booking error screen (SCR-007)
        onError('Something went wrong. Please try again.');
        navigate('/appointments/error');
      }
    },
  });
}
