import { Box, Button, LinearProgress, Typography } from '@mui/material';

/**
 * Formats a seconds value into a human-readable time string.
 * @example formatTimeRemaining(45)  → "45s"
 * @example formatTimeRemaining(90)  → "1m 30s"
 * @example formatTimeRemaining(120) → "2m"
 */
function formatTimeRemaining(seconds: number): string {
  if (seconds < 60) return `${seconds}s`;
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return s === 0 ? `${m}m` : `${m}m ${s}s`;
}

interface OperationProgressBarProps {
  /** Current progress value, 0–100. Values outside this range are clamped. */
  progress: number;
  /** Estimated seconds remaining. `undefined` = not yet calculated (omits the ETA label). */
  estimatedSecondsRemaining?: number;
  /** When `true`, replaces the ETA label with a stall warning and optional Cancel button. */
  isStalled?: boolean;
  /**
   * Human-readable description of the operation shown as the left caption.
   * Also used as the `aria-label` on the progressbar element.
   * Default: `"Processing..."`
   */
  label?: string;
  /**
   * Optional cancel handler. When provided, a Cancel button is rendered beside the stall warning.
   * When absent, no Cancel button is shown.
   */
  onCancel?: () => void;
}

/**
 * Real-time operation progress bar for long-running operations (US_038 AC-4 / UXR-403).
 *
 * ## Usage contexts
 *
 * **File upload (SCR-014)**
 * Drive `progress` from the XHR `upload.onprogress` event: `loaded / total * 100`.
 * Pass `onCancel={() => xhr.abort()}` to allow mid-upload cancellation.
 *
 * **Document processing (SCR-015)**
 * Drive `progress` by polling `GET /api/v1/documents/{id}/status` (see EP-003 for polling
 * interval). `onCancel` is optional — server-side processing may not support cancellation.
 *
 * **AI analysis (SCR-017)**
 * Drive `progress` from server-sent events (SSE) or WebSocket progress messages from the AI
 * gateway pipeline (EP-007). Pass `onCancel` to call an abort endpoint.
 *
 * ## Stall detection
 * Set `isStalled={true}` after 30 seconds of no progress update (use `useOperationProgress`
 * hook which provides this automatically). The stall warning and Cancel button appear in the
 * `warning.main` colour.
 *
 * ## Accessibility
 * MUI `LinearProgress` renders `role="progressbar"` with `aria-valuenow`, `aria-valuemin=0`,
 * and `aria-valuemax=100` automatically. This component adds `aria-label` for context.
 */
export function OperationProgressBar({
  progress,
  estimatedSecondsRemaining,
  isStalled = false,
  label,
  onCancel,
}: OperationProgressBarProps) {
  // Clamp to valid 0–100 range — prevents invalid progressbar states from upstream data races
  const clampedProgress = Math.max(0, Math.min(100, progress));

  const operationLabel = label ?? 'Processing...';

  return (
    <Box>
      <LinearProgress
        variant="determinate"
        value={clampedProgress}
        aria-label={operationLabel}
        sx={{ borderRadius: 1, height: 6 }}
      />

      <Box
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          mt: 0.5,
          gap: 1,
        }}
      >
        {/* Left: operation description */}
        <Typography variant="caption" color="text.secondary">
          {operationLabel}
        </Typography>

        {/* Right: ETA or stall warning */}
        {isStalled ? (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Typography variant="caption" color="warning.main">
              This is taking longer than expected
            </Typography>
            {onCancel && (
              <Button
                variant="text"
                size="small"
                color="warning"
                onClick={onCancel}
                sx={{ minWidth: 'auto', p: '0 4px', lineHeight: 1.5 }}
              >
                Cancel
              </Button>
            )}
          </Box>
        ) : (
          <Typography variant="caption" color="text.secondary">
            {clampedProgress}%
            {estimatedSecondsRemaining !== undefined
              ? ` · ~${formatTimeRemaining(estimatedSecondsRemaining)}`
              : ''}
          </Typography>
        )}
      </Box>
    </Box>
  );
}
