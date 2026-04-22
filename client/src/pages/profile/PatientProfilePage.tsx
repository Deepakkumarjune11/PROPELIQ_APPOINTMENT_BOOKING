// SCR-P01: Patient Profile page — displays logged-in patient's account details.
// Read-only in this iteration; shows name, email, role from the auth store.
import AccountCircleIcon from '@mui/icons-material/AccountCircle';
import EmailIcon from '@mui/icons-material/Email';
import LogoutIcon from '@mui/icons-material/Logout';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Divider from '@mui/material/Divider';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { useNavigate } from 'react-router-dom';

import { useAuthStore } from '@/stores/auth-store';

export default function PatientProfilePage() {
  const { user, logout } = useAuthStore();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  if (!user) return null;

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        My Profile
      </Typography>

      <Paper variant="outlined" sx={{ borderRadius: 2, overflow: 'hidden' }}>
        {/* Avatar header */}
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            gap: 2,
            p: 3,
            bgcolor: 'primary.main',
            color: 'primary.contrastText',
          }}
        >
          <AccountCircleIcon sx={{ fontSize: 56 }} aria-hidden="true" />
          <Box>
            <Typography variant="h6" fontWeight={600}>
              {user.name}
            </Typography>
            <Typography variant="body2" sx={{ opacity: 0.85, textTransform: 'capitalize' }}>
              {user.role}
            </Typography>
          </Box>
        </Box>

        <Divider />

        {/* Detail rows */}
        <Stack spacing={0} divider={<Divider />}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, px: 3, py: 2 }}>
            <EmailIcon color="action" fontSize="small" aria-hidden="true" />
            <Box>
              <Typography variant="caption" color="text.secondary">
                Email
              </Typography>
              <Typography variant="body2">{user.email}</Typography>
            </Box>
          </Box>
        </Stack>

        <Divider />

        {/* Actions */}
        <Box sx={{ p: 2, display: 'flex', justifyContent: 'flex-end' }}>
          <Button
            variant="outlined"
            color="error"
            startIcon={<LogoutIcon />}
            onClick={handleLogout}
            aria-label="Log out of your account"
            sx={{ minHeight: 44 }}
          >
            Log out
          </Button>
        </Box>
      </Paper>
    </Container>
  );
}
