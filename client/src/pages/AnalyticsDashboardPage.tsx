// SCR-028 — Analytics Dashboard (US_033, EP-008).
// Accessible to Staff and Admin roles. Sections: KPI Cards, Trend Charts, System Health.
// Screen states: Default, Loading (Skeleton), Empty (no data), Error (Alert + Retry).
// Breadcrumb: Admin Dashboard > Analytics (admin) | Staff Dashboard > Analytics (staff) — UXR-002.
import {
  Alert,
  Box,
  Breadcrumbs,
  Button,
  Chip,
  Divider,
  Grid,
  Link,
  Paper,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';

import { useAuthStore } from '@/stores/auth-store';

import DateRangeFilter from '@/components/analytics/DateRangeFilter';
import ExportButton from '@/components/analytics/ExportButton';
import MetricKpiCard from '@/components/analytics/MetricKpiCard';
import SystemHealthPanel from '@/components/analytics/SystemHealthPanel';
import TrendChartsPanel from '@/components/analytics/TrendChartsPanel';
import {
  useKpiMetrics,
  useSystemHealth,
  useTrendData,
} from '@/hooks/useAnalyticsData';
import type { DateRange } from '@/types/analytics';

function buildDefaultRange(): DateRange {
  const end = new Date();
  const start = new Date();
  start.setDate(start.getDate() - 30);
  return { startDate: start, endDate: end };
}

export default function AnalyticsDashboardPage() {
  const { user } = useAuthStore();
  const isAdmin = user?.role === 'admin';

  const [dateRange, setDateRange] = useState<DateRange>(buildDefaultRange);

  const kpiQuery = useKpiMetrics(dateRange);
  const trendQuery = useTrendData(dateRange);
  const healthQuery = useSystemHealth();

  const handleRangeChange = (startDate: Date, endDate: Date) => {
    setDateRange({ startDate, endDate });
  };

  const handleRetry = () => {
    void kpiQuery.refetch();
    void trendQuery.refetch();
    void healthQuery.refetch();
  };

  const isAnyError = kpiQuery.isError || trendQuery.isError || healthQuery.isError;

  const kpiData = kpiQuery.data;
  const isDataStale = (kpiData?.dataFreshnessSec ?? 0) > 60;

  const isEmpty =
    !kpiQuery.isLoading &&
    !trendQuery.isLoading &&
    !isAnyError &&
    kpiData?.appointmentCount === 0 &&
    (trendQuery.data?.dailyVolumes.length ?? 0) === 0;

  return (
    <Box>
      {/* Breadcrumb — UXR-002: role-aware parent link */}
      <Breadcrumbs aria-label="breadcrumb" sx={{ mb: 2 }}>
        <Link
          component={RouterLink}
          to={isAdmin ? '/admin/dashboard' : '/staff/dashboard'}
          underline="hover"
          color="inherit"
        >
          {isAdmin ? 'Admin Dashboard' : 'Staff Dashboard'}
        </Link>
        <Typography color="text.primary">Analytics</Typography>
      </Breadcrumbs>

      {/* Page header */}
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: 2,
          mb: 3,
        }}
      >
        <Typography variant="h5" component="h1" fontWeight={600}>
          Operational Metrics Dashboard
        </Typography>
        <ExportButton range={dateRange} />
      </Box>

      {/* Date range filter */}
      <Paper elevation={0} variant="outlined" sx={{ p: 2, mb: 3, borderRadius: 2 }}>
        <DateRangeFilter range={dateRange} onRangeChange={handleRangeChange} />
      </Paper>

      {/* Data freshness warning */}
      {isDataStale && !kpiQuery.isLoading && (
        <Box sx={{ mb: 2 }}>
          <Chip
            label={`Data delayed — last refreshed: ${kpiData?.lastRefreshedAt ?? 'unknown'}`}
            color="warning"
            size="small"
          />
        </Box>
      )}

      {/* Error state */}
      {isAnyError && (
        <Alert
          severity="error"
          sx={{ mb: 3 }}
          action={
            <Button color="inherit" size="small" onClick={handleRetry}>
              Retry
            </Button>
          }
        >
          Unable to load metrics. Please try again.
        </Alert>
      )}

      {/* Empty state */}
      {isEmpty && (
        <Alert severity="info" sx={{ mb: 3 }}>
          No metrics available for the selected date range. Try adjusting the filters.
        </Alert>
      )}

      {/* KPI Cards row (AC-1) */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid item xs={12} sm={6} md={3}>
          <MetricKpiCard
            title="Today's Appointments"
            value={kpiData?.appointmentCount ?? 0}
            isLoading={kpiQuery.isLoading}
          />
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <MetricKpiCard
            title="No-show Rate"
            value={kpiData?.noShowRate ?? 0}
            unit="%"
            isLoading={kpiQuery.isLoading}
          />
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <MetricKpiCard
            title="Avg Wait Time"
            value={kpiData?.avgWaitTimeMin ?? 0}
            unit="min"
            isLoading={kpiQuery.isLoading}
          />
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <MetricKpiCard
            title="AI Acceptance Rate"
            value={kpiData?.aiAcceptanceRate ?? 0}
            unit="%"
            isLoading={kpiQuery.isLoading}
          />
        </Grid>
      </Grid>

      <Divider sx={{ mb: 3 }} />

      {/* Trend Charts (AC-3) + System Health (AC-4) — side by side on desktop */}
      <Grid container spacing={3}>
        <Grid item xs={12} lg={8}>
          <Paper elevation={1} sx={{ p: 3, borderRadius: 2 }}>
            <TrendChartsPanel data={trendQuery.data} isLoading={trendQuery.isLoading} />
          </Paper>
        </Grid>
        <Grid item xs={12} lg={4}>
          <Paper elevation={1} sx={{ p: 3, borderRadius: 2 }}>
            <SystemHealthPanel data={healthQuery.data} isLoading={healthQuery.isLoading} />
          </Paper>
        </Grid>
      </Grid>
    </Box>
  );
}
