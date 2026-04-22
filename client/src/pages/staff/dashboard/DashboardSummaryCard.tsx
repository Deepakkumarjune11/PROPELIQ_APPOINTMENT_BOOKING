// Reusable summary Card sub-component for SCR-010 Staff Dashboard (US_016).
// Displays a count value, a label, and an optional badge — matching wireframe SCR-010 layout.
import { Box, Card, CardContent, Chip, Skeleton, Typography } from '@mui/material';

interface DashboardSummaryCardProps {
  title: string;
  value: number | undefined;
  subtitle?: string;
  /** Optional badge text (e.g. "4 waiting"). */
  badge?: string;
  /** MUI Chip color for the badge. */
  badgeColor?: 'default' | 'warning' | 'error' | 'success' | 'info';
  isLoading?: boolean;
}

export default function DashboardSummaryCard({
  title,
  value,
  subtitle,
  badge,
  badgeColor = 'default',
  isLoading = false,
}: DashboardSummaryCardProps) {
  return (
    <Card elevation={1} sx={{ borderRadius: 2 }}>
      <CardContent>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 1 }}>
          <Typography
            variant="caption"
            sx={{
              textTransform: 'uppercase',
              letterSpacing: '0.5px',
              color: 'text.secondary',
              fontWeight: 500,
            }}
          >
            {title}
          </Typography>
          {badge && (
            <Chip label={badge} color={badgeColor} size="small" />
          )}
        </Box>

        {isLoading ? (
          <>
            <Skeleton variant="rectangular" height={36} width="60%" sx={{ mb: 1 }} />
            <Skeleton variant="text" width="80%" />
          </>
        ) : (
          <>
            <Typography variant="h4" component="p" sx={{ fontWeight: 500, lineHeight: 1.2 }}>
              {value ?? '—'}
            </Typography>
            {subtitle && (
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                {subtitle}
              </Typography>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
