// KPI metric card for analytics dashboard (US_033, AC-1, SCR-028).
// Design tokens: Card elevation=1, borderRadius=2 (8px), p=3 (24px); h4 value; body2 label.
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import { Box, Card, CardContent, Chip, Skeleton, Typography } from '@mui/material';

export type KpiTrend = 'up' | 'down' | 'neutral';

interface MetricKpiCardProps {
  title: string;
  value: string | number;
  unit?: string;
  trend?: KpiTrend;
  trendLabel?: string;
  isLoading?: boolean;
}

export default function MetricKpiCard({
  title,
  value,
  unit,
  trend,
  trendLabel,
  isLoading,
}: MetricKpiCardProps) {
  if (isLoading) {
    return (
      <Card
        elevation={1}
        sx={{ borderRadius: 2, p: 3, minHeight: 140 }}
        aria-label={`${title} loading`}
      >
        <Skeleton variant="text" width="60%" sx={{ mb: 1 }} />
        <Skeleton variant="text" width="40%" height={48} />
        <Skeleton variant="text" width="50%" />
      </Card>
    );
  }

  const trendColor =
    trend === 'up' ? 'primary' : trend === 'down' ? 'error' : 'default';

  const trendIcon =
    trend === 'up' ? (
      <ArrowUpwardIcon />
    ) : trend === 'down' ? (
      <ArrowDownwardIcon />
    ) : undefined;

  return (
    <Card
      elevation={1}
      sx={{ borderRadius: 2, p: 3 }}
      aria-label={title}
    >
      <CardContent sx={{ p: 0, '&:last-child': { pb: 0 } }}>
        <Typography variant="body2" color="text.secondary">
          {title}
        </Typography>
        <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 0.5, my: 1 }}>
          <Typography variant="h4" fontWeight={600}>
            {value}
          </Typography>
          {unit && (
            <Typography variant="body2" color="text.secondary">
              {unit}
            </Typography>
          )}
        </Box>
        {trend && trendLabel && (
          <Chip
            label={trendLabel}
            color={trendColor}
            size="small"
            icon={trendIcon}
          />
        )}
      </CardContent>
    </Card>
  );
}
