// SCR-010 — Staff Dashboard (US_016, AC-1).
// Four summary cards + same-day queue table with Loading / Error / Empty / Default states.
// Breadcrumb: Home > Staff Dashboard (UXR-002).
import {
  Alert,
  Box,
  Breadcrumbs,
  Button,
  Chip,
  Grid,
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
import { useQuery } from '@tanstack/react-query';
import { Link as RouterLink, useNavigate } from 'react-router-dom';

import { type DashboardData, getDashboardData } from '@/api/staff';
import DashboardSummaryCard from './DashboardSummaryCard';

// ── Status badge colour mapping ───────────────────────────────────────────────

function statusChipColor(status: string): 'warning' | 'success' | 'info' | 'default' {
  switch (status) {
    case 'waiting':
      return 'warning';
    case 'arrived':
    case 'in-room':
      return 'success';
    default:
      return 'default';
  }
}

function statusLabel(status: string): string {
  switch (status) {
    case 'waiting':
      return 'Waiting';
    case 'arrived':
      return 'Arrived';
    case 'in-room':
      return 'In Room';
    default:
      return status;
  }
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function StaffDashboardPage() {
  const navigate = useNavigate();

  const { data, isLoading, isError, refetch } = useQuery<DashboardData, Error>({
    queryKey: ['staff', 'dashboard'],
    queryFn: getDashboardData,
    staleTime: 30_000,
  });

  const summary = data?.summary;
  const queue   = data?.queue ?? [];

  return (
    <Box sx={{ p: { xs: 2, md: 3 } }}>
      {/* Breadcrumb — UXR-002 */}
      <Breadcrumbs aria-label="breadcrumb" sx={{ mb: 2 }}>
        <Link component={RouterLink} to="/" underline="hover" color="inherit">
          Home
        </Link>
        <Typography color="text.primary">Staff Dashboard</Typography>
      </Breadcrumbs>

      {/* Page header */}
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          mb: 4,
          flexWrap: 'wrap',
          gap: 2,
        }}
      >
        <Typography variant="h4" component="h1" fontWeight={400}>
          Staff Dashboard
        </Typography>
        <Button
          variant="contained"
          onClick={() => void navigate('/staff/walk-in')}
          sx={{ backgroundColor: 'primary.main' }}
        >
          Walk-In Booking
        </Button>
      </Box>

      {/* ── Error state ── */}
      {isError && !isLoading && (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => void refetch()}>
              Try again
            </Button>
          }
          sx={{ mb: 3 }}
        >
          Failed to load dashboard data. Please try again.
        </Alert>
      )}

      {/* ── Summary cards ── */}
      <Grid container spacing={3} sx={{ mb: 4 }}>
        <Grid item xs={12} sm={6} lg={3}>
          <DashboardSummaryCard
            title="Walk-Ins Today"
            value={summary?.walkInsToday}
            subtitle="+3 from yesterday"
            isLoading={isLoading}
          />
        </Grid>
        <Grid item xs={12} sm={6} lg={3}>
          <DashboardSummaryCard
            title="Queue Length"
            value={summary?.queueLength}
            subtitle="Avg wait: 18 min"
            badge={summary ? `${summary.queueLength} waiting` : undefined}
            badgeColor="warning"
            isLoading={isLoading}
          />
        </Grid>
        <Grid item xs={12} sm={6} lg={3}>
          <DashboardSummaryCard
            title="Verification Pending"
            value={summary?.verificationPending}
            subtitle="Oldest: 2 hours ago"
            badge={summary ? `${summary.verificationPending} charts` : undefined}
            badgeColor="warning"
            isLoading={isLoading}
          />
        </Grid>
        <Grid item xs={12} sm={6} lg={3}>
          <DashboardSummaryCard
            title="Critical Conflicts"
            value={summary?.criticalConflicts}
            subtitle="Requires review"
            badge={summary?.criticalConflicts ? `${summary.criticalConflicts} urgent` : undefined}
            badgeColor="error"
            isLoading={isLoading}
          />
        </Grid>
      </Grid>

      {/* ── Same-day queue table ── */}
      <Paper elevation={1} sx={{ borderRadius: 2, overflow: 'hidden' }}>
        <Box sx={{ p: 3, borderBottom: 1, borderColor: 'divider' }}>
          <Typography variant="h5" component="h2" fontWeight={400}>
            Same-Day Queue
          </Typography>
        </Box>

        {isLoading ? (
          <Box sx={{ p: 3 }}>
            {[1, 2, 3].map((i) => (
              <Skeleton key={i} variant="rectangular" height={48} sx={{ mb: 1, borderRadius: 1 }} />
            ))}
          </Box>
        ) : queue.length === 0 ? (
          /* ── Empty state ── */
          <Box
            sx={{
              p: 6,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              gap: 2,
            }}
          >
            <Typography color="text.secondary">No walk-ins today.</Typography>
            <Button variant="contained" onClick={() => void navigate('/staff/walk-in')}>
              Book a Walk-In
            </Button>
          </Box>
        ) : (
          /* ── Default table ── */
          <TableContainer>
            <Table aria-label="Same-day queue">
              <TableHead>
                <TableRow sx={{ backgroundColor: 'grey.100' }}>
                  <TableCell sx={{ fontWeight: 500 }}>Patient Name</TableCell>
                  <TableCell sx={{ fontWeight: 500 }}>Check-In Time</TableCell>
                  <TableCell sx={{ fontWeight: 500 }}>Visit Type</TableCell>
                  <TableCell sx={{ fontWeight: 500 }}>Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {queue.map((entry) => (
                  <TableRow key={entry.appointmentId} hover>
                    <TableCell>{entry.patientName}</TableCell>
                    <TableCell>
                      {new Date(entry.appointmentTime).toLocaleTimeString([], {
                        hour: '2-digit',
                        minute: '2-digit',
                      })}
                    </TableCell>
                    <TableCell>{entry.visitType}</TableCell>
                    <TableCell>
                      <Chip
                        label={statusLabel(entry.status)}
                        color={statusChipColor(entry.status)}
                        size="small"
                      />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Paper>
    </Box>
  );
}
