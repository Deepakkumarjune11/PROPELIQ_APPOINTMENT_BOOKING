// Skeleton shimmer grid shown while isLoading === true (8 cards, wave animation).
// Matches SlotCard dimensions for a stable layout shift-free loading experience.
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Grid from '@mui/material/Grid';
import Skeleton from '@mui/material/Skeleton';

const SKELETON_COUNT = 8;

export default function SlotGridSkeleton() {
  return (
    <Grid
      container
      spacing={3}
      aria-busy="true"
      aria-label="Loading available slots"
    >
      {Array.from({ length: SKELETON_COUNT }).map((_, index) => (
        // eslint-disable-next-line react/no-array-index-key -- static skeleton list, index is stable
        <Grid item key={index} xs={12} sm={6} md={4} lg={3}>
          <Card variant="outlined">
            <CardContent>
              {/* Time row */}
              <Skeleton variant="text" width={80} height={32} animation="wave" />
              {/* Date row */}
              <Skeleton variant="text" width={100} height={20} animation="wave" />
              {/* Provider row */}
              <Skeleton variant="text" width="80%" height={24} animation="wave" />
              {/* Specialty row */}
              <Skeleton variant="text" width="60%" height={20} animation="wave" />
              {/* Availability badge */}
              <Skeleton variant="text" width={90} height={18} animation="wave" />
              {/* Select button */}
              <Box sx={{ mt: 1 }}>
                <Skeleton
                  variant="rectangular"
                  height={36}
                  animation="wave"
                  sx={{ borderRadius: 1 }}
                />
              </Box>
            </CardContent>
          </Card>
        </Grid>
      ))}
    </Grid>
  );
}
