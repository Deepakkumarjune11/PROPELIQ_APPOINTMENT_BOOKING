// SCR-020 — Verification Complete (US_023, AC-5, UXR-003).
//
// Centred completion card (max-width 600px) shown after "Finalize Patient Summary" succeeds.
// Wireframe: green circular check icon (80px), success-alert with chart summary, 2-col stats
// (time to verify + AI confidence avg), "Next patient" CTA → /staff/patients (SCR-016).
//
// Route state (from CodeVerificationPage navigate):
//   { patientName, factCount, codesConfirmed, startedAt, avgConfidence, conflictsResolved? }
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import NavigateNextIcon from '@mui/icons-material/NavigateNext';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Typography,
} from '@mui/material';
import { useRef } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';

interface VerificationCompleteState {
  patientName?: string;
  factCount?: number;
  codesConfirmed?: number;
  conflictsResolved?: number;
  startedAt?: number;
  avgConfidence?: number | null;
}

const VerificationCompletePage: React.FC = () => {
  useParams<{ patientId: string }>(); // consumed for route match; patientId unused in display
  const { state } = useLocation() as { state: VerificationCompleteState | null };
  const navigate = useNavigate();
  const completionTime = useRef(Date.now());

  // Duration from when CodeVerificationPage navigated here to now
  const startedAt = state?.startedAt ?? completionTime.current;
  const durationMs = completionTime.current - startedAt;
  const minutes = Math.floor(durationMs / 60_000);
  const seconds = String(Math.floor((durationMs % 60_000) / 1000)).padStart(2, '0');
  const durationFormatted = `${minutes}:${seconds}`;

  const factCount = state?.factCount ?? 0;
  const codesConfirmed = state?.codesConfirmed ?? 0;
  const patientName = state?.patientName ?? 'Patient';
  const avgConfidence = state?.avgConfidence ?? null;

  return (
    <Box
      display="flex"
      alignItems="center"
      justifyContent="center"
      minHeight="100vh"
      p={4}
      bgcolor="grey.50"
    >
      <Card sx={{ maxWidth: 600, width: '100%', borderRadius: 2, boxShadow: 1 }}>
        <CardContent sx={{ p: 4, textAlign: 'center' }}>
          {/* Success icon circle — green 80px (wireframe-SCR-020) */}
          <Box
            width={80}
            height={80}
            borderRadius="50%"
            bgcolor="success.main"
            color="white"
            display="flex"
            alignItems="center"
            justifyContent="center"
            mx="auto"
            mb={3}
            aria-hidden="true"
          >
            <CheckCircleIcon sx={{ fontSize: 48 }} />
          </Box>

          <Typography variant="h4" fontWeight={500} mb={1} color="text.primary">
            Verification complete
          </Typography>
          <Typography color="text.secondary" mb={3}>
            Patient chart verified and ready for appointment
          </Typography>

          {/* Chart summary alert — success.50 background, 4px solid success.500 left border */}
          <Alert
            severity="success"
            icon={false}
            sx={{
              textAlign: 'left',
              mb: 3,
              bgcolor: '#E8F5E9',
              borderLeft: '4px solid #4CAF50',
            }}
          >
            <Typography variant="subtitle2" fontWeight={500} mb={0.5}>
              Chart Summary
            </Typography>
            <Typography variant="body2">
              {patientName} • {factCount} fact{factCount !== 1 ? 's' : ''} extracted •{' '}
              {codesConfirmed} code{codesConfirmed !== 1 ? 's' : ''} confirmed
            </Typography>
          </Alert>

          {/* 2-column stats grid (wireframe-SCR-020) */}
          <Box
            display="grid"
            gridTemplateColumns="1fr 1fr"
            gap={2}
            mb={3}
          >
            <Box bgcolor="grey.50" p={2} borderRadius={1}>
              <Typography variant="h5" fontWeight={500} color="text.primary">
                {durationFormatted}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Time to verify
              </Typography>
            </Box>
            <Box bgcolor="grey.50" p={2} borderRadius={1}>
              <Typography variant="h5" fontWeight={500} color="text.primary">
                {avgConfidence !== null ? `${avgConfidence}%` : '—'}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                AI confidence avg
              </Typography>
            </Box>
          </Box>

          {/* Next patient CTA — navigates to SCR-016 patient list */}
          <Button
            variant="contained"
            fullWidth
            startIcon={<NavigateNextIcon />}
            onClick={() => navigate('/staff/patients')}
            sx={{ minHeight: 44 }}
            aria-label="Go to next patient"
          >
            Next patient
          </Button>
        </CardContent>
      </Card>
    </Box>
  );
};

export default VerificationCompletePage;
