import { Skeleton, TableBody, TableCell, TableRow } from '@mui/material';

/**
 * Static width percentages per cell position — pre-computed to avoid `Math.random()` in render.
 * Deterministic values prevent hydration mismatches and cumulative layout shift (CLS = 0).
 * Pattern repeats every 5 columns; extended for up to 10 columns.
 */
const CELL_WIDTHS = [70, 85, 60, 90, 75, 65, 80, 55, 88, 72];

interface TableRowSkeletonProps {
  /** Number of columns to render per row. Default: 5. */
  columns?: number;
  /** Number of skeleton rows to render. Default: 5. */
  rows?: number;
}

/**
 * Table body skeleton — used in:
 * - SCR-010 (queue table)
 * - SCR-012 (same-day queue)
 * - SCR-016 (patient chart review)
 * - SCR-021 (user management table)
 *
 * Renders a `<TableBody>` so the component can be dropped directly into a `<Table>` / `<TableContainer>`.
 * Cell widths are deterministic (no Math.random()) to avoid CLS and hydration issues.
 */
export function TableRowSkeleton({ columns = 5, rows = 5 }: TableRowSkeletonProps) {
  const rowItems = Array.from({ length: rows }, (_, r) => r);
  const colItems = Array.from({ length: columns }, (_, c) => c);

  return (
    <TableBody>
      {rowItems.map((r) => (
        <TableRow key={r}>
          {colItems.map((c) => (
            <TableCell key={c}>
              <Skeleton
                variant="text"
                width={`${CELL_WIDTHS[c % CELL_WIDTHS.length]}%`}
                sx={{ fontSize: '0.875rem' }}
              />
            </TableCell>
          ))}
        </TableRow>
      ))}
    </TableBody>
  );
}
