import { useToastStore } from '@/stores/toast-store';

/**
 * Imperative toast API for use inside React components and mutation callbacks.
 *
 * Usage:
 * ```tsx
 * const { showSuccess, showError } = useToast();
 * showSuccess('Appointment booked successfully');
 * showError('Failed to book appointment', () => retryMutation());
 * ```
 *
 * Severity / auto-dismiss mapping (AC-2, AC-3):
 * - success  → 5 000ms auto-dismiss (UXR-402)
 * - error    → persistent until manually dismissed (UXR-402 AC-3)
 * - info     → 5 000ms auto-dismiss
 * - warning  → 8 000ms auto-dismiss (longer — user may need to act)
 */
export function useToast() {
  const addToast = useToastStore((s) => s.addToast);

  return {
    showSuccess: (message: string) =>
      addToast({ message, severity: 'success', autoDismissMs: 5000 }),

    showError: (message: string, retryFn?: () => void) =>
      addToast({ message, severity: 'error', autoDismissMs: null, retryFn }),

    showInfo: (message: string) =>
      addToast({ message, severity: 'info', autoDismissMs: 5000 }),

    showWarning: (message: string) =>
      addToast({ message, severity: 'warning', autoDismissMs: 8000 }),
  };
}
