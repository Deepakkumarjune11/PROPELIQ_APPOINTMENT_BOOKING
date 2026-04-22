// Trend charts panel for analytics dashboard (US_033, AC-3, SCR-028).
// Charts: BarChart (daily volumes) + LineChart dual-axis (no-show + AI latency) + PieChart (doc status).
import { Box, Skeleton, Typography } from '@mui/material';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import type { TrendData } from '@/types/analytics';

// Color palette aligned with designsystem.md
const CHART_COLORS = {
  primary: '#2196F3',   // primary.500
  warning: '#FF9800',   // warning.main
  success: '#4CAF50',   // success.main
  error: '#F44336',     // error.main
  secondary: '#9C27B0', // secondary.500
} as const;

const PIE_COLORS = [CHART_COLORS.success, CHART_COLORS.warning, CHART_COLORS.error] as const;

interface Props {
  data: TrendData | undefined;
  isLoading: boolean;
}

export default function TrendChartsPanel({ data, isLoading }: Props) {
  if (isLoading) {
    return <Skeleton variant="rectangular" height={620} sx={{ borderRadius: 2 }} />;
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      {/* Daily Appointment Volumes — BarChart */}
      <Box>
        <Typography variant="h6" sx={{ mb: 1 }}>
          Daily Appointment Volumes
        </Typography>
        <ResponsiveContainer width="100%" height={200}>
          <BarChart data={data?.dailyVolumes ?? []}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="date" />
            <YAxis />
            <Tooltip />
            <Bar dataKey="count" fill={CHART_COLORS.primary} name="Appointments" />
          </BarChart>
        </ResponsiveContainer>
      </Box>

      {/* No-show Rate + AI Latency — LineChart with dual y-axis */}
      <Box>
        <Typography variant="h6" sx={{ mb: 1 }}>
          No-Show Rate & AI Response Latency
        </Typography>
        <ResponsiveContainer width="100%" height={200}>
          <LineChart data={data?.weeklyTrends ?? []}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="week" />
            <YAxis yAxisId="left" unit="%" />
            <YAxis yAxisId="right" orientation="right" unit="ms" />
            <Tooltip />
            <Legend />
            <Line
              yAxisId="left"
              type="monotone"
              dataKey="noShowRate"
              stroke={CHART_COLORS.warning}
              name="No-show Rate (%)"
            />
            <Line
              yAxisId="right"
              type="monotone"
              dataKey="aiLatencyP95Ms"
              stroke={CHART_COLORS.secondary}
              name="AI p95 Latency (ms)"
            />
          </LineChart>
        </ResponsiveContainer>
      </Box>

      {/* Document Processing Status — PieChart */}
      <Box>
        <Typography variant="h6" sx={{ mb: 1 }}>
          Document Processing Status
        </Typography>
        <ResponsiveContainer width="100%" height={220}>
          <PieChart>
            <Pie
              data={data?.documentThroughput ?? []}
              dataKey="count"
              nameKey="status"
              cx="50%"
              cy="50%"
              outerRadius={80}
            >
              {(data?.documentThroughput ?? []).map((_, i) => (
                <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />
              ))}
            </Pie>
            <Tooltip />
            <Legend />
          </PieChart>
        </ResponsiveContainer>
      </Box>
    </Box>
  );
}
