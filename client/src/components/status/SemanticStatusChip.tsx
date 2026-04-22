import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import InfoIcon from '@mui/icons-material/Info';
import WarningIcon from '@mui/icons-material/Warning';
import { Chip } from '@mui/material';
import type { ChipProps } from '@mui/material';
import type { ReactElement } from 'react';

/**
 * SemanticStatusChip — canonical status indicator for the platform.
 *
 * WCAG 1.4.1 Use of Color: Color MUST NOT be the only visual means of conveying information.
 * This component enforces compliance by ALWAYS rendering an icon alongside the color.
 * Do NOT use raw `<Chip color="success">` without an icon; use `<SemanticStatusChip>` instead.
 *
 * UXR-303: healthcare semantic color palette (success #4CAF50, warning #FF9800,
 * error #F44336, info #2196F3). Clinical category colours in theme.palette.clinical.*
 */

/** Union of all valid semantic status values. */
export type SemanticStatus = 'success' | 'warning' | 'error' | 'info';

interface StatusConfig {
  icon: ReactElement;
  defaultLabel: string;
  color: ChipProps['color'];
}

const STATUS_CONFIG: Record<SemanticStatus, StatusConfig> = {
  success: { icon: <CheckCircleIcon fontSize="small" />, defaultLabel: 'Success', color: 'success' },
  warning: { icon: <WarningIcon fontSize="small" />,     defaultLabel: 'Warning', color: 'warning' },
  error:   { icon: <ErrorIcon fontSize="small" />,       defaultLabel: 'Error',   color: 'error' },
  info:    { icon: <InfoIcon fontSize="small" />,        defaultLabel: 'Info',    color: 'info' },
};

interface SemanticStatusChipProps {
  /** Semantic status — determines both the chip colour and the icon. */
  status: SemanticStatus;
  /**
   * Visible text label. When omitted, falls back to the status default label
   * (e.g., `"Success"` for `status="success"`).
   */
  label?: string;
  /** Chip size. Defaults to `"small"` to match data-dense clinical UIs. */
  size?: 'small' | 'medium';
}

/**
 * Status chip that always renders colour + icon + text label together (WCAG 1.4.1).
 *
 * @example
 * ```tsx
 * <SemanticStatusChip status="success" label="Booking Confirmed" />
 * <SemanticStatusChip status="warning" label="Pending Review" />
 * <SemanticStatusChip status="error" label="Cancelled" />
 * <SemanticStatusChip status="info" label="In Progress" />
 * ```
 */
export function SemanticStatusChip({ status, label, size = 'small' }: SemanticStatusChipProps) {
  const { icon, defaultLabel, color } = STATUS_CONFIG[status];

  return (
    <Chip
      icon={icon}
      label={label ?? defaultLabel}
      color={color}
      size={size}
      variant="filled"
    />
  );
}
