import { useEffect, useRef, useState } from 'react';

/**
 * Returns a debounced copy of `value` that only updates after `delayMs` milliseconds
 * of inactivity — used by search inputs to prevent excessive re-renders.
 */
export function useDebounce<T>(value: T, delayMs: number): T {
  const [debouncedValue, setDebouncedValue] = useState<T>(value);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (timerRef.current) clearTimeout(timerRef.current);
    timerRef.current = setTimeout(() => setDebouncedValue(value), delayMs);
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [value, delayMs]);

  return debouncedValue;
}
