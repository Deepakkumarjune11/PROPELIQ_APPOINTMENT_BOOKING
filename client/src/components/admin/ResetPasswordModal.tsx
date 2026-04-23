import { useState } from 'react';
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  InputAdornment,
  TextField,
  Typography,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';

import { useResetPassword } from '@/hooks/admin/useResetPassword';

interface Props {
  open: boolean;
  onClose: () => void;
  userId: string;
  userName: string;
}

function validate(v: string) {
  if (!v) return 'New password is required';
  if (v.length < 8) return 'Password must be at least 8 characters';
  if (!/[A-Z]/.test(v)) return 'Password must contain an uppercase letter';
  if (!/[0-9!@#$%^&*]/.test(v)) return 'Password must contain a number or special character';
  return '';
}

export function ResetPasswordModal({ open, onClose, userId, userName }: Props) {
  const [password, setPassword]       = useState('');
  const [error, setError]             = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [apiError, setApiError]       = useState<string | null>(null);

  const { mutate: resetPassword, isPending } = useResetPassword(userId);

  const handleClose = () => {
    if (isPending) return;
    setPassword('');
    setError('');
    setApiError(null);
    setShowPassword(false);
    onClose();
  };

  const handleSubmit = () => {
    const err = validate(password);
    setError(err);
    if (err) return;

    setApiError(null);
    resetPassword(
      { newPassword: password },
      {
        onSuccess: handleClose,
        onError: () => setApiError('Failed to reset password. Please try again.'),
      },
    );
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="xs" fullWidth
      PaperProps={{ sx: { borderRadius: 2 } }}>
      <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', pb: 1 }}>
        <Typography variant="h6" component="span">Reset password</Typography>
        <IconButton onClick={handleClose} size="small" aria-label="Close" sx={{ color: 'text.secondary' }}>
          <CloseIcon />
        </IconButton>
      </DialogTitle>

      <DialogContent dividers sx={{ pt: 2 }}>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Set a new password for <strong>{userName}</strong>. They will need to use this password on their next login.
        </Typography>

        <TextField
          label="New password"
          required
          fullWidth
          autoFocus
          type={showPassword ? 'text' : 'password'}
          value={password}
          onChange={(e) => { setPassword(e.target.value); if (error) setError(validate(e.target.value)); }}
          error={Boolean(error)}
          helperText={error || 'Min 8 chars, 1 uppercase, 1 number or special character'}
          InputProps={{
            endAdornment: (
              <InputAdornment position="end">
                <IconButton
                  size="small"
                  onClick={() => setShowPassword((v) => !v)}
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                  edge="end"
                >
                  {showPassword ? <VisibilityOff fontSize="small" /> : <Visibility fontSize="small" />}
                </IconButton>
              </InputAdornment>
            ),
          }}
        />

        {apiError && (
          <Alert severity="error" sx={{ mt: 2 }}>{apiError}</Alert>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2, gap: 2 }}>
        <Button variant="outlined" onClick={handleClose} disabled={isPending}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={isPending}
          startIcon={isPending ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          Reset password
        </Button>
      </DialogActions>
    </Dialog>
  );
}
