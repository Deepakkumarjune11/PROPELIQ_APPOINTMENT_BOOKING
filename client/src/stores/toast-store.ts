import { nanoid } from 'nanoid';
import { create } from 'zustand';

export type ToastSeverity = 'success' | 'error' | 'info' | 'warning';

export interface Toast {
  id: string;
  message: string;
  severity: ToastSeverity;
  /** null = persistent (error toasts); number = auto-dismiss delay in ms */
  autoDismissMs: number | null;
  /** Optional retry callback rendered as a "Retry" button on error toasts (AC-3) */
  retryFn?: () => void;
}

/** Maximum toast queue depth — prevents unbounded memory growth (OWASP A03) */
const MAX_QUEUE_DEPTH = 10;
/** Maximum message length — truncate API-sourced strings before storing (OWASP A03) */
const MAX_MESSAGE_LENGTH = 200;

interface ToastState {
  queue: Toast[];
  /** True when the browser tab is hidden (Page Visibility API edge case) */
  paused: boolean;
}

interface ToastActions {
  addToast: (toast: Omit<Toast, 'id'>) => void;
  dismissToast: (id: string) => void;
  dismissAll: () => void;
}

export const useToastStore = create<ToastState & ToastActions>((set) => {
  // Bind Page Visibility API listener — SSR-safe guard (AC edge case)
  if (typeof document !== 'undefined') {
    document.addEventListener('visibilitychange', () => {
      set({ paused: document.hidden });
    });
  }

  return {
    queue: [],
    paused: false,

    addToast: (toast) =>
      set((state) => {
        // OWASP A03 — truncate externally-sourced message strings before storage
        const safeMessage =
          toast.message.length > MAX_MESSAGE_LENGTH
            ? toast.message.slice(0, MAX_MESSAGE_LENGTH - 3) + '...'
            : toast.message;

        const newToast: Toast = { ...toast, message: safeMessage, id: nanoid(6) };

        // Enforce max queue depth — drop oldest overflow toasts (FIFO)
        const updatedQueue =
          state.queue.length >= MAX_QUEUE_DEPTH
            ? [...state.queue.slice(1), newToast]
            : [...state.queue, newToast];

        return { queue: updatedQueue };
      }),

    dismissToast: (id) =>
      set((state) => ({ queue: state.queue.filter((t) => t.id !== id) })),

    dismissAll: () => set({ queue: [] }),
  };
});
