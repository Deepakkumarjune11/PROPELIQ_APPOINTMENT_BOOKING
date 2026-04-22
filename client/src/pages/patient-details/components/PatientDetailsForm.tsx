// SCR-003: Controlled patient details form — 5 TextFields + 1 date input + 1 Select.
// All required fields validate inline on blur (UXR-502).
// Submit is disabled while any required validation error is present or mutation is pending.
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import FormControl from '@mui/material/FormControl';
import FormHelperText from '@mui/material/FormHelperText';
import InputLabel from '@mui/material/InputLabel';
import MenuItem from '@mui/material/MenuItem';
import Select from '@mui/material/Select';
import TextField from '@mui/material/TextField';
import { useState } from 'react';

import type { PatientRegistrationRequest } from '@/api/registration';

// Phone regex: accepts 555-0123, (555) 123-4567, +1-555-0123, etc.
const PHONE_REGEX = /^[+]?[(]?[0-9]{3}[)]?[-\s.]?[0-9]{3}[-\s.]?[0-9]{4,6}$/;
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const INSURANCE_PROVIDERS = [
  { value: 'Blue Cross', label: 'Blue Cross' },
  { value: 'Aetna', label: 'Aetna' },
  { value: 'Cigna', label: 'Cigna' },
  { value: 'UnitedHealth', label: 'UnitedHealth' },
  { value: 'Other', label: 'Other' },
] as const;

interface PatientDetailsFormProps {
  isLoading: boolean;
  /** Inline email-conflict message surfaced from 409 API responses. */
  emailConflictError: string | null;
  onSubmit: (payload: PatientRegistrationRequest) => void;
  onBack: () => void;
}

interface FormErrors {
  email?: string;
  name?: string;
  dob?: string;
  phone?: string;
  insuranceProvider?: string;
}

function getTodayISO(): string {
  return new Date().toISOString().split('T')[0];
}

