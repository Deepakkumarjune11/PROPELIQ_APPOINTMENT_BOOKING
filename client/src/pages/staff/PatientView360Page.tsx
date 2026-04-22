// SCR-017 — 360-Degree Patient View (US_021, AC-1, AC-3, AC-5).
// Patient identity header, conflict badge, 5-category MUI Tabs, FactCard grid,
// SourceCitationDrawer; all 5 states: Default / Loading / Empty / Error / Pending-assembly.
// Sidebar hides at ≤ 900px per wireframe media query.
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import VerifiedIcon from '@mui/icons-material/Verified';
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
  Tab,
  Tabs,
  Tooltip,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';

import { type ConsolidatedFact, type FactType } from '@/api/patientView360';
import FactCard from '@/components/clinical/FactCard';
import SourceCitationDrawer from '@/components/clinical/SourceCitationDrawer';
import { useFactSource } from '@/hooks/useFactSource';
import { usePatientView360 } from '@/hooks/usePatientView360';

// Category tabs aligned with DR-005 FactType enum values
const CATEGORIES: FactType[] = ['Vitals', 'Medications', 'History', 'Diagnoses', 'Procedures'];

function formatDob(iso: string): string {
  const d = new Date(iso);
  const age = Math.floor((Date.now() - d.getTime()) / (365.25 * 24 * 60 * 60 * 1000));
  return `${d.toLocaleDateString()} (${age} yrs)`;
}

