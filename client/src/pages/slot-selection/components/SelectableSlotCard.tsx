// SCR-002: Selectable slot card with primary.500 selected-state highlight.
// Extends the SlotCard pattern from SCR-001 — adds isSelected/onSelect props and
// optimistic UI: visual highlight fires immediately on click before any API call.
//
// ARIA: role="button", aria-pressed={isSelected}, keyboard Enter/Space triggers onSelect.
// UXR-101: keyboard accessible. UXR-102: 44px min touch target on the card itself.
import EventAvailableIcon from '@mui/icons-material/EventAvailable';
import LocationOnIcon from '@mui/icons-material/LocationOn';
import ScheduleIcon from '@mui/icons-material/Schedule';
import VideocamIcon from '@mui/icons-material/Videocam';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Divider from '@mui/material/Divider';
import Typography from '@mui/material/Typography';
import type { KeyboardEvent } from 'react';

import type { AvailabilitySlot } from '@/api/availability';
import NoShowRiskBadge from './NoShowRiskBadge';

function formatDatetime(datetime: string): string {
  return new Intl.DateTimeFormat('en-US', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  }).format(new Date(datetime));
}

interface SelectableSlotCardProps {
  slot: AvailabilitySlot;
  isSelected: boolean;
  onSelect: (slot: AvailabilitySlot) => void;
}

export default function SelectableSlotCard({ slot, isSelected, onSelect }: SelectableSlotCardProps) {
  const isVirtual = slot.visitType === 'telehealth';

  const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onSelect(slot);
    }
  };

  return (
    <Card
      // Optimistic selection: sx border/background update before any API call
      sx={{
        border: isSelected ? '2px solid' : '1px solid',
        borderColor: isSelected ? 'primary.main' : 'divider',
        bgcolor: isSelected ? 'primary.50' : 'background.paper',
        // primary.50 mapped to #E3F2FD per designsystem.md
        backgroundColor: isSelected ? '#E3F2FD' : undefined,
        transition: 'border-color 150ms cubic-bezier(0.4,0,0.2,1), background-color 150ms cubic-bezier(0.4,0,0.2,1)',
        cursor: 'pointer',
        '&:hover': {
          boxShadow: 4,
        },
        '&:focus-visible': {
          outline: '2px solid',
          outlineColor: 'primary.main',
          outlineOffset: 2,
        },
      }}
      role="button"
      tabIndex={0}
      aria-pressed={isSelected}
      aria-label={`${isSelected ? 'Selected slot' : 'Select slot'}: ${formatDatetime(slot.datetime)} with ${slot.provider}`}
      onClick={() => onSelect(slot)}
      onKeyDown={handleKeyDown}
    >
      <CardContent>
        {/* Slot heading row: datetime + risk badge */}
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 1 }}>
          <Box>
            <Typography variant="h6" component="div" fontWeight={500}>
              {formatDatetime(slot.datetime)}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {slot.provider}{slot.specialty ? ` • ${slot.specialty}` : ''}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {isVirtual ? 'Telehealth visit' : 'In-person visit'}
            </Typography>
          </Box>
          <NoShowRiskBadge
            noShowRisk={slot.noShowRisk}
            riskContributingFactors={slot.riskContributingFactors}
            isPartialScoring={slot.isPartialScoring}
          />
        </Box>

        {/* Metadata row: location + duration */}
        {(slot.location || slot.durationMinutes) && (
          <>
            <Divider sx={{ my: 1.5 }} />
            <Box sx={{ display: 'flex', gap: 3, flexWrap: 'wrap' }}>
              {slot.location && (
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                  <LocationOnIcon fontSize="small" color="action" aria-hidden="true" />
                  <Typography variant="body2" color="text.secondary">
                    {slot.location}
                  </Typography>
                </Box>
              )}
              {slot.durationMinutes && (
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                  <ScheduleIcon fontSize="small" color="action" aria-hidden="true" />
                  <Typography variant="body2" color="text.secondary">
                    {slot.durationMinutes} minutes
                  </Typography>
                </Box>
              )}
            </Box>
          </>
        )}

        {/* Visit type icon row */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mt: 1 }}>
          {isVirtual ? (
            <VideocamIcon fontSize="small" color="action" aria-hidden="true" />
          ) : (
            <EventAvailableIcon fontSize="small" color="action" aria-hidden="true" />
          )}
          <Typography variant="caption" color="text.secondary">
            {isVirtual ? 'Telehealth available' : 'Available'}
          </Typography>
        </Box>
      </CardContent>
    </Card>
  );
}
