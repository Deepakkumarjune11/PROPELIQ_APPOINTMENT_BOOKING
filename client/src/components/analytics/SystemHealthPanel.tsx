// System health panel for analytics dashboard (US_033, AC-4, SCR-028).
// Shows API latency percentiles, DB connection pool, cache hit ratio, and AI gateway status.
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import WarningIcon from '@mui/icons-material/Warning';
import { Box, Chip, LinearProgress, Skeleton, Stack, Typography } from '@mui/material';

import type { SystemHealth } from '@/types/analytics';

interface Props {
  data: SystemHealth | undefined;
  isLoading: boolean;
}

export default function SystemHealthPanel({ data, isLoading }: Props) {
  if (isLoading) {
    return <Skeleton variant="rectangular" height={180} sx={{ borderRadius: 2 }} />;
  }

  const gatewayStatusColor: 'success' | 'warning' | 'error' =
    data?.aiGatewayStatus === 'Available'
      ? 'success'
      : data?.aiGatewayStatus === 'Degraded'
        ? 'warning'
        : 'error';

  const dbUsage = data?.dbPoolUsagePct ?? 0;
  const dbProgressColor: 'error' | 'primary' = dbUsage > 80 ? 'error' : 'primary';

  return (
    <Box>
      <Typography variant="h6" sx={{ mb: 2 }}>
        System Health
      </Typography>

      {/* API latency percentiles + AI gateway status */}
      <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ mb: 3 }}>
        <Chip
          label={`p50: ${data?.apiLatencyP50Ms ?? '–'}ms`}
          variant="outlined"
          size="small"
        />
        <Chip
          label={`p95: ${data?.apiLatencyP95Ms ?? '–'}ms`}
          variant="outlined"
          size="small"
        />
        <Chip
          label={`p99: ${data?.apiLatencyP99Ms ?? '–'}ms`}
          variant="outlined"
          size="small"
        />
        <Chip
          label={`AI Gateway: ${data?.aiGatewayStatus ?? 'Unknown'}`}
          color={gatewayStatusColor}
          icon={
            gatewayStatusColor === 'success' ? (
              <CheckCircleIcon />
            ) : (
              <WarningIcon />
            )
          }
          size="small"
        />
      </Stack>

      {/* DB connection pool gauge */}
      <Box sx={{ mb: 2 }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
          <Typography variant="body2">DB Connection Pool</Typography>
          <Typography variant="body2">{dbUsage}%</Typography>
        </Box>
        <LinearProgress
          variant="determinate"
          value={dbUsage}
          color={dbProgressColor}
          aria-label={`DB connection pool usage: ${dbUsage}%`}
        />
      </Box>

      {/* Cache hit ratio gauge */}
      <Box>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
          <Typography variant="body2">Cache Hit Ratio</Typography>
          <Typography variant="body2">{data?.cacheHitRatioPct ?? 0}%</Typography>
        </Box>
        <LinearProgress
          variant="determinate"
          value={data?.cacheHitRatioPct ?? 0}
          color="success"
          aria-label={`Cache hit ratio: ${data?.cacheHitRatioPct ?? 0}%`}
        />
      </Box>
    </Box>
  );
}
