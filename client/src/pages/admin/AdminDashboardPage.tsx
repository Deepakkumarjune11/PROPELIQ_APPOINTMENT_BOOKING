// SCR-A01 — Admin Dashboard (US_025, BRD §6 Admin role).
// Shows platform-wide summary cards (total users, active staff, patients) with quick links.
import GroupIcon from '@mui/icons-material/Group';
import ManageAccountsIcon from '@mui/icons-material/ManageAccounts';
import BarChartIcon from '@mui/icons-material/BarChart';
import { Box, Breadcrumbs, Button, Grid, Link, Typography } from '@mui/material';
import { Link as RouterLink, useNavigate } from 'react-router-dom';

import { useAdminUsers } from '@/hooks/admin/useAdminUsers';
import DashboardSummaryCard from '@/pages/staff/dashboard/DashboardSummaryCard';

export default function AdminDashboardPage() {
  const navigate = useNavigate();
  const { data: users, isLoading } = useAdminUsers();

  const totalUsers   = users?.length;
  const totalStaff   = users?.filter((u) => u.role === 'Staff').length;
  const totalPatients = users?.filter((u) => u.role === 'Patient').length;
  const activeUsers  = users?.filter((u) => u.isActive).length;

  return (
    <Box sx={{ p: { xs: 2, md: 3 } }}>
      <Breadcrumbs aria-label="breadcrumb" sx={{ mb: 2 }}>
        <Link component={RouterLink} to="/" underline="hover" color="inherit">
          Home
        </Link>
        <Typography color="text.primary">Admin Dashboard</Typography>
      </Breadcrumbs>

      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 3 }}>
        <ManageAccountsIcon color="primary" sx={{ fontSize: 28 }} />
        <Typography variant="h5" fontWeight={600}>
          Admin Dashboard
        </Typography>
      </Box>

      {/* Summary cards */}
      <Grid container spacing={2} sx={{ mb: 4 }}>
        <Grid item xs={12} sm={6} md={3}>
          <DashboardSummaryCard
            title="Total Users"
            value={totalUsers}
            subtitle="All roles"
            isLoading={isLoading}
          />
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <DashboardSummaryCard
            title="Active Staff"
            value={totalStaff}
            subtitle="Front desk / clinical"
            isLoading={isLoading}
          />
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <DashboardSummaryCard
            title="Patients"
            value={totalPatients}
            subtitle="Registered patients"
            isLoading={isLoading}
          />
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <DashboardSummaryCard
            title="Active Accounts"
            value={activeUsers}
            subtitle="Not disabled"
            badge={activeUsers !== undefined && totalUsers !== undefined && activeUsers < totalUsers
              ? `${totalUsers - activeUsers} disabled`
              : undefined}
            badgeColor="warning"
            isLoading={isLoading}
          />
        </Grid>
      </Grid>

      {/* Quick action buttons */}
      <Typography variant="subtitle1" fontWeight={600} sx={{ mb: 2 }}>
        Quick Actions
      </Typography>
      <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
        <Button
          variant="contained"
          startIcon={<GroupIcon />}
          onClick={() => void navigate('/admin/users')}
        >
          Manage Users
        </Button>
        <Button
          variant="outlined"
          startIcon={<BarChartIcon />}
          onClick={() => void navigate('/metrics')}
        >
          View Analytics
        </Button>
      </Box>
    </Box>
  );
}