export default function PatientDetailsForm({
  isLoading,
  emailConflictError,
  onSubmit,
  onBack,
}: PatientDetailsFormProps) {
  const [email, setEmail] = useState('');
  const [name, setName] = useState('');
  const [dob, setDob] = useState('');
  const [phone, setPhone] = useState('');
  const [insuranceProvider, setInsuranceProvider] = useState('');
  const [insuranceMemberId, setInsuranceMemberId] = useState('');
  const [errors, setErrors] = useState<FormErrors>({});
  const [touched, setTouched] = useState<Record<string, boolean>>({});

  // ── Validators (called on blur and before submit) ──────────────────────────

  function validateEmail(value: string): string | undefined {
    if (!value) return 'Email address is required.';
    if (!EMAIL_REGEX.test(value)) return 'Please enter a valid email address.';
    return undefined;
  }

  function validateName(value: string): string | undefined {
    if (!value.trim()) return 'Full name is required.';
    if (value.trim().length > 200) return 'Name must be 200 characters or fewer.';
    return undefined;
  }

  function validateDob(value: string): string | undefined {
    if (!value) return 'Date of birth is required.';
    const parsed = new Date(value);
    if (isNaN(parsed.getTime())) return 'Please enter a valid date.';
    if (parsed >= new Date()) return 'Date of birth must be in the past.';
    return undefined;
  }

  function validatePhone(value: string): string | undefined {
    if (!value) return 'Phone number is required.';
    if (!PHONE_REGEX.test(value.trim()))
      return 'Use format: 555-0123 or +1-555-0123';
    return undefined;
  }

  function validateInsuranceProvider(value: string): string | undefined {
    if (!value) return 'Insurance provider is required.';
    return undefined;
  }

  function validateAll(): FormErrors {
    return {
      email: validateEmail(email),
      name: validateName(name),
      dob: validateDob(dob),
      phone: validatePhone(phone),
      insuranceProvider: validateInsuranceProvider(insuranceProvider),
    };
  }

  function isFormValid(errs: FormErrors): boolean {
    return Object.values(errs).every((e) => !e);
  }

  // ── Blur handlers ─────────────────────────────────────────────────────────

  const handleBlur = (field: keyof FormErrors, validatorFn: () => string | undefined) => {
    setTouched((prev) => ({ ...prev, [field]: true }));
    const error = validatorFn();
    setErrors((prev) => ({ ...prev, [field]: error }));
  };

  // ── Submit ────────────────────────────────────────────────────────────────

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    // Touch all fields so errors are visible
    setTouched({ email: true, name: true, dob: true, phone: true, insuranceProvider: true });
    const errs = validateAll();
    setErrors(errs);
    if (!isFormValid(errs)) return;

    onSubmit({ email, name, dob, phone, insuranceProvider, insuranceMemberId });
  };

  const currentErrors = validateAll();
  const submitDisabled = isLoading || !isFormValid(currentErrors);

  // Use API-level conflict error over local email validation error when present
  const emailDisplayError =
    touched.email ? (emailConflictError ?? errors.email) : emailConflictError ?? undefined;

  return (
    <Box component="form" onSubmit={handleSubmit} noValidate sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      {/* Email */}
      <TextField
        id="email"
        label="Email address"
        type="email"
        required
        fullWidth
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        onBlur={() => handleBlur('email', () => validateEmail(email))}
        error={Boolean(emailDisplayError)}
        helperText={emailDisplayError ?? "We'll use this for appointment reminders"}
        inputProps={{ 'aria-required': true }}
        disabled={isLoading}
      />

      {/* Full name */}
      <TextField
        id="full-name"
        label="Full name"
        required
        fullWidth
        value={name}
        onChange={(e) => setName(e.target.value)}
        onBlur={() => handleBlur('name', () => validateName(name))}
        error={touched.name && Boolean(errors.name)}
        helperText={touched.name ? errors.name : undefined}
        placeholder="John Doe"
        inputProps={{ 'aria-required': true, maxLength: 200 }}
        disabled={isLoading}
      />

      {/* Date of birth */}
      <TextField
        id="dob"
        label="Date of birth"
        type="date"
        required
        fullWidth
        value={dob}
        onChange={(e) => setDob(e.target.value)}
        onBlur={() => handleBlur('dob', () => validateDob(dob))}
        error={touched.dob && Boolean(errors.dob)}
        helperText={touched.dob ? errors.dob : undefined}
        inputProps={{ 'aria-required': true, max: getTodayISO() }}
        InputLabelProps={{ shrink: true }}
        disabled={isLoading}
      />

      {/* Phone */}
      <TextField
        id="phone"
        label="Phone number"
        type="tel"
        required
        fullWidth
        value={phone}
        onChange={(e) => setPhone(e.target.value)}
        onBlur={() => handleBlur('phone', () => validatePhone(phone))}
        error={touched.phone && Boolean(errors.phone)}
        helperText={touched.phone ? errors.phone : undefined}
        placeholder="(555) 123-4567"
        inputProps={{ 'aria-required': true }}
        disabled={isLoading}
      />

      {/* Insurance provider */}
      <FormControl
        fullWidth
        required
        error={touched.insuranceProvider && Boolean(errors.insuranceProvider)}
        disabled={isLoading}
      >
        <InputLabel id="insurance-provider-label">Insurance provider</InputLabel>
        <Select
          labelId="insurance-provider-label"
          id="insurance-provider"
          value={insuranceProvider}
          label="Insurance provider"
          onChange={(e) => setInsuranceProvider(e.target.value)}
          onBlur={() =>
            handleBlur('insuranceProvider', () => validateInsuranceProvider(insuranceProvider))
          }
          inputProps={{ 'aria-required': true }}
        >
          {INSURANCE_PROVIDERS.map((opt) => (
            <MenuItem key={opt.value} value={opt.value}>
              {opt.label}
            </MenuItem>
          ))}
        </Select>
        {touched.insuranceProvider && errors.insuranceProvider && (
          <FormHelperText>{errors.insuranceProvider}</FormHelperText>
        )}
      </FormControl>

      {/* Insurance member ID (optional) */}
      <TextField
        id="insurance-member-id"
        label="Insurance member ID"
        fullWidth
        value={insuranceMemberId}
        onChange={(e) => setInsuranceMemberId(e.target.value)}
        helperText="Found on your insurance card"
        placeholder="ABC123456789"
        inputProps={{ maxLength: 100 }}
        disabled={isLoading}
      />

      {/* Actions — min 44px touch targets (UXR-102) */}
      <Box sx={{ display: 'flex', gap: 2, mt: 1 }}>
        <Button
          variant="outlined"
          onClick={onBack}
          disabled={isLoading}
          sx={{ flex: 1, minHeight: 44 }}
        >
          Back
        </Button>
        <Button
          type="submit"
          variant="contained"
          disabled={submitDisabled}
          sx={{ flex: 1, minHeight: 44 }}
          startIcon={isLoading ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {isLoading ? 'Submitting…' : 'Continue to intake'}
        </Button>
      </Box>
    </Box>
  );
}
