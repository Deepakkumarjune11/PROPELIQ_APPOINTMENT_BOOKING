// Shown when the backend returns fallbackToManual: true (circuit-breaker fired — AC-5 / AIR-O02).
// Automatically navigates to the manual form after 2 seconds.
// The navigate call is intentionally handled by the parent (useIntakeChat hook) so this component
// is purely presentational and does not need its own timer.
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import { useNavigate } from 'react-router-dom';

export default function IntakeFallbackBanner() {
  const navigate = useNavigate();

  return (
    <Box sx={{ px: { xs: 2, sm: 3 }, py: 1.5 }}>
      <Alert
        severity="warning"
        icon={<WarningAmberIcon />}
        action={
          <Button
            color="inherit"
            size="small"
            sx={{ minHeight: 44, whiteSpace: 'nowrap' }}
            onClick={() => navigate('/appointments/intake/manual')}
          >
            Switch now
          </Button>
        }
      >
        AI assistant is temporarily unavailable. Switching you to the manual form…
      </Alert>
    </Box>
  );
}
