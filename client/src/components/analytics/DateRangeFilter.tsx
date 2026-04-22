// Date range filter with two HTML date inputs (US_033, AC-2, SCR-028).
// Uses MUI TextField type="date" to avoid additional date-picker dependency.
// Default range: today minus 30 days to today.
import { Box, TextField, Typography } from '@mui/material';

import type { DateRange } from '@/types/analytics';

interface Props {
  range: DateRange;
  onRangeChange: (startDate: Date, endDate: Date) => void;
}

function toInputValue(date: Date): string {
  // Use local date parts to avoid UTC-shift on date-only inputs
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function fromInputValue(value: string): Date {
  // Parse as local time to avoid off-by-one from UTC parsing
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, (month ?? 1) - 1, day ?? 1);
}

export default function DateRangeFilter({ range, onRangeChange }: Props) {
  const handleStartChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.value) return;
    const newStart = fromInputValue(e.target.value);
    // Clamp: start must not be after end
    const clampedStart = newStart > range.endDate ? range.endDate : newStart;
    onRangeChange(clampedStart, range.endDate);
  };

  const handleEndChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.value) return;
    const newEnd = fromInputValue(e.target.value);
    // Clamp: end must not be before start
    const clampedEnd = newEnd < range.startDate ? range.startDate : newEnd;
    onRangeChange(range.startDate, clampedEnd);
  };

  return (
    <Box
      sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}
      role="group"
      aria-label="Date range filter"
    >
      <Typography variant="body2" color="text.secondary" sx={{ whiteSpace: 'nowrap' }}>
        Date range:
      </Typography>
      <TextField
        type="date"
        label="Start date"
        size="small"
        value={toInputValue(range.startDate)}
        onChange={handleStartChange}
        inputProps={{
          max: toInputValue(range.endDate),
          'aria-label': 'Start date',
        }}
        InputLabelProps={{ shrink: true }}
      />
      <Typography variant="body2" color="text.secondary">
        to
      </Typography>
      <TextField
        type="date"
        label="End date"
        size="small"
        value={toInputValue(range.endDate)}
        onChange={handleEndChange}
        inputProps={{
          min: toInputValue(range.startDate),
          'aria-label': 'End date',
        }}
        InputLabelProps={{ shrink: true }}
      />
    </Box>
  );
}
