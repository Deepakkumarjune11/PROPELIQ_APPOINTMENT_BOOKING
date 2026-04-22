import type React from 'react';

interface LiveRegionProps {
  /** The message to announce to screen readers. Set to empty string to clear. */
  message: string;
  /** `assertive` interrupts the user immediately (errors). `polite` waits for a pause (status). */
  politeness?: 'polite' | 'assertive';
  /** When `true`, the entire region content is read as a unit on each update. */
  atomic?: boolean;
}

/**
 * Visually hidden ARIA live region — announces dynamic content to screen readers
 * without any visible output (WCAG 4.1.3 Status Messages, AC-4/AC-2).
 *
 * Usage:
 * - Form errors: `politeness="assertive"` — interrupts current reading immediately
 * - Slot availability updates: `politeness="polite"` — waits for a natural pause
 *
 * The clip technique used here is the modern replacement for `display:none` +
 * `visibility:hidden` (which would hide content from AT as well).
 */
const visuallyHidden: React.CSSProperties = {
  position: 'absolute',
  width: '1px',
  height: '1px',
  padding: 0,
  margin: '-1px',
  overflow: 'hidden',
  clip: 'rect(0,0,0,0)',
  whiteSpace: 'nowrap',
  borderWidth: 0,
};

export default function LiveRegion({
  message,
  politeness = 'polite',
  atomic = true,
}: LiveRegionProps) {
  return (
    <div
      aria-live={politeness}
      aria-atomic={atomic}
      style={visuallyHidden}
    >
      {message}
    </div>
  );
}
