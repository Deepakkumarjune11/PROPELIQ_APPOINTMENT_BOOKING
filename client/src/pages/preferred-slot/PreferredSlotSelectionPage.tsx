// SCR-009 — Preferred Slot Selection (US_015).
// Allows a patient to pick an unavailable (booked) slot to register on the swap watchlist.
//
// Navigation: arrived from SCR-008 via /appointments/:appointmentId/preferred-slot
//
// Slot eligibility rules (FR-004 / task spec):
//   • available: true  → grayed / disabled — patient can book directly; no watchlist needed
//   • available: false → selectable       — booked by someone else; eligible for watchlist
//
// States: Default (slot calendar) | Loading (skeleton) | Empty (no eligible slots) | Error (Alert)
//
// UXR-003: info alert explains watchlist process
// UXR-101: WCAG 2.2 AA  |  UXR-102: ARIA labels  |  UXR-201: responsive
// UXR-401: loading < 200ms  |  UXR-402: success/error toast  |  UXR-501: actionable errors
import InfoIcon from '@mui/icons-material/Info';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Container from '@mui/material/Container';
import Grid from '@mui/material/Grid';
import Skeleton from '@mui/material/Skeleton';
import Snackbar from '@mui/material/Snackbar';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { DateCalendar } from '@mui/x-date-pickers/DateCalendar';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import dayjs, { type Dayjs } from 'dayjs';
import { useCallback, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';

import {
  type SlotAvailabilityEntry,
  getSlotAvailability,
} from '@/api/appointments';
import { useAppointments } from '@/hooks/useAppointments';
import { useRegisterPreferredSlot } from '@/hooks/useRegisterPreferredSlot';

// ── Helpers ──────────────────────────────────────────────────────────────────

function formatTime(iso: string): string {
  return new Intl.DateTimeFormat('en-US', {
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  }).format(new Date(iso));
}

function isSameDay(a: string, b: Dayjs): boolean {
  const d = new Date(a);
  return d.getFullYear() === b.year() && d.getMonth() === b.month() && d.getDate() === b.date();
}

// ── Component ────────────────────────────────────────────────────────────────

export default function PreferredSlotSelectionPage() {
  const { appointmentId } = useParams<{ appointmentId: string }>();
  const navigate = useNavigate();

  // ── State ──────────────────────────────────────────────────────────────────
  const [selectedDate, setSelectedDate] = useState<Dayjs>(dayjs());
  const [selectedSlotDatetime, setSelectedSlotDatetime] = useState<string | null>(null);
  const [snackbar, setSnackbar] = useState<{
    open: boolean;
    message: string;
    severity: 'success' | 'warning' | 'error';
  }>({ open: false, message: '', severity: 'success' });

  // ── Appointment context (providerId) ──────────────────────────────────────
  const { appointments, isLoading: apptLoading, isError: apptError } = useAppointments();
  const appointment = appointments.find((a) => a.id === appointmentId);

  // ── Slot availability fetch ────────────────────────────────────────────────
  const {
    data: allSlots = [],
    isLoading: slotsLoading,
    isError: slotsError,
    refetch: refetchSlots,
  } = useQuery<SlotAvailabilityEntry[], Error>({
    // BUG-009 fix: key by appointmentId (stable, from URL) and calendar month.
    // providerId was used before but the backend always returns it as null, so
    // `enabled: Boolean(appointment?.providerId)` permanently blocked the query.
    queryKey: ['slotAvailability', appointmentId, selectedDate.year(), selectedDate.month() + 1],
    queryFn: () =>
      getSlotAvailability(
        selectedDate.year(),
        selectedDate.month() + 1, // dayjs months are 0-indexed; API uses 1-indexed
        appointment?.providerId ?? undefined,
      ),
    // Enable as soon as we have the appointmentId from the URL — no providerId required.
    enabled: !!appointmentId,
    staleTime: 30_000,
  });

  // ── Mutation ──────────────────────────────────────────────────────────────
  const { mutate, isLoading: isSubmitting } = useRegisterPreferredSlot({
    onSuccess: () => {
      setSnackbar({
        open: true,
        message: "Watchlist registered! We'll notify you if the slot opens.",
        severity: 'success',
      });
      // Navigate back after a brief toast display (allows patient to read it)
      setTimeout(() => navigate('/appointments'), 1500);
    },
    onError: (is422) => {
      setSnackbar({
        open: true,
        message: is422
          ? 'This slot is no longer available for watchlist. Please select another.'
          : 'Could not register watchlist. Please try again.',
        severity: is422 ? 'warning' : 'error',
      });
    },
  });


  // ── Derived: future-only slots (BUG-012: exclude past/elapsed slots) ───────────────────────
  const nowMs = Date.now();
  const futureSlots = useMemo(
    () => allSlots.filter((s) => new Date(s.datetime).getTime() > nowMs),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [allSlots], // nowMs intentionally not in deps — it's a render-time snapshot
  );

  // ── Derived: ALL days that have at least one FUTURE slot ─────────────────
  // BUG-011 fix: shouldDisableDate must only hide days with zero slots.
  // BUG-012 fix: count only future slots so past days appear disabled.
  const daysWithAnySlots = useMemo(() => {
    const days = new Set<string>();
    futureSlots.forEach((s) => {
      const d = new Date(s.datetime);
      days.add(`${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`);
    });
    return days;
  }, [futureSlots]);

  // ── shouldDisableDate: disable days with NO slots at all ─────────────────
  const shouldDisableDate = useCallback(
    (date: Dayjs) => {
      const key = `${date.year()}-${date.month()}-${date.date()}`;
      return !daysWithAnySlots.has(key);
    },
    [daysWithAnySlots],
  );

  // ── Slots for the selected date (future-only, BUG-012) ──────────────────
  const slotsForDay = useMemo(
    () => futureSlots.filter((s) => isSameDay(s.datetime, selectedDate)),
    [futureSlots, selectedDate],
  );

  const handleConfirm = () => {
    if (!appointmentId || !selectedSlotDatetime) return;
    mutate({ appointmentId, preferredSlotDatetime: selectedSlotDatetime });
  };

  // ── Loading state (appointment + slots) ──────────────────────────────────
  if (apptLoading || slotsLoading) {
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Typography variant="h4" component="h1" gutterBottom>
          Select preferred slot
        </Typography>
        <Stack spacing={2} role="status" aria-label="Loading slot calendar">
          <Skeleton variant="rectangular" height={320} animation="wave" sx={{ borderRadius: 2 }} />
          <Skeleton variant="rectangular" height={120} animation="wave" sx={{ borderRadius: 2 }} />
        </Stack>
      </Container>
    );
  }

  // ── Error state ────────────────────────────────────────────────────────────
  if (apptError || slotsError || !appointment) {
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Typography variant="h4" component="h1" gutterBottom>
          Select preferred slot
        </Typography>
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => void refetchSlots()}>
              Try again
            </Button>
          }
        >
          Could not load the slot calendar. Please try again.
        </Alert>
        <Button
          variant="text"
          onClick={() => navigate('/appointments')}
          sx={{ mt: 2 }}
        >
          Back to My Appointments
        </Button>
      </Container>
    );
  }

  // ── Derived: slots for the selected day split by eligibility ──────────────
  const eligibleSlotsForDay = slotsForDay.filter((s) => !s.isAvailable);

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Select preferred slot
      </Typography>

      {/* UXR-003: Permanent guidance explaining the automatic swap process */}
      <Alert
        icon={<InfoIcon fontSize="inherit" />}
        severity="info"
        sx={{ mb: 3 }}
      >
        <Typography variant="body2" fontWeight={500}>
          How this works
        </Typography>
        <Typography variant="body2">
          Select a slot that&rsquo;s already taken — we&rsquo;ll automatically swap you in and
          notify you if it becomes available. Greyed-out slots are open and can be booked directly.
        </Typography>
      </Alert>

      {/* BUG-011 fix: always render the calendar so patients can browse months.
          Previously the calendar was hidden when no booked slots existed,
          making it impossible to navigate to months that do have booked slots. */}
      <LocalizationProvider dateAdapter={AdapterDayjs}>
        {futureSlots.length === 0 ? (
          // ── Truly empty: no future slots returned for this month ─────────────
          <Box sx={{ textAlign: 'center', py: 6 }}>
            <Typography variant="body1" color="text.secondary" gutterBottom>
              No slots found for this month.
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Use the calendar arrows to browse other months.
            </Typography>
          </Box>
        ) : (
          <Box
            sx={{
              bgcolor: 'background.paper',
              borderRadius: 2,
              boxShadow: '0 1px 3px rgba(0,0,0,0.12)',
              mb: 3,
              overflow: 'hidden',
            }}
          >
            <DateCalendar
              value={selectedDate}
              onChange={(date) => {
                if (date) {
                  setSelectedDate(date);
                  setSelectedSlotDatetime(null); // reset selection on day change
                }
              }}
              disablePast
              shouldDisableDate={shouldDisableDate}
              sx={{
                width: '100%',
                // Highlight days that have watchlist-eligible (booked) slots
                '& .MuiPickersDay-root:not(.Mui-disabled):not(.MuiPickersDay-today)': {
                  color: 'primary.main',
                  fontWeight: 600,
                },
              }}
            />
          </Box>
        )}

        {/* Slot grid for the selected day */}
        {slotsForDay.length > 0 && (
          <Box
            sx={{
              bgcolor: 'background.paper',
              borderRadius: 2,
              boxShadow: '0 1px 3px rgba(0,0,0,0.12)',
              p: 3,
              mb: 3,
            }}
          >
            <Typography variant="subtitle1" fontWeight={500} gutterBottom>
              {selectedDate.format('dddd, MMMM D, YYYY')}
            </Typography>

            {/* Per-day hint when all slots on this day are still available */}
            {eligibleSlotsForDay.length === 0 && (
              <Alert severity="warning" sx={{ mb: 2 }}>
                All slots on this day are currently open. Select a taken slot to join the watchlist,
                or book an available slot directly.
              </Alert>
            )}

            <Grid container spacing={2}>
              {slotsForDay.map((slot) => {
                const isEligible = !slot.isAvailable; // booked = watchlist eligible
                const isSelected = selectedSlotDatetime === slot.datetime;

                  return (
                    <Grid item xs={6} sm={4} key={slot.datetime}>
                      <Box
                        component="button"
                        onClick={() => isEligible && setSelectedSlotDatetime(slot.datetime)}
                        disabled={!isEligible}
                        aria-pressed={isSelected}
                        aria-label={`${formatTime(slot.datetime)} ${isEligible ? '– select for watchlist' : '– available, cannot watchlist'}`}
                        sx={{
                          width: '100%',
                          minHeight: 44,
                          border: '1px solid',
                          borderColor: isSelected ? 'primary.main' : 'divider',
                          borderRadius: 1,
                          bgcolor: isSelected ? 'primary.main' : 'background.paper',
                          color: isSelected
                            ? '#fff'
                            : isEligible
                            ? 'text.primary'
                            : 'text.disabled',
                          opacity: isEligible ? 1 : 0.4,
                          cursor: isEligible ? 'pointer' : 'not-allowed',
                          p: 1.5,
                          textAlign: 'center',
                          fontFamily: 'inherit',
                          fontSize: '0.875rem',
                          fontWeight: 500,
                          transition: 'all 150ms',
                          '&:hover:not(:disabled)': {
                            bgcolor: isSelected ? 'primary.dark' : 'primary.50',
                            borderColor: 'primary.main',
                          },
                        }}
                      >
                        {formatTime(slot.datetime)}
                        {!isEligible && (
                          <Box
                            component="span"
                            display="block"
                            sx={{ fontSize: '0.75rem', fontWeight: 400 }}
                          >
                            Available
                          </Box>
                        )}
                      </Box>
                    </Grid>
                  );
                })}
              </Grid>
            </Box>
          )}
        </LocalizationProvider>

      {/* Action buttons */}
      <Stack spacing={2}>
        <Button
          variant="contained"
          fullWidth
          disabled={!selectedSlotDatetime || isSubmitting}
          onClick={handleConfirm}
          aria-busy={isSubmitting}
          sx={{ minHeight: 44 }}
          startIcon={
            isSubmitting ? <CircularProgress size={16} color="inherit" /> : undefined
          }
        >
          {isSubmitting ? 'Registering…' : 'Confirm preferred slot'}
        </Button>
        <Button
          variant="outlined"
          fullWidth
          onClick={() => navigate('/appointments')}
          disabled={isSubmitting}
          sx={{ minHeight: 44 }}
        >
          Cancel
        </Button>
      </Stack>

      {/* Toast feedback (UXR-402) */}
      <Snackbar
        open={snackbar.open}
        autoHideDuration={4000}
        onClose={() => setSnackbar((s) => ({ ...s, open: false }))}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          onClose={() => setSnackbar((s) => ({ ...s, open: false }))}
          severity={snackbar.severity}
          sx={{ width: '100%' }}
          elevation={6}
          variant="filled"
        >
          {snackbar.message}
        </Alert>
      </Snackbar>
    </Container>
  );
}
