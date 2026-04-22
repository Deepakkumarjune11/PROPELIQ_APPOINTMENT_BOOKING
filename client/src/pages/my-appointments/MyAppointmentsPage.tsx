// SCR-008 — My Appointments (US_015).
// Displays all booked appointments for the authenticated patient.
// Implements all 4 required states: Default, Loading, Empty, Error.
//
// UXR-003: inline tooltip guidance on watchlist badge (in AppointmentCard)
// UXR-101: WCAG 2.2 AA  |  UXR-102: ARIA labels  |  UXR-201: responsive 375/768/1440px
// UXR-401: loading feedback < 200ms (Skeleton)  |  UXR-501: actionable error messages
import CalendarTodayIcon from '@mui/icons-material/CalendarToday';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Skeleton from '@mui/material/Skeleton';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { useNavigate } from 'react-router-dom';

import { useAppointments } from '@/hooks/useAppointments';
import AppointmentCard from './AppointmentCard';

// 3 skeleton cards mirror the average expected appointment count (UXR-401)
const SKELETON_COUNT = 3;

export default function MyAppointmentsPage() {
  const navigate = useNavigate();
  const { appointments, isLoading, isError, refetch } = useAppointments();

  // ── Loading state ─────────────────────────────────────────────────────────
  if (isLoading) {
    return (
      <Container maxWidth="md" sx={{ py: 4 }}>
        <Typography variant="h4" component="h1" gutterBottom>
          My appointments
        </Typography>
        <Stack spacing={3} role="status" aria-label="Loading appointments">
          {Array.from({ length: SKELETON_COUNT }).map((_, i) => (
            <Skeleton
              key={i}
              variant="rectangular"
              height={120}
              animation="wave"
              sx={{ borderRadius: 2 }}
            />
          ))}
        </Stack>
      </Container>
    );
  }

  // ── Error state ────────────────────────────────────────────────────────────
  if (isError) {
    return (
      <Container maxWidth="md" sx={{ py: 4 }}>
        <Typography variant="h4" component="h1" gutterBottom>
          My appointments
        </Typography>
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => void refetch()}>
              Try again
            </Button>
          }
          sx={{ mb: 2 }}
        >
          Could not load your appointments. Please try again.
        </Alert>
      </Container>
    );
  }

  // ── Empty state ────────────────────────────────────────────────────────────
  if (appointments.length === 0) {
    return (
      <Container maxWidth="md" sx={{ py: 4 }}>
        <Typography variant="h4" component="h1" gutterBottom>
          My appointments
        </Typography>
        <Box
          sx={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            py: 8,
            gap: 2,
          }}
        >
          <CalendarTodayIcon sx={{ fontSize: 80, color: 'text.disabled' }} aria-hidden="true" />
          <Typography variant="h6" color="text.secondary">
            No appointments yet
          </Typography>
          <Typography variant="body2" color="text.secondary" textAlign="center">
            You don't have any upcoming appointments.
          </Typography>
          <Button
            variant="contained"
            onClick={() => navigate('/appointments/search')}
            sx={{ mt: 1, minHeight: 44 }}
          >
            Book an appointment
          </Button>
        </Box>
      </Container>
    );
  }

  // ── Default state — appointment list ─────────────────────────────────────
  return (
    <Container maxWidth="md" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        My appointments
      </Typography>
      <Stack spacing={3}>
        {appointments.map((appointment) => (
          <AppointmentCard key={appointment.id} appointment={appointment} />
        ))}
      </Stack>
    </Container>
  );
}
