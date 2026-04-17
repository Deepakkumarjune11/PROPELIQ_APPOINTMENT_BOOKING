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
import { useState } from 'react';

function validateEmail(value: string): string {
  if (!value) return 'Please enter a valid email address';
  if (!value.includes('@') || !value.includes('.')) return 'Please enter a valid email address';
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
  const [emailError, setEmailError] = useState('');
  const [passwordError, setPasswordError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [authError, setAuthError] = useState('');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    const emailErr = validateEmail(email);
    const passErr = validatePassword(password);
    setEmailError(emailErr);
    setPasswordError(passErr);

    if (emailErr || passErr) return;

    setAuthError('');
    setIsLoading(true);

    // Placeholder: actual auth will be wired in a later authentication task
    setTimeout(() => {
      setIsLoading(false);
      setAuthError('Invalid email or password. Please try again.');
    }, 1500);
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

          {/* Error state: Alert appears on failed authentication */}
          {authError && (
            <Alert severity="error" sx={{ mb: 3 }} role="alert">
              {authError}
            </Alert>
          )}

          <Box component="form" onSubmit={handleSubmit} noValidate>
            <TextField
              id="email"
              label="Email"
              type="email"
              fullWidth
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              onBlur={() => setEmailError(validateEmail(email))}
              error={Boolean(emailError)}
              helperText={emailError || ' '}
              placeholder="name@example.com"
              autoComplete="email"
              inputProps={{ 'aria-describedby': 'email-error' }}
              FormHelperTextProps={{ id: 'email-error' }}
              sx={{ mb: 1 }}
            />

            <TextField
              id="password"
              label="Password"
              type="password"
              fullWidth
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              onBlur={() => setPasswordError(validatePassword(password))}
              error={Boolean(passwordError)}
              helperText={passwordError || ' '}
              placeholder="Enter your password"
              autoComplete="current-password"
              inputProps={{ 'aria-describedby': 'password-error' }}
              FormHelperTextProps={{ id: 'password-error' }}
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
              disabled={isLoading}
              aria-label={isLoading ? 'Logging in' : 'Login'}
              sx={{ mb: 2, py: 1.5 }}
            >
              {isLoading ? <CircularProgress size={20} color="inherit" /> : 'Login'}
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
