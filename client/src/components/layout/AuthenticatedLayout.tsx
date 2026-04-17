import { Box, Toolbar, useMediaQuery } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { Navigate, Outlet } from 'react-router-dom';


import BottomNav from './BottomNav';
import Header from './Header';
import Sidebar from './Sidebar';
import { SIDEBAR_WIDTH } from './Sidebar';

import { useAuthStore } from '@/stores/auth-store';

// SCR-025: Authenticated layout shell — Header + (Sidebar desktop | BottomNav mobile) + content
// UXR-203: Adaptive navigation — bottom nav on mobile (<md), sidebar on desktop (>=md) for staff/admin
export default function AuthenticatedLayout() {
  const theme = useTheme();
  const isDesktop = useMediaQuery(theme.breakpoints.up('md'));
  const { isAuthenticated, user } = useAuthStore();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  const isStaffOrAdmin = user?.role === 'staff' || user?.role === 'admin';
  const showSidebar = isDesktop && isStaffOrAdmin;
  const showBottomNav = !isDesktop;

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <Header />

      {showSidebar && <Sidebar />}

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          p: 3,
          // Extra bottom padding prevents last content element hiding behind BottomNav
          pb: showBottomNav ? 9 : 3,
          // Width constraint when sidebar is not shown ensures content fills space
          width: showSidebar ? `calc(100% - ${SIDEBAR_WIDTH}px)` : '100%',
        }}
      >
        {/* Spacer element equals AppBar height — required because AppBar is position:fixed */}
        <Toolbar />
        <Outlet />
      </Box>

      {showBottomNav && <BottomNav />}
    </Box>
  );
}
