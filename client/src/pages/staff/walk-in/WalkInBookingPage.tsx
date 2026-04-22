// SCR-011 — Walk-In Booking (US_016, AC-2 / AC-3 / AC-4 / AC-5).
// Patient search autocomplete (debounced 300ms), inline patient-create form,
// visit type select, and walk-in booking submission.
// Breadcrumb: Home > Staff Dashboard > Walk-In Booking (UXR-002).
import {
  Alert,
  Autocomplete,
  Box,
  Breadcrumbs,
  Button,
  CircularProgress,
  Collapse,
  Divider,
  FormControl,
  FormHelperText,
  InputLabel,
  Link,
  MenuItem,
  Paper,
  Select,
  Snackbar,
  TextField,
  Typography,
} from '@mui/material';
import { type SyntheticEvent, useEffect, useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router-dom';

import { type CreatePatientRequest, type PatientSearchResult, createPatient } from '@/api/staff';
import { useBookWalkIn } from '@/hooks/useBookWalkIn';
import { usePatientSearch } from '@/hooks/usePatientSearch';

// ── Visit type options (FR-008) ───────────────────────────────────────────────

const VISIT_TYPES = ['General', 'Follow-Up', 'Urgent Care'] as const;
type VisitType = (typeof VISIT_TYPES)[number];

// ── Component ─────────────────────────────────────────────────────────────────

export default function WalkInBookingPage() {
  const navigate = useNavigate();

  // ── Search / autocomplete state ──────────────────────────────────────────
  const [inputValue, setInputValue]         = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [selectedPatient, setSelectedPatient] = useState<PatientSearchResult | null>(null);

  // 300ms debounce on the text input (AC-2)
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedQuery(inputValue), 300);
    return () => clearTimeout(timer);
  }, [inputValue]);

  const { results, isLoading: isSearching } = usePatientSearch(debouncedQuery);

  // ── Inline create-patient form state ────────────────────────────────────
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [createForm, setCreateForm]         = useState<CreatePatientRequest>({
    fullName: '',
    email: '',
    phone: '',
  });
  const [createErrors, setCreateErrors]     = useState({ fullName: '', email: '', phone: '' });
  const [isCreating, setIsCreating]         = useState(false);

  // ── Visit type ───────────────────────────────────────────────────────────
  const [visitType, setVisitType]           = useState<VisitType | ''>('');
  const [visitTypeError, setVisitTypeError] = useState('');

  // ── Snackbar ─────────────────────────────────────────────────────────────
  const [snackbar, setSnackbar] = useState<{
    open: boolean;
    message: string;
    severity: 'success' | 'error' | 'info';
  }>({ open: false, message: '', severity: 'success' });

  const showSnack = (message: string, severity: 'success' | 'error' | 'info') =>
    setSnackbar({ open: true, message, severity });

  // ── Walk-in mutation ─────────────────────────────────────────────────────
  const { mutate: submitBooking, isLoading: isSubmitting } = useBookWalkIn({
    onSuccess: (message, severity) => showSnack(message, severity),
    onError: (detail) => showSnack(detail, 'error'),
  });

  // ── Show "Create new patient" only when search is active but returns nothing ──
  const showCreateOption =
    debouncedQuery.trim().length >= 2 && !isSearching && results.length === 0 && !selectedPatient;

  // ── Inline form validation ───────────────────────────────────────────────

  function validateCreateForm(): boolean {
    const errors = { fullName: '', email: '', phone: '' };
    if (!createForm.fullName.trim()) errors.fullName = 'Full name is required.';
    if (!createForm.email.trim()) {
      errors.email = 'Email is required.';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(createForm.email)) {
      errors.email = 'Enter a valid email address.';
    }
    if (!createForm.phone.trim()) errors.phone = 'Phone number is required.';
    setCreateErrors(errors);
    return !errors.fullName && !errors.email && !errors.phone;
  }

  async function handleCreatePatient() {
    if (!validateCreateForm()) return;
    setIsCreating(true);
    try {
      const patient = await createPatient(createForm);
      setSelectedPatient(patient);
      setShowCreateForm(false);
      setInputValue(patient.fullName);
    } catch {
      showSnack('Failed to create patient. Please try again.', 'error');
    } finally {
      setIsCreating(false);
    }
  }

  // ── Submit walk-in booking ────────────────────────────────────────────────

  function handleSubmit() {
    let valid = true;
    if (!visitType) {
      setVisitTypeError('Visit type is required.');
      valid = false;
    } else {
      setVisitTypeError('');
    }
    if (!selectedPatient || !valid) return;

    submitBooking({ patientId: selectedPatient.id, visitType });
  }

  const submitDisabled = !selectedPatient || !visitType || isSubmitting;

  return (
    <Box sx={{ p: { xs: 2, md: 3 }, maxWidth: 800, mx: 'auto' }}>
      {/* Breadcrumb — UXR-002 */}
      <Breadcrumbs aria-label="breadcrumb" sx={{ mb: 2 }}>
        <Link component={RouterLink} to="/" underline="hover" color="inherit">
          Home
        </Link>
        <Link
          component={RouterLink}
          to="/staff/dashboard"
          underline="hover"
          color="inherit"
        >
          Staff Dashboard
        </Link>
        <Typography color="text.primary">Walk-In Booking</Typography>
      </Breadcrumbs>

      <Typography variant="h4" component="h1" fontWeight={400} sx={{ mb: 3 }}>
        Walk-in booking
      </Typography>

      <Paper elevation={1} sx={{ p: 3, borderRadius: 2 }}>
        {/* ── Patient search (AC-2) ── */}
        <Typography variant="subtitle1" fontWeight={500} gutterBottom>
          Search patient
        </Typography>

        <Autocomplete<PatientSearchResult, false, false, true>
          freeSolo
          options={results}
          getOptionLabel={(opt) =>
            typeof opt === 'string' ? opt : `${opt.fullName} — ${opt.email}`
          }
          filterOptions={(x) => x} // filtering is server-side
          loading={isSearching}
          inputValue={inputValue}
          value={selectedPatient}
          onInputChange={(_e, value, reason) => {
            // "reset" fires after an option is selected — MUI sets the input to
            // getOptionLabel(). We already set inputValue in onChange; skip here
            // to prevent re-triggering a search with the full display label.
            if (reason === 'reset') return;
            setInputValue(value);
            // Clear selection when user edits the field
            if (selectedPatient && value !== selectedPatient.fullName) {
              setSelectedPatient(null);
            }
          }}
          onChange={(_e: SyntheticEvent, value) => {
            if (value && typeof value !== 'string') {
              setSelectedPatient(value);
              setInputValue(value.fullName);
              setShowCreateForm(false);
            }
          }}
          renderInput={(params) => (
            <TextField
              {...params}
              label="Search by email or phone"
              placeholder="e.g. john@example.com or 555-0100"
              InputProps={{
                ...params.InputProps,
                endAdornment: (
                  <>
                    {isSearching ? <CircularProgress size={18} /> : null}
                    {params.InputProps.endAdornment}
                  </>
                ),
              }}
              inputProps={{
                ...params.inputProps,
                'aria-label': 'Search patient by email or phone',
              }}
            />
          )}
          renderOption={(props, option) => (
            <li {...props} key={typeof option === 'string' ? option : option.id}>
              <Box>
                <Typography variant="body2" fontWeight={500}>
                  {typeof option === 'string' ? option : option.fullName}
                </Typography>
                {typeof option !== 'string' && (
                  <Typography variant="caption" color="text.secondary">
                    {option.email} · {option.phone}
                  </Typography>
                )}
              </Box>
            </li>
          )}
          noOptionsText={null}
          sx={{ mb: 2 }}
        />

        {/* ── Selected patient card ── */}
        {selectedPatient && (
          <Box
            sx={{
              p: 2,
              mb: 2,
              backgroundColor: 'success.light',
              borderRadius: 1,
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
            }}
          >
            <Box>
              <Typography variant="body2" fontWeight={500}>
                {selectedPatient.fullName}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {selectedPatient.email} · {selectedPatient.phone}
              </Typography>
            </Box>
            <Button
              size="small"
              onClick={() => {
                setSelectedPatient(null);
                setInputValue('');
              }}
            >
              Change
            </Button>
          </Box>
        )}

        {/* ── "Create new patient" trigger (AC-3) ── */}
        {showCreateOption && !showCreateForm && (
          <Box sx={{ mb: 2 }}>
            <Alert severity="info" sx={{ mb: 1 }}>
              No patient found for "{debouncedQuery}".
            </Alert>
            <Button variant="outlined" onClick={() => setShowCreateForm(true)}>
              Create new patient
            </Button>
          </Box>
        )}

        {/* ── Inline patient creation form (AC-3) ── */}
        <Collapse in={showCreateForm} unmountOnExit>
          <Paper
            variant="outlined"
            sx={{ p: 2, mb: 2, borderRadius: 1 }}
          >
            <Typography variant="subtitle2" fontWeight={500} gutterBottom>
              New patient profile
            </Typography>
            <TextField
              label="Full name"
              required
              fullWidth
              size="small"
              value={createForm.fullName}
              onChange={(e) => setCreateForm((f) => ({ ...f, fullName: e.target.value }))}
              onBlur={() => {
                if (!createForm.fullName.trim())
                  setCreateErrors((e) => ({ ...e, fullName: 'Full name is required.' }));
                else setCreateErrors((e) => ({ ...e, fullName: '' }));
              }}
              error={!!createErrors.fullName}
              helperText={createErrors.fullName}
              sx={{ mb: 2 }}
              inputProps={{ 'aria-label': 'Full name' }}
            />
            <TextField
              label="Email"
              type="email"
              required
              fullWidth
              size="small"
              value={createForm.email}
              onChange={(e) => setCreateForm((f) => ({ ...f, email: e.target.value }))}
              onBlur={() => {
                if (!createForm.email.trim()) {
                  setCreateErrors((e) => ({ ...e, email: 'Email is required.' }));
                } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(createForm.email)) {
                  setCreateErrors((e) => ({ ...e, email: 'Enter a valid email address.' }));
                } else {
                  setCreateErrors((e) => ({ ...e, email: '' }));
                }
              }}
              error={!!createErrors.email}
              helperText={createErrors.email}
              sx={{ mb: 2 }}
              inputProps={{ 'aria-label': 'Email address' }}
            />
            <TextField
              label="Phone"
              type="tel"
              required
              fullWidth
              size="small"
              value={createForm.phone}
              onChange={(e) => setCreateForm((f) => ({ ...f, phone: e.target.value }))}
              onBlur={() => {
                if (!createForm.phone.trim())
                  setCreateErrors((e) => ({ ...e, phone: 'Phone number is required.' }));
                else setCreateErrors((e) => ({ ...e, phone: '' }));
              }}
              error={!!createErrors.phone}
              helperText={createErrors.phone}
              sx={{ mb: 2 }}
              inputProps={{ 'aria-label': 'Phone number' }}
            />
            <Box sx={{ display: 'flex', gap: 1 }}>
              <Button
                variant="contained"
                onClick={() => void handleCreatePatient()}
                disabled={isCreating}
                startIcon={isCreating ? <CircularProgress size={16} /> : null}
              >
                Create patient
              </Button>
              <Button
                variant="text"
                onClick={() => {
                  setShowCreateForm(false);
                  setCreateErrors({ fullName: '', email: '', phone: '' });
                }}
              >
                Cancel
              </Button>
            </Box>
          </Paper>
        </Collapse>

        <Divider sx={{ my: 2 }} />

        {/* ── Visit type select ── */}
        <FormControl
          fullWidth
          required
          error={!!visitTypeError}
          sx={{ mb: 3 }}
        >
          <InputLabel id="visit-type-label">Visit type</InputLabel>
          <Select
            labelId="visit-type-label"
            label="Visit type"
            value={visitType}
            onChange={(e) => {
              setVisitType(e.target.value as VisitType);
              setVisitTypeError('');
            }}
            onBlur={() => {
              if (!visitType) setVisitTypeError('Visit type is required.');
            }}
            inputProps={{ 'aria-label': 'Visit type' }}
          >
            {VISIT_TYPES.map((vt) => (
              <MenuItem key={vt} value={vt}>
                {vt}
              </MenuItem>
            ))}
          </Select>
          {visitTypeError && <FormHelperText>{visitTypeError}</FormHelperText>}
        </FormControl>

        {/* ── Action buttons ── */}
        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
          <Button
            variant="contained"
            onClick={handleSubmit}
            disabled={submitDisabled}
            startIcon={isSubmitting ? <CircularProgress size={16} color="inherit" /> : null}
            sx={{ minWidth: 160 }}
            aria-busy={isSubmitting}
          >
            Book Walk-In
          </Button>
          <Button
            variant="outlined"
            onClick={() => void navigate('/staff/dashboard')}
            disabled={isSubmitting}
          >
            Cancel
          </Button>
        </Box>
      </Paper>

      {/* ── Toast notifications (UXR-402) ── */}
      <Snackbar
        open={snackbar.open}
        autoHideDuration={6000}
        onClose={() => setSnackbar((s) => ({ ...s, open: false }))}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          severity={snackbar.severity}
          onClose={() => setSnackbar((s) => ({ ...s, open: false }))}
        >
          {snackbar.message}
        </Alert>
      </Snackbar>
    </Box>
  );
}
