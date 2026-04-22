/**
 * Skeleton loader components — reusable, layout-matched loading states (US_038 AC-1).
 *
 * All skeletons use MUI `Skeleton` with `animation="wave"` by default.
 * When `prefers-reduced-motion: reduce` is active the global `MuiSkeleton` theme
 * override in `healthcare-theme.ts` disables animation (WCAG 2.3.3).
 *
 * No `useEffect` or `setTimeout` delay — skeletons render synchronously on first
 * render, guaranteeing <100ms display (UXR-401, AC-1).
 */

/** Used in SCR-010 summary cards, SCR-008 appointment cards, SCR-028 metric tiles. */
export { CardSkeleton } from './CardSkeleton';

/** Used in SCR-010 queue table, SCR-012 same-day queue, SCR-016 chart review, SCR-021 user management. */
export { TableRowSkeleton } from './TableRowSkeleton';

/** Used in SCR-003 patient details, SCR-011 walk-in booking form, SCR-022 create/edit user. */
export { FormSkeleton } from './FormSkeleton';

/** Used in SCR-017 360-degree patient view, SCR-019 code verification panel. */
export { DetailViewSkeleton } from './DetailViewSkeleton';
