// SCR-003: Patient Details Form — step 3 of the booking flow (FL-001).
//
// Guards: redirects to /appointments/search if no slot is selected in booking-store.
// UXR-403: BookingProgressStepper — step 2 (index 2, "Details") active.
// UXR-502: All fields validate inline on blur; submit disabled until valid.
// UXR-101: Keyboard accessible throughout.
// UXR-102: 44px min touch targets on action buttons.
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import IconButton from '@mui/material/IconButton';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';

import type { PatientRegistrationRequest } from '@/api/registration';
import BookingProgressStepper from '@/pages/availability/components/BookingProgressStepper';
import { useBookingStore } from '@/stores/booking-store';
import { useRegisterPatient } from '@/hooks/useRegisterPatient';
import InsuranceStatusAlert from './components/InsuranceStatusAlert';
import PatientDetailsForm from './components/PatientDetailsForm';
import type { PatientRegistrationResponse } from '@/api/registration';

export default function PatientDetailsFormPage() {
  const navigate = useNavigate();
  const { selectedSlot } = useBookingStore();

  const [emailConflictError, setEmailConflictError] = useState<string | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [insuranceStatus, setInsuranceStatus] =
    useState<PatientRegistrationResponse['insuranceStatus'] | null>(null);

  // Guard: no slot selected → send back to step 1
  if (!selectedSlot) {
    return <Navigate to="/appointments/search" replace />;
  }

  const { mutate, isLoading } = useRegisterPatient({
    onEmailConflict: (msg) => {
      setEmailConflictError(msg);
      setApiError(null);
    },
    onError: (msg) => {
      setApiError(msg);
      setEmailConflictError(null);
    },
  });

  const handleSubmit = (payload: PatientRegistrationRequest) => {
    setEmailConflictError(null);
    setApiError(null);
    mutate(
      { slotId: selectedSlot.id, payload },
      {
        onSuccess: (data) => {
          setInsuranceStatus(data.insuranceStatus);
        },
      },
    );
  };

  const handleBack = () => {
    navigate('/appointments/slot-selection');
  };

  return (
    <Box sx={{ minHeight: '100vh', backgroundColor: 'grey.50', pb: 10 }}>
      {/* Header */}
      <Box
        component="header"
        sx={{
          backgroundColor: 'background.paper',
          boxShadow: 1,
          px: 3,
          py: 2,
          display: 'flex',
          alignItems: 'center',
          gap: 2,
        }}
      >
        <IconButton
          onClick={handleBack}
          aria-label="Go back to slot selection"
          sx={{ minWidth: 44, minHeight: 44 }}
        >
          <ArrowBackIcon />
        </IconButton>
        <Typography variant="h6" component="span" fontWeight={500}>
          Patient details
        </Typography>
      </Box>

      <Container maxWidth="sm" sx={{ pt: 4, px: 3 }}>
        {/* UXR-403: Progress stepper — step 2 (Details) active */}
        <BookingProgressStepper activeStep={2} />

        <Stack spacing={1} sx={{ mb: 4 }}>
          <Typography variant="h5" component="h1">
            Your information
          </Typography>
          <Typography variant="body2" color="text.secondary">
            We'll create your account to book this appointment
          </Typography>
        </Stack>

        {/* Generic API error */}
        {apiError && (
          <Alert
            severity="error"
            sx={{ mb: 3 }}
            action={
              <Typography
                component="button"
                variant="body2"
                onClick={() => setApiError(null)}
                sx={{
                  background: 'none',
                  border: 'none',
                  cursor: 'pointer',
                  color: 'error.main',
                  fontWeight: 500,
                }}
              >
                Retry
              </Typography>
            }
          >
            {apiError}
          </Alert>
        )}

        {/* Insurance validation result — non-blocking (AC-2, AC-3) */}
        {insuranceStatus && <InsuranceStatusAlert insuranceStatus={insuranceStatus} />}

        <PatientDetailsForm
          isLoading={isLoading}
          emailConflictError={emailConflictError}
          onSubmit={handleSubmit}
          onBack={handleBack}
        />
      </Container>
    </Box>
  );
}
