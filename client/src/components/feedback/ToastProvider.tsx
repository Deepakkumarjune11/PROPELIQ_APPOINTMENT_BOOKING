import { Alert, Button, IconButton, Snackbar } from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';

import { useToastStore } from '@/stores/toast-store';

/** Maximum number of toasts rendered simultaneously (AC-5) */
const MAX_VISIBLE = 3;
/** Top offset below the fixed AppBar (80px) */
const TOP_BASE_PX = 80;
/** Vertical increment per stacked toast: 64px toast height + 8px gap */
const TOP_STEP_PX = 72;

/**
 * Renders up to 3 stacked toast notifications at fixed top-right position (AC-2, AC-3, AC-5).
 *
 * Place this component once, inside `ThemeProvider` and outside `RouterProvider`,
 * so toasts are globally available across all routes.
 *
 * Accessibility:
 * - Error toasts: `role="alert"` (assertive — interrupts screen reader immediately, WCAG 4.1.3)
 * - Success/info/warning: `role="status"` (polite — announced at next pause)
 * - `aria-atomic="true"` — whole message read as a unit on update
 * - Close button: `aria-label="Dismiss notification"`
 */
export function ToastProvider() {
  const queue = useToastStore((s) => s.queue);
  const dismissToast = useToastStore((s) => s.dismissToast);

  const visible = queue.slice(0, MAX_VISIBLE);

  return (
    <>
      {visible.map((toast, index) => {
        const isError = toast.severity === 'error';
        const topPx = TOP_BASE_PX + index * TOP_STEP_PX;

        const closeButton = (
          <IconButton
            size="small"
            aria-label="Dismiss notification"
            color="inherit"
            onClick={() => dismissToast(toast.id)}
          >
            <CloseIcon fontSize="small" />
          </IconButton>
        );

        // Retry action rendered alongside close on error toasts (AC-3)
        const action = toast.retryFn ? (
          <>
            <Button
              size="small"
              color="inherit"
              onClick={() => {
                toast.retryFn?.();
                dismissToast(toast.id);
              }}
              sx={{ mr: 0.5 }}
            >
              Retry
            </Button>
            {closeButton}
          </>
        ) : (
          closeButton
        );

        return (
          <Snackbar
            key={toast.id}
            open
            anchorOrigin={{ vertical: 'top', horizontal: 'right' }}
            // Error toasts persist (null autoDismissMs) — omit autoHideDuration entirely
            {...(toast.autoDismissMs !== null
              ? {
                  autoHideDuration: toast.autoDismissMs,
                  onClose: (_e, reason) => {
                    // Ignore clickaway — only dismiss on timeout or explicit close button
                    if (reason === 'clickaway') return;
                    dismissToast(toast.id);
                  },
                }
              : {})}
            sx={{ top: `${topPx}px !important` }}
          >
            <Alert
              severity={toast.severity}
              // WCAG 4.1.3: role="alert" announces assertively; role="status" announces politely
              role={isError ? 'alert' : 'status'}
              aria-atomic="true"
              action={action}
              onClose={() => dismissToast(toast.id)}
              sx={{ width: '100%', alignItems: 'center' }}
            >
              {toast.message}
            </Alert>
          </Snackbar>
        );
      })}
    </>
  );
}
