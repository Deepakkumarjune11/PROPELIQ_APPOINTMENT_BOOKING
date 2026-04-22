// SCR-019 — Code Verification (US_023, FR-014, AIR-005, UXR-002, UXR-003).
//
// Displays all CodeSuggestion rows for the patient in a responsive grid.
// Each card: ICD-10/CPT badge, description, evidence breadcrumb chips, Accept/Reject/Modify.
// Evidence chip click → opens SourceCitationDrawer (reuses useFactSource from US_021).
// Fixed bottom bar: "Finalize Patient Summary" disabled + Tooltip when pending codes remain;
// "Back to review" navigates to SCR-017.
// Finalize: PATCH /360-view/{view360Id}/status = 'verified' → navigate to SCR-020.
//
// States: Loading (Skeleton ×3), Error (Alert), Empty (Alert info), Default (card grid).
// Responsive: code-grid collapses to 1-col at ≤ 600px (minmax(400px, 1fr)).
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import VerifiedIcon from '@mui/icons-material/Verified';
import {
  Alert,
  Box,
  Breadcrumbs,
  Button,
  Link,
  Skeleton,
  Tooltip,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';

import { patchView360Status } from '@/api/codeSuggestions';
import CodeSuggestionCard from '@/components/clinical/CodeSuggestionCard';
import SourceCitationDrawer from '@/components/clinical/SourceCitationDrawer';
import { useCodeSuggestions } from '@/hooks/useCodeSuggestions';
import { useFactSource } from '@/hooks/useFactSource';
import { usePatientView360 } from '@/hooks/usePatientView360';

const CodeVerificationPage: React.FC = () => {
  const { patientId } = useParams<{ patientId: string }>();
  const pid = patientId ?? '';
  const navigate = useNavigate();

  const { data: codes = [], isLoading, isError, refetch: refetchCodes } = useCodeSuggestions(pid);
  const { data: view360 } = usePatientView360(pid);

  // ── Evidence citation drawer ───────────────────────────────────────────────
  const [citationFactId, setCitationFactId] = useState<string | null>(null);
  const { data: citation, isFetching: citationFetching, refetch: refetchCitation } =
    useFactSource(citationFactId);

  const handleEvidenceClick = (factId: string) => {
    setCitationFactId(factId);
    void refetchCitation();
  };

  // ── Progress ───────────────────────────────────────────────────────────────
  const pendingCount = codes.filter((c) => !c.staffReviewed).length;
  const allReviewed = pendingCount === 0 && codes.length > 0;

  // ── Finalize ───────────────────────────────────────────────────────────────
  const [finalizing, setFinalizing] = useState(false);
  const [finalizeError, setFinalizeError] = useState<string | null>(null);

  const handleFinalize = async () => {
    if (!view360 || !allReviewed) return;
    setFinalizing(true);
    setFinalizeError(null);
    try {
      // view360.patient.patientId is the patient ID; the view360Id is not directly exposed
      // by usePatientView360. We use the patientId-based status endpoint.
      // The backend ConflictController PATCH /360-view/{view360Id}/status uses view360Id.
      // PatientView360Dto does not include view360Id currently, so we fall back to patientId-
      // based finalize via the existing PATCH endpoint pattern.
      await patchView360Status(pid, 'verified');
      const acceptedCount = codes.filter((c) => c.reviewOutcome === 'accepted').length;
      navigate(`/staff/patients/${pid}/verification-complete`, {
        state: {
          patientName: view360.patient.fullName,
          factCount: view360.facts?.length ?? 0,
          codesConfirmed: acceptedCount,
          startedAt: Date.now(),
          avgConfidence:
            codes.length > 0
              ? Math.round(
                  (codes.reduce((sum, c) => sum + c.confidenceScore, 0) / codes.length) * 100,
                )
              : null,
        },
      });
    } catch (err) {
      setFinalizeError(
        err instanceof Error ? err.message : 'Failed to finalize patient summary.',
      );
    } finally {
      setFinalizing(false);
    }
  };

  return (
    <Box sx={{ minHeight: '100vh', pb: 10, bgcolor: 'background.default' }}>
      {/* ── Page header ─────────────────────────────────────────────────── */}
      <Box
        sx={{
          bgcolor: 'background.paper',
          boxShadow: '0 1px 3px rgba(0,0,0,0.12)',
          px: 3,
          py: 2,
          display: 'flex',
          alignItems: 'center',
          gap: 2,
        }}
      >
        <Button
          startIcon={<ArrowBackIcon />}
          component={RouterLink}
          to={`/staff/patients/${pid}/360-view`}
          sx={{ minWidth: 0 }}
          aria-label="Back to patient view"
        >
          Back
        </Button>
        <Typography variant="subtitle1" fontWeight={500}>
          Code verification
        </Typography>
      </Box>

      {/* ── Main content ────────────────────────────────────────────────── */}
      <Box maxWidth={1200} mx="auto" p={3}>
        {/* Breadcrumbs — UXR-002 */}
        <Breadcrumbs aria-label="breadcrumb" sx={{ mb: 2 }}>
          <Link component={RouterLink} to="/staff/patients" color="inherit" underline="hover">
            Patients
          </Link>
          <Link
            component={RouterLink}
            to={`/staff/patients/${pid}/360-view`}
            color="inherit"
            underline="hover"
          >
            360 View
          </Link>
          <Typography color="text.primary">Code Verification</Typography>
        </Breadcrumbs>

        <Typography variant="h5" fontWeight={500} mb={0.5}>
          Review and confirm codes
        </Typography>
        <Typography variant="body2" color="text.secondary" mb={3}>
          Suggested codes based on patient's clinical data
          {view360 ? ` • ${view360.patient.fullName} (${view360.patient.mrn})` : ''}
        </Typography>

        {/* Progress indicator (UXR-003) */}
        {!isLoading && !isError && codes.length > 0 && (
          <Alert
            severity={allReviewed ? 'success' : 'warning'}
            aria-live="polite"
            sx={{ mb: 3 }}
          >
            {allReviewed
              ? 'All codes reviewed — ready to finalize.'
              : `${codes.length - pendingCount} of ${codes.length} codes reviewed. ${pendingCount} remaining.`}
          </Alert>
        )}

        {/* Finalize error */}
        {finalizeError && (
          <Alert severity="error" sx={{ mb: 3 }}>
            {finalizeError}
          </Alert>
        )}

        {/* ── Loading ───────────────────────────────────────────────────── */}
        {isLoading &&
          [0, 1, 2].map((i) => (
            <Skeleton
              key={i}
              variant="rectangular"
              height={200}
              sx={{ mb: 3, borderRadius: 2 }}
            />
          ))}

        {/* ── Error ─────────────────────────────────────────────────────── */}
        {isError && (
          <Alert
            severity="error"
            action={
              <Button color="inherit" size="small" onClick={() => refetchCodes()}>
                Retry
              </Button>
            }
          >
            Failed to load code suggestions. Please try again.
          </Alert>
        )}

        {/* ── Empty state ───────────────────────────────────────────────── */}
        {!isLoading && !isError && codes.length === 0 && (
          <Alert severity="info">
            No code suggestions generated for this patient. You may still finalize the patient
            summary below.
          </Alert>
        )}

        {/* ── Code grid — collapses to 1-col at ≤ 600px (wireframe-SCR-019) ─ */}
        {!isLoading && !isError && codes.length > 0 && (
          <Box
            display="grid"
            sx={{
              gridTemplateColumns: { xs: '1fr', sm: 'repeat(auto-fill, minmax(400px, 1fr))' },
              gap: 3,
            }}
          >
            {codes.map((code) => (
              <CodeSuggestionCard
                key={code.id}
                code={code}
                patientId={pid}
                onEvidenceClick={handleEvidenceClick}
              />
            ))}
          </Box>
        )}
      </Box>

      {/* ── Fixed bottom action bar (wireframe-SCR-019) ─────────────────── */}
      <Box
        position="fixed"
        bottom={0}
        left={0}
        right={0}
        bgcolor="background.paper"
        boxShadow="0 -1px 3px rgba(0,0,0,0.12)"
        px={3}
        py={2}
        display="flex"
        justifyContent="center"
        gap={2}
        zIndex={1100}
      >
        <Button
          variant="outlined"
          component={RouterLink}
          to={`/staff/patients/${pid}/360-view`}
          sx={{ minHeight: 44, px: 4 }}
        >
          Back to review
        </Button>

        {/* Tooltip wraps span so tooltip works on disabled button (UXR-003 / MUI requirement) */}
        <Tooltip
          title={
            !allReviewed && codes.length > 0
              ? 'Review all codes before finalizing'
              : isLoading
              ? 'Loading codes…'
              : ''
          }
          arrow
        >
          <span>
            <Button
              variant="contained"
              startIcon={<VerifiedIcon />}
              disabled={isLoading || finalizing || (!allReviewed && codes.length > 0)}
              onClick={() => void handleFinalize()}
              sx={{ minHeight: 44, px: 4 }}
              aria-label="Finalize patient summary"
            >
              {finalizing ? 'Finalizing…' : 'Finalize Patient Summary'}
            </Button>
          </span>
        </Tooltip>
      </Box>

      {/* ── Source citation drawer (reuse from US_021, AC-2) ────────────── */}
      <SourceCitationDrawer
        open={!!citationFactId}
        onClose={() => setCitationFactId(null)}
        citation={citation}
        isFetching={citationFetching}
      />
    </Box>
  );
};

export default CodeVerificationPage;
