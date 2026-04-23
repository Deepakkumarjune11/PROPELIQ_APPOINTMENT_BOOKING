// SCR-016 — Patient Chart Review / Verification Queue (US_021).
// MUI Table with 6 columns; Conflict (error) and Pending (warning) Chip badges;
// Skeleton on loading; "No patients pending review" empty state; Error retry.
// Breadcrumb: Staff > Verification queue (UXR-002).
import {
  Alert,
  Box,
  Breadcrumbs,
  Button,
  Chip,
  Link,
  Paper,
  Skeleton,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { Link as RouterLink, useNavigate } from 'react-router-dom';

import { useVerificationQueue } from '@/hooks/useVerificationQueue';

function formatAppointmentDatetime(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  const today = new Date();
  const isToday =
    d.getFullYear() === today.getFullYear() &&
    d.getMonth() === today.getMonth() &&
    d.getDate() === today.getDate();

  const time = d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  if (isToday) return `Today ${time}`;

  const tomorrow = new Date(today);
  tomorrow.setDate(today.getDate() + 1);
  const isTomorrow =
    d.getFullYear() === tomorrow.getFullYear() &&
    d.getMonth() === tomorrow.getMonth() &&
    d.getDate() === tomorrow.getDate();
  if (isTomorrow) return `Tomorrow ${time}`;

  return `${d.toLocaleDateString()} ${time}`;
}

const PatientChartReviewPage: React.FC = () => {
  const navigate = useNavigate();
  const { data, isLoading, isError, refetch } = useVerificationQueue();

  const handleRowClick = (patientId: string) => {
    void navigate(`/staff/patients/${patientId}/360-view`);
  };

  // ── Loading state ────────────────────────────────────────────────────────
  if (isLoading) {
    return (
      <Box p={3}>
        <Typography variant="h5" mb={3}>
          Verification queue
        </Typography>
        <TableContainer component={Paper} sx={{ boxShadow: 1 }}>
          <Table>
            <TableHead>
              <TableRow>
                {['Patient', 'MRN', 'Appointment', 'Documents', 'Priority', 'Action'].map(
                  (col) => (
                    <TableCell key={col} sx={{ fontWeight: 500 }}>
                      {col}
                    </TableCell>
                  ),
                )}
              </TableRow>
            </TableHead>
            <TableBody>
              {[1, 2, 3].map((i) => (
                <TableRow key={i}>
                  {Array.from({ length: 6 }).map((_, j) => (
                    <TableCell key={j}>
                      <Skeleton variant="text" />
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Box>
    );
  }

  // ── Error state ──────────────────────────────────────────────────────────
  if (isError) {
    return (
      <Box p={3}>
        <Typography variant="h5" mb={3}>
          Verification queue
        </Typography>
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => void refetch()}>
              Retry
            </Button>
          }
        >
          Failed to load the verification queue. Please try again.
        </Alert>
      </Box>
    );
  }

  // ── Empty state ──────────────────────────────────────────────────────────
  if (!data || data.length === 0) {
    return (
      <Box p={3}>
        <Breadcrumbs sx={{ mb: 2 }}>
          <Link component={RouterLink} to="/staff/dashboard" underline="hover" color="inherit">
            Staff
          </Link>
          <Typography color="text.primary">Verification queue</Typography>
        </Breadcrumbs>
        <Typography variant="h5" mb={3}>
          Verification queue
        </Typography>
        <Paper sx={{ p: 4, textAlign: 'center', boxShadow: 1 }}>
          <Typography variant="body1" color="text.secondary">
            No patients pending review
          </Typography>
        </Paper>
      </Box>
    );
  }

  // ── Default state ────────────────────────────────────────────────────────
  return (
    <Box p={3}>
      <Breadcrumbs sx={{ mb: 2 }}>
        <Link component={RouterLink} to="/staff/dashboard" underline="hover" color="inherit">
          Staff
        </Link>
        <Typography color="text.primary">Verification queue</Typography>
      </Breadcrumbs>

      <Typography variant="h5" mb={3}>
        Verification queue
      </Typography>

      <TableContainer component={Paper} sx={{ boxShadow: 1 }}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell sx={{ fontWeight: 500 }}>Patient</TableCell>
              <TableCell sx={{ fontWeight: 500 }}>MRN</TableCell>
              <TableCell sx={{ fontWeight: 500 }}>Appointment</TableCell>
              <TableCell sx={{ fontWeight: 500 }}>Documents</TableCell>
              <TableCell sx={{ fontWeight: 500 }}>Priority</TableCell>
              <TableCell sx={{ fontWeight: 500 }}>Action</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data.map((entry) => (
              <TableRow
                key={entry.patientId}
                hover
                sx={{ cursor: 'pointer' }}
                onClick={() => handleRowClick(entry.patientId)}
              >
                <TableCell>{entry.patientName}</TableCell>
                <TableCell>{entry.mrn}</TableCell>
                <TableCell>{formatAppointmentDatetime(entry.appointmentDatetime)}</TableCell>
                <TableCell>{entry.documentCount} uploaded</TableCell>
                <TableCell>
                  {entry.priority === 'conflict' ? (
                    <Chip
                      label={`${entry.conflictCount} Conflict${entry.conflictCount !== 1 ? 's' : ''}`}
                      color="error"
                      size="small"
                    />
                  ) : (
                    <Chip label="Pending" color="warning" size="small" />
                  )}
                </TableCell>
                <TableCell>
                  {/* stopPropagation prevents double-navigation from row onClick */}
                  <Button
                    variant="contained"
                    size="small"
                    onClick={(e) => {
                      e.stopPropagation();
                      handleRowClick(entry.patientId);
                    }}
                  >
                    Review
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
};

export default PatientChartReviewPage;
