// Responsive grid of availability slot cards.
// Breakpoints: xs=12 (1 col), sm=6 (2 col), md=4 (3 col), lg=3 (4 col).
import Box from '@mui/material/Box';
import Grid from '@mui/material/Grid';
import Typography from '@mui/material/Typography';

import type { AvailabilitySlot } from '@/api/availability';
import SlotCard from './SlotCard';

interface SlotGridProps {
  slots: AvailabilitySlot[];
}

export default function SlotGrid({ slots }: SlotGridProps) {
  const count = slots.length;

  return (
    <Box>
      <Typography variant="h6" gutterBottom aria-live="polite" aria-atomic="true">
        {count} slot{count !== 1 ? 's' : ''} available
      </Typography>

      <Grid container spacing={3}>
        {slots.map((slot) => (
          <Grid item key={slot.id} xs={12} sm={6} md={4} lg={3}>
            <SlotCard slot={slot} />
          </Grid>
        ))}
      </Grid>
    </Box>
  );
}
