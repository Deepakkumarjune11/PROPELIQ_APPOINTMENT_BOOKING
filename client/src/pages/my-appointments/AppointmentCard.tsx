// AppointmentCard — reusable appointment list item for SCR-008 (My Appointments).
// Renders date/time, provider, visit type, status badge, and CTA button.
//
// Status badge rules (AC-1, AC-4):
//   • status='booked' + preferredSlotDatetime=null   → green "Confirmed" chip
//   • status='booked' + preferredSlotDatetime≠null   → orange "Watchlist" chip + Tooltip
// "Select preferred slot" button shown ONLY on confirmed (booked, no watchlist) appointments.
//
// UXR-101: WCAG 2.2 AA  |  UXR-102: ARIA labels  |  UXR-003: tooltip guidance
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ScheduleIcon from '@mui/icons-material/Schedule';
import WatchLaterIcon from '@mui/icons-material/WatchLater';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardActions from '@mui/material/CardActions';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import { useNavigate } from 'react-router-dom';

import type { AppointmentDto } from '@/api/appointments';

interface AppointmentCardProps {
  appointment: AppointmentDto;
}

function formatDatetime(iso: string): string {
  return new Intl.DateTimeFormat('en-US', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  }).format(new Date(iso));
}

export default function AppointmentCard({ appointment }: AppointmentCardProps) {
  const navigate = useNavigate();

  const { id, slotDatetime, providerName, visitType, status, preferredSlotDatetime } = appointment;

  const isConfirmed = status === 'booked' && preferredSlotDatetime === null;
  const isOnWatchlist = status === 'booked' && preferredSlotDatetime !== null;

  const handleSelectPreferredSlot = () => {
    navigate(`/appointments/${id}/preferred-slot`);
  };

  return (
    <Card
      variant="outlined"
      sx={{
        borderRadius: 2,
        boxShadow: '0 1px 3px rgba(0,0,0,0.12), 0 1px 2px rgba(0,0,0,0.24)',
      }}
    >
      <CardContent>
        {/* Card header: date/time + status badge */}
        <Box
          sx={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'flex-start',
            mb: 1,
          }}
        >
          <Box>
            <Typography variant="h6" component="p" sx={{ fontWeight: 500, mb: 0.5 }}>
              {formatDatetime(slotDatetime)}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {providerName} • {visitType}
            </Typography>
          </Box>

          {/* Status badge */}
          {isConfirmed && (
            <Chip
              icon={<CheckCircleIcon fontSize="small" />}
              label="Confirmed"
              size="small"
              aria-label="Appointment confirmed"
              sx={{
                bgcolor: 'success.main',
                color: '#fff',
                fontWeight: 500,
                '& .MuiChip-icon': { color: '#fff' },
              }}
            />
          )}

          {isOnWatchlist && (
            <Tooltip
              title="We'll notify you automatically if this slot opens."
              arrow
              placement="top"
            >
              <Chip
                icon={<WatchLaterIcon fontSize="small" />}
                label={`Watchlist: ${formatDatetime(preferredSlotDatetime!)}`}
                size="small"
                aria-label={`On watchlist for ${formatDatetime(preferredSlotDatetime!)}`}
                sx={{
                  bgcolor: 'warning.main',
                  color: 'rgba(0,0,0,0.87)',
                  fontWeight: 500,
                  '& .MuiChip-icon': { color: 'rgba(0,0,0,0.87)' },
                  maxWidth: 280,
                  '& .MuiChip-label': { overflow: 'hidden', textOverflow: 'ellipsis' },
                }}
              />
            </Tooltip>
          )}

          {/* Informational badge for other statuses */}
          {!isConfirmed && !isOnWatchlist && (
            <Chip
              icon={<ScheduleIcon fontSize="small" />}
              label={status.charAt(0).toUpperCase() + status.slice(1)}
              size="small"
              variant="outlined"
            />
          )}
        </Box>
      </CardContent>

      {/* CTA — only on confirmed (booked, no watchlist) appointments per task spec */}
      {isConfirmed && (
        <CardActions sx={{ pt: 0, px: 2, pb: 2 }}>
          <Button
            variant="outlined"
            size="small"
            onClick={handleSelectPreferredSlot}
            aria-label={`Select preferred slot for appointment on ${formatDatetime(slotDatetime)}`}
            sx={{ minHeight: 44 }}
          >
            Select preferred slot
          </Button>
        </CardActions>
      )}
    </Card>
  );
}
