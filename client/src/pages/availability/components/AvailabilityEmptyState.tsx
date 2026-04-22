// AC-4: Empty state shown when no slots are available for the selected date range.
// Provides a CTA to reset the filter back to today → today+7.
import EventBusyIcon from '@mui/icons-material/EventBusy';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';

interface AvailabilityEmptyStateProps {
  /** Resets the date filter to default range (today → today+7). */
  onReset: () => void;
}

export default function AvailabilityEmptyState({ onReset }: AvailabilityEmptyStateProps) {
  return (
    <Box
      sx={{ textAlign: 'center', py: 6, px: 3 }}
      role="status"
      aria-live="polite"
    >
      <EventBusyIcon sx={{ fontSize: 64, color: 'text.disabled', mb: 2 }} aria-hidden="true" />

      <Typography variant="h6" color="text.secondary" gutterBottom>
        No appointments available
      </Typography>

      <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>
        Try selecting different dates or contact the clinic.
      </Typography>

      <Button
        variant="contained"
        color="primary"
        onClick={onReset}
        sx={{ minHeight: 44 }}
      >
        Try different dates
      </Button>
    </Box>
  );
}
