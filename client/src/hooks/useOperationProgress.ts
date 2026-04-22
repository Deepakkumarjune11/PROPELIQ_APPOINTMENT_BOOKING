import { useCallback, useEffect, useRef, useState } from 'react';

/** Milliseconds of no progress update before the operation is considered stalled (AC-4 edge case). */
const STALL_THRESHOLD_MS = 30_000;

/** Interval at which stall detection checks elapsed time since last progress update. */
const STALL_POLL_INTERVAL_MS = 5_000;

/** Minimum progress percentage before ETA estimation is attempted (< 5% is too noisy). */
const ETA_MIN_PROGRESS = 5;

interface UseOperationProgressReturn {
  /** Current progress value, 0–100. */
  progress: number;
  /** Whether the operation has stalled (no update for STALL_THRESHOLD_MS). */
  isStalled: boolean;
  /**
   * Estimated seconds remaining. `undefined` until `progress > 5` and elapsed time is known.
   * Recomputed on every render from current progress and elapsed ms.
   */
  estimatedSecondsRemaining: number | undefined;
  /** Call with the new progress value (0–100) on each progress event. Resets stall detection. */
  updateProgress: (newProgress: number) => void;
  /** Resets all state to initial values (call on operation completion or cancellation). */
  resetProgress: () => void;
}

/**
 * Manages progress state for long-running operations (US_038 AC-4).
 *
 * Provides stall detection (`STALL_THRESHOLD_MS = 30s`) and ETA estimation for use
 * with `OperationProgressBar`.
 *
 * @example
 * ```tsx
 * const { progress, isStalled, estimatedSecondsRemaining, updateProgress, resetProgress } =
 *   useOperationProgress();
 *
 * // Drive from XHR upload.onprogress:
 * xhr.upload.onprogress = (e) => updateProgress((e.loaded / e.total) * 100);
 *
 * return (
 *   <OperationProgressBar
 *     progress={progress}
 *     isStalled={isStalled}
 *     estimatedSecondsRemaining={estimatedSecondsRemaining}
 *     onCancel={() => { xhr.abort(); resetProgress(); }}
 *   />
 * );
 * ```
 */
export function useOperationProgress(): UseOperationProgressReturn {
  const [progress, setProgress] = useState(0);
  const [isStalled, setIsStalled] = useState(false);

  // Use refs for timing values to avoid stale closures in the setInterval callback
  const startedAtRef = useRef<number | null>(null);
  const lastProgressAtRef = useRef<number | null>(null);

  // Derive ETA from current progress and elapsed time (computed — no extra state)
  let estimatedSecondsRemaining: number | undefined;
  if (progress > ETA_MIN_PROGRESS && startedAtRef.current !== null) {
    const elapsedMs = Date.now() - startedAtRef.current;
    const ratePerMs = progress / elapsedMs; // % per ms
    if (ratePerMs > 0) {
      estimatedSecondsRemaining = Math.round(((100 - progress) / ratePerMs) / 1000);
    }
  }

  const updateProgress = useCallback((newProgress: number) => {
    const now = Date.now();
    if (startedAtRef.current === null) {
      startedAtRef.current = now;
    }
    lastProgressAtRef.current = now;
    setProgress(newProgress);
    setIsStalled(false);
  }, []);

  const resetProgress = useCallback(() => {
    setProgress(0);
    setIsStalled(false);
    startedAtRef.current = null;
    lastProgressAtRef.current = null;
  }, []);

  // Stall detection — poll every STALL_POLL_INTERVAL_MS while operation is in-flight
  useEffect(() => {
    // Only poll when operation is active (started and not yet complete)
    if (progress <= 0 || progress >= 100) return;

    const id = setInterval(() => {
      if (
        lastProgressAtRef.current !== null &&
        Date.now() - lastProgressAtRef.current > STALL_THRESHOLD_MS
      ) {
        setIsStalled(true);
      }
    }, STALL_POLL_INTERVAL_MS);

    return () => clearInterval(id);
  }, [progress]);

  return {
    progress,
    isStalled,
    estimatedSecondsRemaining,
    updateProgress,
    resetProgress,
  };
}
