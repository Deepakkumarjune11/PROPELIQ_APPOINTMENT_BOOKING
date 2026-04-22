import HelpOutlineIcon from '@mui/icons-material/HelpOutline';
import LogoutIcon from '@mui/icons-material/Logout';
import PersonIcon from '@mui/icons-material/Person';
import SettingsIcon from '@mui/icons-material/Settings';
import {
  AppBar,
  Avatar,
  Box,
  IconButton,
  ListItemIcon,
  Menu,
  MenuItem,
  Toolbar,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router-dom';

import { useAuthStore } from '@/stores/auth-store';
import { useOnboardingStore } from '@/stores/onboarding-store';

// SCR-025: Application header — logo + user avatar dropdown (Profile / Settings / Logout)
export default function Header() {
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const navigate = useNavigate();
  const { user, logout } = useAuthStore();
  const { resetOnboarding, startTour } = useOnboardingStore();

  const handleAvatarClick = (e: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(e.currentTarget);
  };

  const handleClose = () => setAnchorEl(null);

  const handleLogout = () => {
    handleClose();
    logout();
    navigate('/login');
  };

  // Derive initials from display name, fall back to 'U'
  const initials = user?.name
    ? user.name
        .split(' ')
        .map((n) => n[0])
        .join('')
        .toUpperCase()
        .slice(0, 2)
    : 'U';

  return (
    // component="header" renders semantic <header> element — implicit role="banner" (WCAG 1.3.1, AC-2)
    <AppBar
      component="header"
      position="fixed"
      color="default"
      elevation={1}
      sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}
    >
      <Toolbar>
        {/* RouterLink renders as <a> — keyboard-accessible, Tab-reachable, Enter navigates (AC-1) */}
        <Typography
          variant="h6"
          component={RouterLink}
          to="/"
          aria-label="PropelIQ Healthcare - go to dashboard"
          color="primary"
          sx={{
            fontWeight: 500,
            fontSize: '1.25rem',
            flexGrow: 0,
            textDecoration: 'none',
            '&:focus-visible': {
              outline: '2px solid #2196F3',
              outlineOffset: '2px',
              borderRadius: '2px',
            },
          }}
        >
          PropelIQ Healthcare
        </Typography>

        <Box sx={{ flexGrow: 1 }} />

        <IconButton
          onClick={handleAvatarClick}
          aria-label="Open user menu"
          aria-controls={anchorEl ? 'user-menu' : undefined}
          aria-haspopup="true"
          aria-expanded={Boolean(anchorEl)}
          size="small"
        >
          <Avatar
            sx={{
              bgcolor: 'primary.main',
              width: 40,
              height: 40,
              fontSize: '1rem',
              fontWeight: 500,
            }}
          >
            {initials}
          </Avatar>
        </IconButton>

        <Menu
          id="user-menu"
          anchorEl={anchorEl}
          open={Boolean(anchorEl)}
          onClose={handleClose}
          anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
          transformOrigin={{ vertical: 'top', horizontal: 'right' }}
          PaperProps={{ sx: { minWidth: 200, mt: 0.5 } }}
        >
          <MenuItem onClick={() => { handleClose(); navigate('/profile'); }}>
            <ListItemIcon>
              <PersonIcon fontSize="small" />
            </ListItemIcon>
            Profile
          </MenuItem>
          <MenuItem onClick={handleClose}>
            <ListItemIcon>
              <SettingsIcon fontSize="small" />
            </ListItemIcon>
            Settings
          </MenuItem>
          <MenuItem
            onClick={() => {
              handleClose();
              resetOnboarding();
              startTour();
            }}
          >
            <ListItemIcon>
              <HelpOutlineIcon fontSize="small" />
            </ListItemIcon>
            Restart Tour
          </MenuItem>
          <MenuItem onClick={handleLogout}>
            <ListItemIcon>
              <LogoutIcon fontSize="small" />
            </ListItemIcon>
            Logout
          </MenuItem>
        </Menu>
      </Toolbar>
    </AppBar>
  );
}
