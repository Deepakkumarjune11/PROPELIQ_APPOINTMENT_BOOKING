// SCR-005: Conversational AI Intake — step 4 of the booking flow (FL-001).
//
// Architecture: FE is a stateless message renderer. All LLM/AI logic lives in the backend.
// The `useIntakeChat` hook manages message history, typing state, completion, and fallback.
//
// Guards:  redirects to /appointments/search if no patientDetails in booking-store.
// AC-1:    on mount, empty-message API call fires → AI greeting appears.
// AC-2:    typing indicator while mutation pending (p95 ≤ 3s — AIR-Q02).
// AC-3:    "Switch to manual" preserves answers via intake-store (does NOT clear answers).
// AC-4:    Submit button appears when isComplete = true; fires useSubmitIntake mutation.
// AC-5:    fallbackToManual → IntakeFallbackBanner + auto-redirect to manual form.
// UXR-403: BookingProgressStepper step 3 (index 3) active.
import SwapHorizIcon from '@mui/icons-material/SwapHoriz';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Snackbar from '@mui/material/Snackbar';
import Typography from '@mui/material/Typography';
import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';

import BookingProgressStepper from '@/pages/availability/components/BookingProgressStepper';
import { useSubmitIntake } from '@/hooks/useSubmitIntake';
import { useIntakeChat } from '@/hooks/useIntakeChat';
import { useBookingStore } from '@/stores/booking-store';
import { useIntakeStore } from '@/stores/intake-store';
import ChatInputBar from './components/ChatInputBar';
import ChatMessageList from './components/ChatMessageList';
import IntakeFallbackBanner from './components/IntakeFallbackBanner';

export default function ConversationalIntakePage() {
  const navigate = useNavigate();
  const { patientDetails } = useBookingStore();
  const { answers, setMode } = useIntakeStore();
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Guard: patient must have completed registration before intake
  if (!patientDetails) {
    return <Navigate to="/appointments/search" replace />;
  }

  const {
    messages,
    isTyping,
    isComplete,
    isFallback,
    showInactivityWarning,
    send,
  } = useIntakeChat(patientDetails.patientId);

  const { mutate: submitIntake, isLoading: isSubmitting } = useSubmitIntake({
    onError: (msg) => setSubmitError(msg),
  });

  const handleSwitchToManual = () => {
    // AC-3: setMode only — answers are NOT cleared
    setMode('manual');
    navigate('/appointments/intake/manual');
  };

  const handleSubmit = () => {
    setSubmitError(null);
    submitIntake({
      patientId: patientDetails.patientId,
      payload: { answers, mode: 'conversational' },
    });
  };

  // Single initial letter for the patient avatar
  const patientInitial = (patientDetails.name?.[0] ?? 'P').toUpperCase();

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100vh', backgroundColor: 'grey.50' }}>
      {/* ── Header ────────────────────────────────────────────────────────── */}
      <Box
        component="header"
        sx={{
          backgroundColor: 'background.paper',
          boxShadow: 1,
          px: { xs: 2, sm: 3 },
          py: 2,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 2,
          flexShrink: 0,
        }}
      >
        <Typography variant="h6" component="span" fontWeight={500}>
          AI-assisted intake
        </Typography>

        {/* UXR-102: 44px min touch target */}
        <Button
          variant="text"
          size="small"
          startIcon={<SwapHorizIcon />}
          onClick={handleSwitchToManual}
          sx={{ minHeight: 44, color: 'primary.main', whiteSpace: 'nowrap' }}
          aria-label="Switch to manual intake form"
        >
          Switch to manual
        </Button>
      </Box>

      {/* ── Booking stepper (step 3 = "Intake", UXR-403) ─────────────────── */}
      <Box sx={{ px: { xs: 2, sm: 3 }, pt: 2, flexShrink: 0 }}>
        <BookingProgressStepper activeStep={3} />
      </Box>

      {/* ── Fallback banner (AC-5) ────────────────────────────────────────── */}
      {isFallback && <IntakeFallbackBanner />}

      {/* ── Submit error ─────────────────────────────────────────────────── */}
      {submitError && (
        <Box sx={{ px: { xs: 2, sm: 3 }, py: 1 }}>
          <Alert severity="error" onClose={() => setSubmitError(null)}>
            {submitError}
          </Alert>
        </Box>
      )}

      {/* ── Completion banner + Submit button (AC-4) ─────────────────────── */}
      {isComplete && (
        <Box sx={{ px: { xs: 2, sm: 3 }, py: 1, flexShrink: 0 }}>
          <Alert
            severity="success"
            action={
              <Button
                color="inherit"
                size="small"
                onClick={handleSubmit}
                disabled={isSubmitting}
                sx={{ minHeight: 44, whiteSpace: 'nowrap' }}
                startIcon={isSubmitting ? <CircularProgress size={14} color="inherit" /> : undefined}
              >
                {isSubmitting ? 'Submitting…' : 'Submit intake'}
              </Button>
            }
          >
            All required information collected!
          </Alert>
        </Box>
      )}

      {/* ── Message list — grows to fill remaining height ─────────────────── */}
      <ChatMessageList
        messages={messages}
        isTyping={isTyping}
        patientInitial={patientInitial}
      />

      {/* ── Input bar ────────────────────────────────────────────────────── */}
      <Box sx={{ flexShrink: 0 }}>
        <ChatInputBar
          disabled={isTyping || isComplete || isFallback}
          onSend={send}
        />
      </Box>

      {/* ── 4-minute inactivity Snackbar (edge case) ─────────────────────── */}
      <Snackbar
        open={showInactivityWarning}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
        message="Still there? Your progress is saved."
        sx={{ mb: 10 }}
      />
    </Box>
  );
}

