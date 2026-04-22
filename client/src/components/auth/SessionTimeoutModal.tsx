import {
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Typography,
} from '@mui/material';
import { useMutation } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';

import api from '@/lib/api';
import { useAuthStore } from '@/stores/auth-store';

interface Props {
  open: boolean;
  countdown: number;
  onClose: () => void;
}

interface RefreshResponse {
  token: string;
  expiresAt: number;
}

/**
 * Session expiry warning dialog (US_024, AC-4, UXR-504).
 *
 * Appears 1 minute before the 15-minute idle timeout fires.
 * "Stay Logged In" calls `POST /api/v1/auth/refresh`; on success resets the session.
 * "Logout" button and a failed refresh both trigger immediate logout.
 *
 * `disableEscapeKeyDown` prevents accidental dismissal without an explicit choice.
 */
export function SessionTimeoutModal({ open, countdown, onClose }: Props) {
  const { logout, user } = useAuthStore();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  const { mutate: refresh, isPending } = useMutation<RefreshResponse, Error>({
    mutationFn: () =>
      api.post<RefreshResponse>('/api/v1/auth/refresh').then((r) => r.data),

    onSuccess: (data) => {
      // Preserve existing user profile; update token + expiry
      useAuthStore.getState().setAuth(user!, data.token, data.expiresAt);
      onClose();
    },

    // Refresh 401 → force logout per NFR-005 edge case
    onError: handleLogout,
  });

  return (
    <Dialog
      open={open}
      onClose={() => {}}       // intentionally no-op — user must choose explicitly
      disableEscapeKeyDown
      maxWidth="xs"
      fullWidth
      aria-labelledby="session-timeout-title"
      aria-describedby="session-timeout-description"
    >
      <DialogTitle id="session-timeout-title">Session Expiring Soon</DialogTitle>

      <DialogContent>
        <Typography id="session-timeout-description">
          Your session will expire in 1 minute. Stay logged in?
        </Typography>
        <Box sx={{ mt: 2 }}>
          <Chip
            label={`${countdown}s`}
            color="warning"
            aria-label={`Session expires in ${countdown} seconds`}
          />
        </Box>
      </DialogContent>

      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button
          variant="outlined"
          color="inherit"
          onClick={handleLogout}
          aria-label="Logout now"
        >
          Logout
        </Button>
        <Button
          variant="contained"
          onClick={() => refresh()}
          disabled={isPending}
          aria-label="Stay logged in"
        >
          Stay Logged In
        </Button>
      </DialogActions>
    </Dialog>
  );
}
