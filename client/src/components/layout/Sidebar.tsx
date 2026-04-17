import BarChartIcon from '@mui/icons-material/BarChart';
import DashboardIcon from '@mui/icons-material/Dashboard';
import FactCheckIcon from '@mui/icons-material/FactCheck';
import ListIcon from '@mui/icons-material/List';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import {
  Box,
  Drawer,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
} from '@mui/material';
import { useLocation, useNavigate } from 'react-router-dom';

export const SIDEBAR_WIDTH = 240;

const NAV_ITEMS = [
  { label: 'Dashboard', icon: <DashboardIcon />, path: '/' },
  { label: 'Walk-in', icon: <PersonAddIcon />, path: '/walkin' },
  { label: 'Queue', icon: <ListIcon />, path: '/queue' },
  { label: 'Verify', icon: <FactCheckIcon />, path: '/verify' },
  { label: 'Metrics', icon: <BarChartIcon />, path: '/metrics' },
] as const;

// SCR-025: Desktop-only persistent sidebar for staff/admin roles
export default function Sidebar() {
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <Drawer
      variant="permanent"
      sx={{
        width: SIDEBAR_WIDTH,
        flexShrink: 0,
        '& .MuiDrawer-paper': {
          width: SIDEBAR_WIDTH,
          boxSizing: 'border-box',
          bgcolor: 'background.default',
          borderRight: '1px solid',
          borderColor: 'divider',
        },
      }}
    >
      {/* Spacer keeps nav items below the fixed AppBar */}
      <Toolbar />
      <Box sx={{ overflow: 'auto', pt: 1 }}>
        <List disablePadding>
          {NAV_ITEMS.map(({ label, icon, path }) => {
            const isActive = location.pathname === path;
            return (
              <ListItem key={label} disablePadding sx={{ px: 1, pb: 0.5 }}>
                <ListItemButton
                  selected={isActive}
                  onClick={() => navigate(path)}
                  sx={{
                    borderRadius: 1,
                    '&.Mui-selected': {
                      bgcolor: 'primary.main',
                      color: 'white',
                      '& .MuiListItemIcon-root': { color: 'white' },
                      '&:hover': { bgcolor: 'primary.dark' },
                    },
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 40, color: isActive ? 'white' : 'text.secondary' }}>
                    {icon}
                  </ListItemIcon>
                  <ListItemText
                    primary={label}
                    primaryTypographyProps={{ fontSize: '0.875rem' }}
                  />
                </ListItemButton>
              </ListItem>
            );
          })}
        </List>
      </Box>
    </Drawer>
  );
}
