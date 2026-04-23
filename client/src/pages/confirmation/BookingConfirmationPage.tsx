// SCR-006 — Booking Confirmation screen (US_014).
// Shows success alert, booking summary, PDF download, and Google/Outlook calendar sync buttons.
// UXR-403: step 5 (index 4) active in BookingProgressStepper.
import CalendarMonthIcon from '@mui/icons-material/CalendarMonth';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdf';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import CircularProgress from '@mui/material/CircularProgress';
import Container from '@mui/material/Container';
import Divider from '@mui/material/Divider';
import Snackbar from '@mui/material/Snackbar';
import Typography from '@mui/material/Typography';
import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

import { type CalendarProvider, getCalendarInitUrl } from '@/api/calendar';
import BookingProgressStepper from '@/pages/availability/components/BookingProgressStepper';
import { useBookingStore } from '@/stores/booking-store';
import { useIntakeStore } from '@/stores/intake-store';

function formatDatetime(datetime: string): string {
  return new Intl.DateTimeFormat('en-US', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  }).format(new Date(datetime));
}

export default function BookingConfirmationPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  const { selectedSlot, patientDetails, appointmentId, clearIntake } = useBookingStore();
  const { clearIntake: clearIntakeStore } = useIntakeStore();

  // Snapshot booking data into local state immediately on mount so it survives
  // the clearIntake() call (which sets store values to null on the next render).
  const [snapshot] = useState(() => ({
    slot: selectedSlot,
    details: patientDetails,
    apptId: appointmentId,
  }));

  const [loadingProvider, setLoadingProvider] = useState<CalendarProvider | null>(null);
  const [snackbar, setSnackbar] = useState<{ open: boolean; message: string; severity: 'success' | 'error' }>({
    open: false,
    message: '',
    severity: 'success',
  });

  // Guard: if booking data is missing the patient navigated here directly — redirect to search
  useEffect(() => {
    if (!snapshot.details || !snapshot.slot) {
      navigate('/appointments/search', { replace: true });
      return;
    }

    // Handle OAuth callback query params: ?calendarSynced=google|outlook or ?calendarError=true
    const synced = searchParams.get('calendarSynced');
    const error = searchParams.get('calendarError');

    if (synced) {
      const providerLabel = synced === 'google' ? 'Google' : 'Outlook';
      setSnackbar({ open: true, message: `Added to ${providerLabel} Calendar!`, severity: 'success' });
    } else if (error) {
      setSnackbar({ open: true, message: 'Calendar sync failed. Try again.', severity: 'error' });
    }

    // Reset both stores after snapshotting — safe because we render from snapshot now
    clearIntake();
    clearIntakeStore();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleCalendarSync = async (provider: CalendarProvider) => {
    if (!snapshot.apptId) return;
    setLoadingProvider(provider);
    try {
      const { authUrl } = await getCalendarInitUrl(provider, snapshot.apptId);
      window.location.href = authUrl; // OAuth redirect — leaves the page
    } catch {
      setSnackbar({ open: true, message: 'Could not start calendar sync. Try again.', severity: 'error' });
      setLoadingProvider(null);
    }
  };

  const handleSnackbarClose = () => setSnackbar((s) => ({ ...s, open: false }));

  // Render using snapshot — immune to store being cleared mid-render
  if (!snapshot.details || !snapshot.slot) return null;

  const effectiveAppointmentId = snapshot.apptId ?? snapshot.details.patientId;
  const pdfUrl = `${(import.meta.env.VITE_API_URL as string | undefined) ?? ''}/api/v1/appointments/${effectiveAppointmentId}/pdf`;

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <BookingProgressStepper activeStep={4} />

      {/* Success alert — AC-5 */}
      <Alert
        severity="success"
        icon={<CheckCircleOutlineIcon fontSize="inherit" />}
        sx={{ mb: 3, mt: 2 }}
      >
        <Typography fontWeight={600}>Booking confirmed!</Typography>
        <Typography variant="body2">
          A confirmation email and PDF have been sent to {snapshot.details.email}.
        </Typography>
      </Alert>

      {/* Booking summary card */}
      <Card variant="outlined" sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h6" fontWeight={600} gutterBottom>
            Appointment Summary
          </Typography>
          <Divider sx={{ mb: 2 }} />
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
              <Typography variant="body2" color="text.secondary">Date &amp; Time</Typography>
              <Typography variant="body2" fontWeight={500}>
                {formatDatetime(snapshot.slot.datetime)}
              </Typography>
            </Box>
            <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
              <Typography variant="body2" color="text.secondary">Provider</Typography>
              <Typography variant="body2" fontWeight={500}>{snapshot.slot.provider}</Typography>
            </Box>
            <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
              <Typography variant="body2" color="text.secondary">Patient</Typography>
              <Typography variant="body2" fontWeight={500}>{snapshot.details.name}</Typography>
            </Box>
            {snapshot.slot.location && (
              <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                <Typography variant="body2" color="text.secondary">Location</Typography>
                <Typography variant="body2" fontWeight={500}>{snapshot.slot.location}</Typography>
              </Box>
            )}
          </Box>
        </CardContent>
      </Card>

      {/* Actions — AC-5 */}
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        {/* PDF download — opens in new tab; browser handles the download */}
        <Button
          variant="outlined"
          startIcon={<PictureAsPdfIcon />}
          href={pdfUrl}
          target="_blank"
          rel="noopener noreferrer"
          fullWidth
          aria-label="Download PDF confirmation"
        >
          Download PDF Confirmation
        </Button>

        {/* Google Calendar — AC-2, AC-3 */}
        <Button
          variant="contained"
          startIcon={
            loadingProvider === 'google' ? (
              <CircularProgress size={16} color="inherit" />
            ) : (
              <CalendarMonthIcon />
            )
          }
          onClick={() => handleCalendarSync('google')}
          disabled={loadingProvider !== null}
          fullWidth
          aria-label="Add to Google Calendar"
        >
          Add to Google Calendar
        </Button>

        {/* Outlook Calendar — AC-2, AC-3 */}
        <Button
          variant="outlined"
          startIcon={
            loadingProvider === 'outlook' ? (
              <CircularProgress size={16} color="inherit" />
            ) : (
              <CalendarMonthIcon />
            )
          }
          onClick={() => handleCalendarSync('outlook')}
          disabled={loadingProvider !== null}
          fullWidth
          aria-label="Add to Outlook Calendar"
        >
          Add to Outlook Calendar
        </Button>
      </Box>

      {/* Transient feedback snackbar (calendar synced / error) */}
      <Snackbar
        open={snackbar.open}
        autoHideDuration={5000}
        onClose={handleSnackbarClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          onClose={handleSnackbarClose}
          severity={snackbar.severity}
          variant="filled"
          sx={{ width: '100%' }}
        >
          {snackbar.message}
        </Alert>
      </Snackbar>
    </Container>
  );
}
