// Individual availability slot card — matches wireframe-SCR-001 slot-card layout.
// UXR-102: 44px min-height on Select button (touch target compliance).
import EventAvailableIcon from '@mui/icons-material/EventAvailable';
import VideocamIcon from '@mui/icons-material/Videocam';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import { useNavigate } from 'react-router-dom';

import type { AvailabilitySlot } from '@/api/availability';

function formatTime(datetime: string): string {
  return new Intl.DateTimeFormat('en-US', {
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  }).format(new Date(datetime));
}

function formatDate(datetime: string): string {
  return new Intl.DateTimeFormat('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
  }).format(new Date(datetime));
}

interface SlotCardProps {
  slot: AvailabilitySlot;
}

export default function SlotCard({ slot }: SlotCardProps) {
  const navigate = useNavigate();
  const isVirtual = slot.visitType === 'telehealth';
  const timeLabel = formatTime(slot.datetime);

  const handleSelect = () => {
    navigate('/appointments/slot-selection', { state: { slot } });
  };

  return (
    <Card
      variant="outlined"
      sx={{
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        transition: 'transform 150ms cubic-bezier(0.4,0,0.2,1), box-shadow 150ms cubic-bezier(0.4,0,0.2,1)',
        '&:hover': {
          transform: 'translateY(-2px)',
          boxShadow: 8,
        },
      }}
      aria-label={`Available slot: ${timeLabel} with ${slot.provider}`}
    >
      <CardContent sx={{ flexGrow: 1 }}>
        {/* Time — prominent, matches .slot-time in wireframe */}
        <Typography variant="h5" component="div" fontWeight={500} gutterBottom>
          {timeLabel}
        </Typography>

        <Typography variant="body2" color="text.secondary" gutterBottom>
          {formatDate(slot.datetime)}
        </Typography>

        <Typography variant="body1" color="text.primary" gutterBottom>
          {slot.provider}
        </Typography>

        {slot.specialty && (
          <Typography variant="body2" color="text.secondary" gutterBottom>
            {slot.specialty}&nbsp;•&nbsp;{isVirtual ? 'Telehealth' : 'In-person'}
          </Typography>
        )}

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 2 }}>
          {isVirtual ? (
            <VideocamIcon fontSize="small" color="action" aria-hidden="true" />
          ) : (
            <EventAvailableIcon fontSize="small" color="action" aria-hidden="true" />
          )}
          <Typography variant="caption" color="text.secondary">
            {isVirtual ? 'Telehealth available' : 'Available'}
          </Typography>
        </Box>

        <Button
          variant="outlined"
          color="primary"
          fullWidth
          onClick={handleSelect}
          sx={{ minHeight: 44 }}
          aria-label={`Select ${timeLabel} with ${slot.provider}`}
        >
          Select
        </Button>
      </CardContent>
    </Card>
  );
}
