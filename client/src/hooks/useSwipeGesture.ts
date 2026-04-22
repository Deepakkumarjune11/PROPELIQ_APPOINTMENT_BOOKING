import { useCallback, useRef } from 'react';

interface UseSwipeGestureOptions {
  /** Minimum horizontal pixel distance to qualify as a swipe. Default: 50px. */
  threshold?: number;
  /** Called when the user swipes from right to left (towards left). */
  onSwipeLeft?: () => void;
  /** Called when the user swipes from left to right (towards right). */
  onSwipeRight?: () => void;
}

interface SwipeHandlers {
  onTouchStart: (e: React.TouchEvent) => void;
  onTouchEnd: (e: React.TouchEvent) => void;
}

/**
 * Detects horizontal swipe gestures on the element that receives these event handlers.
 *
 * Spread the returned handlers onto any element:
 * ```tsx
 * const swipe = useSwipeGesture({ onSwipeRight: openDrawer });
 * <Box {...swipe}>...</Box>
 * ```
 *
 * Non-touch environments (desktop): returns no-op handlers so no listener overhead.
 * SSR-safe: `window` guard prevents crashes in non-browser builds (AC-4 edge case).
 */
export function useSwipeGesture({
  threshold = 50,
  onSwipeLeft,
  onSwipeRight,
}: UseSwipeGestureOptions = {}): SwipeHandlers {
  const touchStartX = useRef<number>(0);

  // Always call hooks before any early return (Rules of Hooks)
  const onTouchStart = useCallback((e: React.TouchEvent) => {
    touchStartX.current = e.touches[0].clientX;
  }, []);

  const onTouchEnd = useCallback(
    (e: React.TouchEvent) => {
      const delta = e.changedTouches[0].clientX - touchStartX.current;
      if (Math.abs(delta) < threshold) return;
      if (delta > 0) {
        onSwipeRight?.();
      } else {
        onSwipeLeft?.();
      }
    },
    [threshold, onSwipeLeft, onSwipeRight],
  );

  // Non-touch environment: return no-op handlers (no native touch events available)
  const isTouchCapable =
    typeof window !== 'undefined' && 'ontouchstart' in window;

  if (!isTouchCapable) {
    // eslint-disable-next-line @typescript-eslint/no-empty-function
    const noop = () => {};
    return { onTouchStart: noop, onTouchEnd: noop };
  }

  return { onTouchStart, onTouchEnd };
}
