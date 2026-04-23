import BarChartIcon from '@mui/icons-material/BarChart';
import DashboardIcon from '@mui/icons-material/Dashboard';
import FactCheckIcon from '@mui/icons-material/FactCheck';
import GroupIcon from '@mui/icons-material/Group';
import ListIcon from '@mui/icons-material/List';
import ManageAccountsIcon from '@mui/icons-material/ManageAccounts';
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
  Tooltip,
} from '@mui/material';
import { useLocation, useNavigate } from 'react-router-dom';

export const SIDEBAR_WIDTH = 240;
/** Icon-rail width (tablet / 900–1199px). Used by AuthenticatedLayout for margin calc. */
export const ICON_RAIL_WIDTH = 64;

// BRD §6: Staff (front desk/call center) nav items
const STAFF_NAV_ITEMS = [
  { label: 'Dashboard', icon: <DashboardIcon />, path: '/staff/dashboard', navId: undefined },
  { label: 'Walk-in',   icon: <PersonAddIcon />,  path: '/staff/walk-in',  navId: 'nav-book' },
  { label: 'Queue',     icon: <ListIcon />,        path: '/staff/queue',    navId: undefined },
  { label: 'Verify',    icon: <FactCheckIcon />,   path: '/staff/patients', navId: undefined },
  { label: 'Metrics',   icon: <BarChartIcon />,    path: '/metrics',        navId: 'nav-documents' },
] as const;

// BRD §6: Admin (user management) nav items — no clinical tools, no walk-in/queue
const ADMIN_NAV_ITEMS = [
  { label: 'Dashboard', icon: <DashboardIcon />,       path: '/admin/dashboard', navId: undefined },
  { label: 'Users',     icon: <GroupIcon />,            path: '/admin/users',     navId: undefined },
  { label: 'Metrics',   icon: <BarChartIcon />,         path: '/metrics',         navId: undefined },
] as const;

interface SidebarProps {
  /**
   * When `true`, renders a 64px icon-rail variant (tablet breakpoint, 900–1199px).
   * Icons are visible; labels are hidden and exposed via `Tooltip` for pointer/keyboard users.
   */
  iconRail?: boolean;
  /** Current user role — drives which nav item set is rendered (BRD §6 role separation). */
  role?: string;
}

// SCR-025: Persistent sidebar for staff/admin roles.
// Full-width (240px) on desktop (lg+); icon-rail (64px) on tablet (md–lg).
// Admin role gets a separate nav set per BRD §6 (user management focus, no clinical tools).
export default function Sidebar({ iconRail = false, role }: SidebarProps) {
  const navigate = useNavigate();
  const location = useLocation();

  const NAV_ITEMS = role === 'admin' ? ADMIN_NAV_ITEMS : STAFF_NAV_ITEMS;
  const drawerWidth = iconRail ? ICON_RAIL_WIDTH : SIDEBAR_WIDTH;

  return (
    // component="nav" renders a <nav> HTML element — ARIA landmark (WCAG 1.3.6)
    <Drawer
      variant="permanent"
      component="nav"
      aria-label="Main navigation"
      sx={{
        width: drawerWidth,
        flexShrink: 0,
        '& .MuiDrawer-paper': {
          width: drawerWidth,
          boxSizing: 'border-box',
          bgcolor: 'background.default',
          borderRight: '1px solid',
          borderColor: 'divider',
          overflowX: 'hidden',
          transition: 'width 225ms cubic-bezier(0.4, 0, 0.6, 1)',
        },
      }}
    >
      {/* Spacer keeps nav items below the fixed AppBar */}
      <Toolbar />
      <Box sx={{ overflow: 'auto', pt: 1 }}>
        <List disablePadding aria-label="Navigation links">
          {NAV_ITEMS.map(({ label, icon, path, navId }) => {
            const isActive = location.pathname === path;

            const button = (
              <ListItemButton
                id={navId}
                selected={isActive}
                aria-current={isActive ? 'page' : undefined}
                onClick={() => navigate(path)}
                sx={{
                  borderRadius: 1,
                  // WCAG 2.5.5 — 44×44px minimum touch target (AC-4)
                  minHeight: 44,
                  // Center icons in icon-rail mode; normal left-align in full mode
                  justifyContent: iconRail ? 'center' : 'flex-start',
                  px: iconRail ? 0 : 2,
                  '&.Mui-selected': {
                    bgcolor: 'primary.main',
                    color: 'white',
                    '& .MuiListItemIcon-root': { color: 'white' },
                    '&:hover': { bgcolor: 'primary.dark' },
                  },
                }}
              >
                <ListItemIcon
                  sx={{
                    minWidth: iconRail ? 'auto' : 40,
                    color: isActive ? 'white' : 'text.secondary',
                    justifyContent: 'center',
                  }}
                >
                  {icon}
                </ListItemIcon>
                {/* Hide text label in icon-rail mode; exposed via Tooltip instead */}
                <ListItemText
                  primary={label}
                  primaryTypographyProps={{ fontSize: '0.875rem' }}
                  sx={{ display: iconRail ? 'none' : 'block' }}
                />
              </ListItemButton>
            );

            return (
              <ListItem key={label} disablePadding sx={{ px: 1, pb: 0.5 }}>
                {iconRail ? (
                  // Tooltip reveals nav label on hover/focus when text is hidden (AC-2, WCAG 1.1.1)
                  <Tooltip title={label} placement="right" arrow>
                    {button}
                  </Tooltip>
                ) : (
                  button
                )}
              </ListItem>
            );
          })}
        </List>
      </Box>
    </Drawer>
  );
}
