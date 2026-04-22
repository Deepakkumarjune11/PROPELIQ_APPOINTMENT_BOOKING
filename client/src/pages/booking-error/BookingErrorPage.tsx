// SCR-007 — Booking Error screen (US_014).
// Shown when the booking submission fails with a non-409 error.
// UXR-403: step 3 active (error occurred at patient details / registration step).
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Link from '@mui/material/Link';
import Typography from '@mui/material/Typography';
import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';

import {
  RegistrationApiError,
  registerPatient,
  type PatientRegistrationRequest,
} from '@/api/registration';
import BookingProgressStepper from '@/pages/availability/components/BookingProgressStepper';
import { useBookingStore } from '@/stores/booking-store';

export default function BookingErrorPage() {
  const navigate = useNavigate();
  const { selectedSlot, patientDetails, setAppointmentId } = useBookingStore();

  // Guard: if no booking data, the patient navigated here directly — redirect to search
  useEffect(() => {
    if (!selectedSlot || !patientDetails) {
      navigate('/appointments/search', { replace: true });
    }
  }, [selectedSlot, patientDetails, navigate]);

  const handleRetry = async () => {
    if (!selectedSlot || !patientDetails) return;

    const payload: PatientRegistrationRequest = {
      email: patientDetails.email,
      name: patientDetails.name,
      dob: patientDetails.dob,
      phone: patientDetails.phone,
      insuranceProvider: patientDetails.insuranceProvider,
      insuranceMemberId: patientDetails.insuranceMemberId,
    };

    try {
      const result = await registerPatient(selectedSlot.id, payload);
      if (result.appointmentId) setAppointmentId(result.appointmentId);
      navigate('/appointments/intake/manual');
    } catch (err) {
      if (err instanceof RegistrationApiError && err.status === 409) {
        navigate('/appointments/slot-selection');
      }
      // On repeated failure, stay on this page — the error state is already shown
    }
  };

  // Wait for guard effect before rendering
  if (!selectedSlot || !patientDetails) return null;

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <BookingProgressStepper activeStep={2} />

      <Alert
        severity="error"
        icon={<ErrorOutlineIcon fontSize="inherit" />}
        sx={{ mb: 3, mt: 2 }}
      >
        <Typography fontWeight={600}>Booking could not be completed.</Typography>
        <Typography variant="body2">
          Please try again. If the problem persists, select a different slot.
        </Typography>
      </Alert>

      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        {/* Retry — re-submits the same patient details to the same slot */}
        <Button
          variant="contained"
          color="primary"
          onClick={() => void handleRetry()}
          fullWidth
          aria-label="Retry booking submission"
        >
          Retry
        </Button>

        {/* Select another slot — back to SCR-002 */}
        <Box sx={{ textAlign: 'center' }}>
          <Link
            component="button"
            variant="body2"
            onClick={() => navigate('/appointments/slot-selection')}
            aria-label="Select another appointment slot"
          >
            Select another slot
          </Link>
        </Box>
      </Box>
    </Container>
  );
}
