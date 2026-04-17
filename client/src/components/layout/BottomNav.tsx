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
  { label: 'Book', icon: <CalendarTodayIcon />, path: '/book' },
  { label: 'Appointments', icon: <EventIcon />, path: '/appointments' },
  { label: 'Documents', icon: <DescriptionIcon />, path: '/documents' },
  { label: 'Profile', icon: <PersonIcon />, path: '/profile' },
] as const;

const STAFF_ITEMS = [
  { label: 'Dashboard', icon: <DashboardIcon />, path: '/' },
  { label: 'Queue', icon: <ListIcon />, path: '/queue' },
  { label: 'Verify', icon: <FactCheckIcon />, path: '/verify' },
  { label: 'Metrics', icon: <BarChartIcon />, path: '/metrics' },
] as const;

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
      <BottomNavigation
        value={selectedIndex === -1 ? false : selectedIndex}
        onChange={(_, newValue: number) => {
          navigate(items[newValue].path);
        }}
        showLabels
      >
        {items.map(({ label, icon }) => (
          <BottomNavigationAction key={label} label={label} icon={icon} />
        ))}
      </BottomNavigation>
    </Paper>
  );
}
