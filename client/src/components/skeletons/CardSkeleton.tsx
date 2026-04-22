import { Card, Grid, Skeleton } from '@mui/material';

interface CardSkeletonProps {
  /** Number of skeleton cards to render in a responsive grid. Default: 1. */
  count?: number;
}

/**
 * Summary card skeleton — used in:
 * - SCR-010 (Staff Dashboard summary cards)
 * - SCR-008 (appointment cards)
 * - SCR-028 (metric tiles)
 *
 * Matches card layout: metric value block (48px) + title line + subtitle line.
 * Renders in a responsive Grid (xs=12, sm=6, md=3) matching SCR-010 summary card grid.
 */
export function CardSkeleton({ count = 1 }: CardSkeletonProps) {
  const items = Array.from({ length: count }, (_, i) => i);

  return (
    <Grid container spacing={3}>
      {items.map((i) => (
        <Grid item xs={12} sm={6} md={3} key={i}>
          <Card sx={{ p: 3, borderRadius: 2 }}>
            {/* Metric value block — 48px matches numeric KPI display */}
            <Skeleton variant="rectangular" height={48} width="40%" sx={{ mb: 1, borderRadius: 1 }} />
            {/* Card title line */}
            <Skeleton variant="text" width="60%" sx={{ fontSize: '1rem' }} />
            {/* Subtitle / secondary line */}
            <Skeleton variant="text" width="40%" sx={{ fontSize: '0.875rem' }} />
          </Card>
        </Grid>
      ))}
    </Grid>
  );
}
