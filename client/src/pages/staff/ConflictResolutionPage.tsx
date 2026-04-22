// SCR-018 — Conflict Resolution Page (US_022, AC-1 through AC-4).
// Route: /staff/patients/:patientId/conflict-resolution
//
// States:
//   Loading  — Skeleton cards while useConflicts fetches
//   Empty    — Alert info "No conflicts detected" + Continue button active
//   Default  — ConflictCard list + progress Alert + Continue button gated
//   Error    — Alert error + Retry button
//
// Design:  wireframe-SCR-018-conflict-resolution.html
//          max-width 900px; breadcrumb (UXR-002); conflict-card border-left 4px #F44336
import {
  Alert,
  Box,
  Breadcrumbs,
  Button,
  Skeleton,
  Tooltip,
  Typography,
} from '@mui/material';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';

import ConflictCard from '@/components/clinical/ConflictCard';
import { useConflicts } from '@/hooks/useConflicts';

const ConflictResolutionPage: React.FC = () => {
  const { patientId } = useParams<{ patientId: string }>();
  const navigate = useNavigate();

  const { data: conflicts = [], isLoading, isError, refetch } = useConflicts(patientId ?? '');

  const unresolvedCount = conflicts.length;
  const allResolved = !isLoading && !isError && unresolvedCount === 0;

  return (
    <Box maxWidth={900} mx="auto" p={3}>
      {/* Breadcrumb — UXR-002 */}
      <Breadcrumbs sx={{ mb: 2 }} aria-label="breadcrumb">
        <RouterLink
          to="/staff/patients"
          style={{ color: 'inherit', textDecoration: 'underline' }}
        >
          Chart Review
        </RouterLink>
        <RouterLink
          to={`/staff/patients/${patientId ?? ''}/360-view`}
          style={{ color: 'inherit', textDecoration: 'underline' }}
        >
          360-View
        </RouterLink>
        <Typography color="text.primary">Resolve Conflicts</Typography>
      </Breadcrumbs>

      <Typography variant="h4" fontWeight={400} mb={3}>
        Resolve conflicts
      </Typography>

      {/* Progress indicator — warning while unresolved, success when all done */}
      {!isLoading && !isError && (
        <Alert
          severity={allResolved ? 'success' : 'warning'}
          sx={{ mb: 3 }}
          aria-live="polite"
        >
          {allResolved
            ? 'All conflicts resolved. Summary ready for verification.'
            : `${unresolvedCount} conflict${unresolvedCount !== 1 ? 's' : ''} remaining`}
        </Alert>
      )}

      {/* Loading state — Skeleton per card */}
      {isLoading &&
        [0, 1].map((i) => (
          <Skeleton
            key={i}
            variant="rectangular"
            height={220}
            sx={{ mb: 3, borderRadius: 2 }}
          />
        ))}

      {/* Error state */}
      {isError && (
        <Alert
          severity="error"
          sx={{ mb: 3 }}
          action={
            <Button color="inherit" size="small" onClick={() => void refetch()}>
              Retry
            </Button>
          }
        >
          Failed to load conflicts. Please try again.
        </Alert>
      )}

      {/* Empty state when no conflicts */}
      {!isLoading && !isError && unresolvedCount === 0 && (
        <Alert severity="info" sx={{ mb: 3 }}>
          No conflicts detected for this patient.
        </Alert>
      )}

      {/* Conflict cards */}
      {!isLoading &&
        !isError &&
        conflicts.map((conflict) => (
          <ConflictCard
            key={conflict.conflictId}
            conflict={conflict}
            patientId={patientId ?? ''}
          />
        ))}

      {/* Continue to code verification — disabled until all conflicts resolved (AC-4) */}
      <Tooltip
        title={!allResolved ? 'Resolve all conflicts before continuing' : ''}
        arrow
      >
        {/* Span wrapper required for Tooltip on a disabled Button (MUI requirement) */}
        <span>
          <Button
            variant="contained"
            size="large"
            fullWidth
            disabled={!allResolved}
            onClick={() => void navigate(`/staff/patients/${patientId ?? ''}/code-verification`)}
            aria-label="Continue to code verification"
            sx={{ minHeight: 44, mt: 1 }}
          >
            Continue to code verification
          </Button>
        </span>
      </Tooltip>
    </Box>
  );
};

export default ConflictResolutionPage;
