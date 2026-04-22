import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  CircularProgress,
  FormControlLabel,
  Link,
  TextField,
  Typography,
} from '@mui/material';
import axios from 'axios';
import { useEffect, useRef, useState } from 'react';

import { useLogin } from '@/hooks/useLogin';
import { useAccessibleForm } from '@/hooks/useAccessibleForm';
import { useFormValidation } from '@/hooks/useFormValidation';
import LiveRegion from '@/components/accessibility/LiveRegion';
import { FormErrorSummary } from '@/components/forms/FormErrorSummary';

interface LoginForm {
  email: string;
  password: string;
}

function validateEmail(value: string): string {
  if (!value) return 'Username or email is required';
  return '';
}

function validatePassword(value: string): string {
  if (!value) return 'Password is required';
  return '';
}

// SCR-024: Login page shell — matches Hi-Fi wireframe
// States: Default | Loading (button spinner) | Error (Alert banner) | Validation (field errors)
export default function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(false);

  // Validation hook replaces ad-hoc emailError/passwordError useState pairs (US_039 AC-1, AC-2)
  const { errors, handleBlur, validate, clearError } = useFormValidation<LoginForm>({
    email: validateEmail,
    password: validatePassword,
  });

  // Refs for programmatic focus on validation failure (WCAG 3.3.1, AC-4)
  const emailRef = useRef<HTMLInputElement>(null);
  const passwordRef = useRef<HTMLInputElement>(null);

  const { announcement, focusFirstError, registerField } = useAccessibleForm();

  // Register field refs in DOM order so focusFirstError lands on the topmost error
  useEffect(() => {
    registerField('email', emailRef);
    registerField('password', passwordRef);
  }, [registerField]);

  const { mutate: login, isPending, isError, error } = useLogin();

  // Derive error message from Axios response body or fall back to generic text
  const authErrorMessage = isError
    ? (axios.isAxiosError(error) && (error.response?.data as { message?: string })?.message)
      || 'Invalid email or password. Please try again.'
    : '';

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    // validate() sets all errors and returns false when any field is invalid (AC-2)
    const valid = validate({ email, password });

    // Collect error field IDs in DOM/tab order and announce + focus first error (AC-4)
    const errorFields: string[] = [];
    if (!valid) {
      if (validateEmail(email)) errorFields.push('email');
      if (validatePassword(password)) errorFields.push('password');
    }

    if (errorFields.length > 0) {
      focusFirstError(errorFields);
      return;
    }

    login({ email, password, rememberMe });
  };

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        px: 2,
        bgcolor: 'background.default',
      }}
    >
      {/* Card: max-width 400px, elevation 1, 8px border-radius — per wireframe SCR-024 */}
      <Card elevation={1} sx={{ width: '100%', maxWidth: 400 }}>
        <CardContent sx={{ p: { xs: 3, sm: 6 } }}>
          <Typography
            variant="h4"
            component="h1"
            align="center"
            color="primary"
            sx={{ mb: 4 }}
          >
            PropelIQ Healthcare
          </Typography>

          {/* Error state: Alert appears on failed authentication.
              aria-live="assertive" added for belt-and-suspenders cross-browser AT support
              on top of role="alert" (WCAG 4.1.3, AC-4) */}
          {isError && (
            <Alert severity="error" sx={{ mb: 3 }} role="alert" aria-live="assertive">
              {authErrorMessage}
            </Alert>
          )}

          {/* Assertive live region announces error count on submit failure (AC-4) */}
          <LiveRegion message={announcement} politeness="assertive" />

          {/* Error summary: lists all validation errors as clickable links (US_039 AC-2) */}
          <FormErrorSummary
            errors={errors as Partial<Record<string, string>>}
            fieldIds={{ email: 'login-email', password: 'login-password' }}
          />

          <Box component="form" onSubmit={handleSubmit} noValidate>
            <TextField
              id="login-email"
              label="Username or Email"
              type="text"
              fullWidth
              required
              value={email}
              onChange={(e) => { setEmail(e.target.value); clearError('email'); }}
              onBlur={() => handleBlur('email', email)}
              error={Boolean(errors.email)}
              helperText={errors.email || ' '}
              placeholder="username or name@example.com"
              autoComplete="username"
              inputRef={emailRef}
              sx={{ mb: 1 }}
            />

            <TextField
              id="login-password"
              label="Password"
              type="password"
              fullWidth
              required
              value={password}
              onChange={(e) => { setPassword(e.target.value); clearError('password'); }}
              onBlur={() => handleBlur('password', password)}
              error={Boolean(errors.password)}
              helperText={errors.password || ' '}
              placeholder="Enter your password"
              autoComplete="current-password"
              inputRef={passwordRef}
              sx={{ mb: 1 }}
            />

            <FormControlLabel
              control={
                <Checkbox
                  checked={rememberMe}
                  onChange={(e) => setRememberMe(e.target.checked)}
                  color="primary"
                />
              }
              label="Remember me"
              sx={{ mb: 3 }}
            />

            {/* Loading state: CircularProgress replaces button label */}
            <Button
              type="submit"
              variant="contained"
              fullWidth
              disabled={isPending}
              aria-label={isPending ? 'Logging in' : 'Login'}
              sx={{ mb: 2, py: 1.5 }}
            >
              {isPending ? <CircularProgress size={20} color="inherit" /> : 'Login'}
            </Button>

            <Box textAlign="center">
              <Link
                href="#"
                onClick={(e) => e.preventDefault()}
                variant="body2"
                underline="hover"
              >
                Forgot password?
              </Link>
            </Box>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}