const PatientView360Page: React.FC = () => {
  const { patientId } = useParams<{ patientId: string }>();
  const navigate = useNavigate();

  const { data, isLoading, isError, refetch } = usePatientView360(patientId ?? '');

  const [activeTab, setActiveTab] = useState(0);
  const [citationFactId, setCitationFactId] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);

  const {
    data: citation,
    isFetching: isCitationFetching,
    refetch: refetchCitation,
  } = useFactSource(citationFactId);

  const handleCiteClick = (factId: string) => {
    setCitationFactId(factId);
    setDrawerOpen(true);
    void refetchCitation();
  };

  const handleDrawerClose = () => {
    setDrawerOpen(false);
  };

  // ── Loading state ────────────────────────────────────────────────────────
  if (isLoading) {
    return (
      <Box p={3}>
        <Skeleton variant="text" width={240} height={32} sx={{ mb: 2 }} />
        <Skeleton variant="rectangular" height={80} sx={{ mb: 2, borderRadius: 1 }} />
        <Skeleton variant="rectangular" height={48} sx={{ mb: 2, borderRadius: 1 }} />
        <Grid container spacing={2}>
          {[1, 2, 3].map((i) => (
            <Grid item xs={12} sm={6} md={4} key={i}>
              <Skeleton variant="rectangular" height={120} sx={{ borderRadius: 1 }} />
            </Grid>
          ))}
        </Grid>
      </Box>
    );
  }

  // ── Error state ──────────────────────────────────────────────────────────
  if (isError) {
    return (
      <Box p={3}>
        <Button
          startIcon={<ArrowBackIcon />}
          onClick={() => void navigate('/staff/patients')}
          sx={{ mb: 2 }}
        >
          Back
        </Button>
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => void refetch()}>
              Retry
            </Button>
          }
        >
          Failed to load the patient 360-view. Please try again.
        </Alert>
      </Box>
    );
  }

  // ── 404 / pending-assembly state ─────────────────────────────────────────
  if (!data || data.assemblyStatus === 'pending') {
    return (
      <Box p={3}>
        <Button
          startIcon={<ArrowBackIcon />}
          onClick={() => void navigate('/staff/patients')}
          sx={{ mb: 2 }}
        >
          Back to queue
        </Button>
        <Paper sx={{ p: 4, textAlign: 'center', boxShadow: 1 }}>
          <Typography variant="body1" color="text.secondary">
            Summary is being assembled…
          </Typography>
          <Typography variant="caption" color="text.secondary" display="block" mt={1}>
            The AI extraction pipeline is still processing this patient's documents. Check back shortly.
          </Typography>
        </Paper>
      </Box>
    );
  }

  const activeCategory = CATEGORIES[activeTab];
  const categoryFacts: ConsolidatedFact[] = data.facts.filter(
    (f) => f.factType === activeCategory,
  );

  // ── Default state ────────────────────────────────────────────────────────
  return (
    <Box display="flex" flexDirection="column" minHeight="100%">
      {/* Page header with back button */}
      <Box
        display="flex"
        alignItems="center"
        gap={1}
        px={3}
        py={1.5}
        bgcolor="background.paper"
        borderBottom={1}
        borderColor="divider"
        boxShadow={1}
      >
        <Button
          startIcon={<ArrowBackIcon />}
          onClick={() => void navigate('/staff/patients')}
          aria-label="Back to chart review"
          size="small"
        >
          Back
        </Button>
        <Typography variant="subtitle1" fontWeight={500}>
          360-degree patient view
        </Typography>
      </Box>

      {/* Breadcrumb */}
      <Box px={3} pt={1}>
        <Breadcrumbs>
          <Link component={RouterLink} to="/staff/dashboard" underline="hover" color="inherit">
            Staff
          </Link>
          <Link component={RouterLink} to="/staff/patients" underline="hover" color="inherit">
            Verification queue
          </Link>
          <Typography color="text.primary">{data.patient.fullName}</Typography>
        </Breadcrumbs>
      </Box>

      {/* Patient identity header */}
      <Box
        px={3}
        py={2}
        bgcolor="background.paper"
        borderBottom={1}
        borderColor="divider"
        display="flex"
        justifyContent="space-between"
        alignItems="flex-start"
        flexWrap="wrap"
        gap={2}
      >
        <Box>
          <Typography variant="h5" fontWeight={500} color="text.primary" mb={0.5}>
            {data.patient.fullName}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            DOB: {formatDob(data.patient.dateOfBirth)}
            {' • '}
            MRN: {data.patient.mrn}
            {data.patient.insuranceName &&
              ` • Insurance: ${data.patient.insuranceName}${data.patient.insuranceMemberId ? ` #${data.patient.insuranceMemberId}` : ''}`}
          </Typography>
        </Box>

        <Box display="flex" gap={1} flexWrap="wrap" alignItems="center">
          {data.conflictCount > 0 && (
            <Chip
              icon={<WarningAmberIcon />}
              label={`${data.conflictCount} conflict${data.conflictCount !== 1 ? 's' : ''} detected`}
              color="error"
              onClick={() => void navigate(`/staff/patients/${patientId}/conflict-resolution`)}
              sx={{ cursor: 'pointer', fontWeight: 500 }}
              aria-label="View conflict details"
            />
          )}
          {/* Mark Verified — disabled when unresolved conflicts exist (AC-4, FR-013) */}
          <Tooltip
            title={data.conflictCount > 0 ? 'Resolve all conflicts before marking verified' : ''}
            arrow
          >
            {/* span wrapper required for Tooltip on disabled Button */}
            <span>
              <Button
                variant="contained"
                color="success"
                size="small"
                startIcon={<VerifiedIcon />}
                disabled={data.conflictCount > 0}
                aria-label="Mark patient summary as verified"
              >
                Mark Verified
              </Button>
            </span>
          </Tooltip>
        </Box>
      </Box>

      {/* Category tabs */}
      <Box bgcolor="background.paper" borderBottom={2} borderColor="divider">
        <Tabs
          value={activeTab}
          onChange={(_, v: number) => setActiveTab(v)}
          variant="scrollable"
          scrollButtons="auto"
          aria-label="Fact category tabs"
        >
          {CATEGORIES.map((cat) => (
            <Tab key={cat} label={cat} id={`tab-${cat}`} aria-controls={`tabpanel-${cat}`} />
          ))}
        </Tabs>
      </Box>

      {/* Tab panel content */}
      <Box
        flex={1}
        p={3}
        role="tabpanel"
        id={`tabpanel-${activeCategory}`}
        aria-labelledby={`tab-${activeCategory}`}
      >
        {/* Empty state for whole view */}
        {data.assemblyStatus === 'empty' || data.facts.length === 0 ? (
          <Paper sx={{ p: 4, textAlign: 'center', boxShadow: 1 }}>
            <Typography variant="body1" color="text.secondary">
              No extracted data available
            </Typography>
          </Paper>
        ) : categoryFacts.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            No {activeCategory.toLowerCase()} data extracted for this patient.
          </Typography>
        ) : (
          /* Fact grid: minmax 350px per wireframe */
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fill, minmax(350px, 1fr))',
              gap: 2,
            }}
          >
            {categoryFacts.map((fact) => (
              <FactCard key={fact.factId} fact={fact} onCiteClick={handleCiteClick} />
            ))}
          </Box>
        )}
      </Box>

      {/* Source citation drawer */}
      <SourceCitationDrawer
        open={drawerOpen}
        onClose={handleDrawerClose}
        citation={citation}
        isFetching={isCitationFetching}
      />
    </Box>
  );
};

export default PatientView360Page;
