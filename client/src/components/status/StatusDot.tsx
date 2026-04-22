import { Box, Typography } from '@mui/material';

import type { SemanticStatus } from './SemanticStatusChip';

/** Maps each semantic status to its MUI palette colour key. */
const STATUS_COLOR: Record<SemanticStatus, string> = {
  success: 'success.main',
  warning: 'warning.main',
  error:   'error.main',
  info:    'info.main',
};

interface StatusDotProps {
  /** Semantic status — determines the dot colour. */
  status: SemanticStatus;
  /**
   * Visible text label — MANDATORY.
   * WCAG 1.4.1: The dot must never be rendered without an accompanying text label;
   * colour alone is insufficient to convey status information.
   */
  label: string;
}

/**
 * Compact inline status indicator: 8×8px colour dot + text label.
 *
 * Suitable for table rows, queue cards, and inline status badges where a full chip
 * would consume too much horizontal space.
 *
 * **Accessibility:**
 * - Dot has `role="img"` + `aria-label` — screen readers announce the status name.
 * - `label` prop is mandatory — the dot is never rendered colour-only (WCAG 1.4.1).
 *
 * @example
 * ```tsx
 * <StatusDot status="success" label="Arrived" />
 * <StatusDot status="warning" label="Awaiting" />
 * ```
 */
export function StatusDot({ status, label }: StatusDotProps) {
  const bgColor = STATUS_COLOR[status];

  return (
    <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}>
      <Box
        role="img"
        aria-label={`${status} status`}
        sx={{
          width: 8,
          height: 8,
          borderRadius: '50%',
          bgcolor: bgColor,
          flexShrink: 0,
        }}
      />
      <Typography variant="body2" component="span">
        {label}
      </Typography>
    </Box>
  );
}
