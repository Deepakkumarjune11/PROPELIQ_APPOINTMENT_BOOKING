import BarChartIcon from '@mui/icons-material/BarChart';
import CalendarTodayIcon from '@mui/icons-material/CalendarToday';
import DashboardIcon from '@mui/icons-material/Dashboard';
import DescriptionIcon from '@mui/icons-material/Description';
import EventIcon from '@mui/icons-material/Event';
import FactCheckIcon from '@mui/icons-material/FactCheck';
import ListIcon from '@mui/icons-material/List';
import PersonIcon from '@mui/icons-material/Person';
import { BottomNavigation, BottomNavigationAction, Paper } from '@mui/material';
import { useLocation, useNavigate } from 'react-router-dom';

import { useAuthStore } from '@/stores/auth-store';

const PATIENT_ITEMS = [
  { label: 'Book',         icon: <CalendarTodayIcon />, path: '/appointments/search', navId: 'nav-book-mobile' as string | undefined },
  { label: 'Appointments', icon: <EventIcon />,         path: '/appointments',        navId: undefined as string | undefined },
  { label: 'Documents',    icon: <DescriptionIcon />,   path: '/documents',           navId: 'nav-documents-mobile' as string | undefined },
  { label: 'Profile',      icon: <PersonIcon />,        path: '/profile',      navId: 'nav-profile-mobile' as string | undefined },
];

const STAFF_ITEMS = [
  { label: 'Dashboard', icon: <DashboardIcon />, path: '/staff/dashboard', navId: undefined as string | undefined },
  { label: 'Queue',     icon: <ListIcon />,      path: '/staff/queue',    navId: undefined as string | undefined },
  { label: 'Verify',   icon: <FactCheckIcon />, path: '/staff/patients', navId: undefined as string | undefined },
  { label: 'Metrics',  icon: <BarChartIcon />,  path: '/metrics',        navId: undefined as string | undefined },
];

// SCR-025: Mobile bottom navigation (<900px breakpoint) — role-aware tab set
export default function BottomNav() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user } = useAuthStore();

  const items = user?.role === 'patient' ? PATIENT_ITEMS : STAFF_ITEMS;
  const selectedIndex = items.findIndex((item) => item.path === location.pathname);

  return (
    <Paper
      elevation={3}
      sx={{ position: 'fixed', bottom: 0, left: 0, right: 0, zIndex: 'appBar' }}
    >
      {/* component="nav" renders semantic <nav> landmark (WCAG 1.3.1, AC-2)
          aria-label distinguishes this nav from the sidebar nav (WCAG 2.4.6)
          touchAction: 'manipulation' removes 300ms tap delay on mobile (AC-4) */}
      <BottomNavigation
        value={selectedIndex === -1 ? false : selectedIndex}
        onChange={(_, newValue: number) => {
          navigate(items[newValue].path);
        }}
        showLabels
        component="nav"
        aria-label="Mobile navigation"
        sx={{ touchAction: 'manipulation' }}
      >
        {items.map(({ label, icon, navId }) => (
          // sx minWidth enforces WCAG 2.5.5 44×44px touch target on each action (AC-4)
          <BottomNavigationAction key={label} id={navId} label={label} icon={icon} sx={{ minWidth: 44 }} />
        ))}
      </BottomNavigation>
    </Paper>
  );
}
