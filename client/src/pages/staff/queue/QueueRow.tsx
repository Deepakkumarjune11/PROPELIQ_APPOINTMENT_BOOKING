// SCR-012 draggable table row (US_017, AC-2, AC-3, UXR-404).
// Composed from useSortable (dnd-kit) + MUI TableRow.
// Status badge colour mapping:
//   waiting  → warning (orange  #FF9800)
//   arrived  → success (green   #4CAF50)
//   in-room  → secondary (purple #9C27B0)
//   completed / left → default (neutral gray)
import { memo } from 'react';
import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import DragIndicatorIcon from '@mui/icons-material/DragIndicator';
import {
  Button,
  Chip,
  IconButton,
  Stack,
  TableCell,
  TableRow,
  Tooltip,
} from '@mui/material';
import type { SxProps, Theme } from '@mui/material';

import type { QueueEntry } from '@/api/staff';

// ── Status colour mapping ────────────────────────────────────────────────────

type ChipColor = 'warning' | 'success' | 'secondary' | 'default';

function statusChipColor(status: QueueEntry['status']): ChipColor {
  switch (status) {
    case 'waiting':
      return 'warning';
    case 'arrived':
      return 'success';
    case 'in-room':
      return 'secondary';
    default:
      return 'default';
  }
}

function statusLabel(status: QueueEntry['status']): string {
  switch (status) {
    case 'waiting':
      return 'Waiting';
    case 'arrived':
      return 'Arrived';
    case 'in-room':
      return 'In Room';
    case 'completed':
      return 'Completed';
    case 'left':
      return 'Left';
    default:
      return status;
  }
}

// ── Props ────────────────────────────────────────────────────────────────────

export interface QueueRowProps {
  entry: QueueEntry;
  selected: boolean;
  onToggleSelect: (id: string) => void;
  onMarkArrived: (id: string) => void;
  onMarkLeft: (id: string) => void;
  isPending: boolean;
}

// ── Component ────────────────────────────────────────────────────────────────

export const QueueRow = memo(function QueueRow({
  entry,
  selected,
  onToggleSelect,
  onMarkArrived,
  onMarkLeft,
  isPending,
}: QueueRowProps) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: entry.appointmentId });

  const rowSx: SxProps<Theme> = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
    background: isDragging ? 'action.hover' : selected ? 'action.selected' : undefined,
    cursor: isDragging ? 'grabbing' : 'default',
  };

  const isActive = entry.status !== 'completed' && entry.status !== 'left';
  const canMarkArrived = entry.status === 'waiting';
  const canMarkLeft = entry.status !== 'completed' && entry.status !== 'left';

  const formattedTime = (() => {
    try {
      return new Date(entry.appointmentTime).toLocaleTimeString([], {
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return entry.appointmentTime;
    }
  })();

  return (
    <TableRow ref={setNodeRef} sx={rowSx} hover={!isDragging}>
      {/* Drag handle */}
      <TableCell sx={{ width: 40, pr: 0 }}>
        <Tooltip title="Drag to reorder">
          <IconButton
            size="small"
            aria-label="Drag to reorder"
            sx={{ cursor: 'grab', color: 'text.secondary' }}
            {...attributes}
            {...listeners}
          >
            <DragIndicatorIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </TableCell>

      {/* Bulk-select checkbox */}
      <TableCell padding="checkbox">
        <input
          type="checkbox"
          aria-label={`Select ${entry.patientName}`}
          checked={selected}
          onChange={() => onToggleSelect(entry.appointmentId)}
          style={{ width: 18, height: 18, cursor: 'pointer' }}
          disabled={!isActive}
        />
      </TableCell>

      {/* Position */}
      <TableCell sx={{ color: 'text.secondary', fontWeight: 500, width: 48 }}>
        {entry.queuePosition}
      </TableCell>

      {/* Patient name */}
      <TableCell sx={{ fontWeight: 500 }}>{entry.patientName}</TableCell>

      {/* Appointment time */}
      <TableCell>{formattedTime}</TableCell>

      {/* Visit type */}
      <TableCell sx={{ color: 'text.secondary' }}>{entry.visitType}</TableCell>

      {/* Status badge */}
      <TableCell>
        <Chip
          label={statusLabel(entry.status)}
          color={statusChipColor(entry.status)}
          size="small"
          sx={{ fontWeight: 500 }}
        />
      </TableCell>

      {/* Action buttons (SCR-013 inline) */}
      <TableCell>
        <Stack direction="row" spacing={1}>
          {canMarkArrived && (
            <Button
              size="small"
              variant="contained"
              color="success"
              disabled={isPending}
              onClick={() => onMarkArrived(entry.appointmentId)}
              aria-label={`Mark ${entry.patientName} as arrived`}
            >
              Mark Arrived
            </Button>
          )}
          {canMarkLeft && (
            <Button
              size="small"
              variant="text"
              color="warning"
              disabled={isPending}
              onClick={() => onMarkLeft(entry.appointmentId)}
              aria-label={`Mark ${entry.patientName} as left`}
            >
              Mark Left
            </Button>
          )}
        </Stack>
      </TableCell>
    </TableRow>
  );
});
