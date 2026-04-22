// SCR-004: Manual Intake Form — step 4 of the booking flow (FL-001).
//
// Guards: redirects to /appointments/search if no patientDetails in booking-store.
// UXR-403: BookingProgressStepper — step 3 (index 3, "Intake") active.
// UXR-502: Submit disabled until all required questions answered.
// UXR-003: Inline tooltips on complex fields via IntakeQuestionField.
// UXR-101/102: Keyboard accessible; 44px min touch targets on action buttons.
// AC-2/AC-3: intake-store holds canonical answers shared with conversational mode.
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Container from '@mui/material/Container';
import IconButton from '@mui/material/IconButton';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';

import { INTAKE_QUESTIONS, REQUIRED_QUESTION_IDS } from '@/config/intakeQuestions';
import BookingProgressStepper from '@/pages/availability/components/BookingProgressStepper';
import { useBookingStore } from '@/stores/booking-store';
import { useIntakeStore } from '@/stores/intake-store';
import { useSubmitIntake } from '@/hooks/useSubmitIntake';
import IntakeProgressBar from './components/IntakeProgressBar';
import IntakeQuestionField from './components/IntakeQuestionField';
import ModeSwitchButton from './components/ModeSwitchButton';

export default function ManualIntakeFormPage() {
  const navigate = useNavigate();
  const { patientDetails } = useBookingStore();
  const { answers, setMode } = useIntakeStore();
  const [apiError, setApiError] = useState<string | null>(null);

  // Guard: registration must have completed before intake can be filled
  if (!patientDetails) {
    return <Navigate to="/appointments/search" replace />;
  }

  const { mutate, isLoading } = useSubmitIntake({
    onError: (msg) => setApiError(msg),
  });

  // ── Progress calculation ────────────────────────────────────────────────────
  const answeredRequiredCount = REQUIRED_QUESTION_IDS.filter(
    (id) => (answers[id] ?? '').trim() !== '',
  ).length;
  const allRequiredAnswered = answeredRequiredCount === REQUIRED_QUESTION_IDS.length;

  // ── Submit ──────────────────────────────────────────────────────────────────
  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setApiError(null);
    setMode('manual');
    mutate({
      patientId: patientDetails.patientId,
      payload: { answers, mode: 'manual' },
    });
  };

  const handleBack = () => navigate('/appointments/patient-details');

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
          aria-label="Go back to patient details"
          sx={{ minWidth: 44, minHeight: 44 }}
        >
          <ArrowBackIcon />
        </IconButton>
        <Typography variant="h6" component="span" fontWeight={500}>
          Manual intake
        </Typography>
      </Box>

      <Container maxWidth="sm" sx={{ pt: 4, px: 3 }}>
        {/* UXR-403: step 3 (index 3 = "Intake") */}
        <BookingProgressStepper activeStep={3} />

        {/* Title row with mode-switch button in the top-right */}
        <Box sx={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', mb: 3 }}>
          <Stack spacing={0.5}>
            <Typography variant="h5" component="h1">
              Medical intake questionnaire
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Please answer the questions below before your appointment.
            </Typography>
          </Stack>
          <ModeSwitchButton />
        </Box>

        {/* Progress bar — answered required / total required */}
        <IntakeProgressBar
          answeredCount={answeredRequiredCount}
          totalRequired={REQUIRED_QUESTION_IDS.length}
        />

        {/* Generic API error */}
        {apiError && (
          <Alert severity="error" sx={{ mb: 3 }} onClose={() => setApiError(null)}>
            {apiError}
          </Alert>
        )}

        {/* Intake form */}
        <Box
          component="form"
          onSubmit={handleSubmit}
          noValidate
          sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}
        >
          {INTAKE_QUESTIONS.map((question) => (
            <IntakeQuestionField key={question.id} question={question} />
          ))}

          {/* Actions — min 44px touch targets (UXR-102) */}
          <Box sx={{ display: 'flex', gap: 2, mt: 1 }}>
            <Button
              variant="outlined"
              onClick={handleBack}
              disabled={isLoading}
              sx={{ flex: 1, minHeight: 44 }}
            >
              Back
            </Button>
            <Button
              type="submit"
              variant="contained"
              disabled={!allRequiredAnswered || isLoading}
              sx={{ flex: 1, minHeight: 44 }}
              startIcon={isLoading ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              {isLoading ? 'Submitting…' : 'Submit intake'}
            </Button>
          </Box>
        </Box>
      </Container>
    </Box>
  );
}
