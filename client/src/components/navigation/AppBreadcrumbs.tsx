import { Breadcrumbs, Link, Typography, Box } from '@mui/material';
import { Link as RouterLink, useMatches } from 'react-router-dom';

/** Custom route handle shape for breadcrumb label metadata. */
export interface CrumbHandle {
  crumb?: string;
}

/** Extends React Router's match type with typed `handle`. */
export type RouteMatchWithHandle = ReturnType<typeof useMatches>[number] & {
  handle: CrumbHandle;
};

/**
 * Auto-generated breadcrumb navigation (US_040 AC-1, AC-5 / UXR-002).
 *
 * Reads `handle.crumb` from each active React Router v6 match (set via route `handle`
 * in `createBrowserRouter`). Renders nothing when fewer than 2 crumb-bearing matches
 * exist — breadcrumbs only appear when the user is more than one level deep (AC-1).
 *
 * **Ellipsis collapse**: MUI `<Breadcrumbs maxItems={4}>` auto-collapses intermediate
 * levels when > 4 segments, showing first 1 + "..." + last 2 (AC edge case requirement).
 *
 * **Accessibility** (WCAG 2.4.8 Location):
 * - Outer `<Box component="nav" aria-label="breadcrumb">` provides nav landmark.
 * - MUI renders `<ol>` internally for the breadcrumb list.
 * - The last crumb is plain `Typography` (not a link); MUI does not add `aria-current="page"`
 *   automatically, so it is added explicitly.
 *
 * **Colour** (WCAG 1.4.3 Contrast): parent links use `color="primary.dark"` (#1565C0),
 * which achieves a 5.9:1 contrast ratio against white — exceeds the 4.5:1 AA threshold.
 */
export function AppBreadcrumbs() {
  const matches = useMatches() as RouteMatchWithHandle[];

  // Keep only routes that have declared a crumb label in their handle
  const crumbs = matches.filter((m) => typeof m.handle?.crumb === 'string');

  // AC-1: only render when user is more than one level deep
  if (crumbs.length <= 1) return null;

  return (
    <Box
      component="nav"
      aria-label="breadcrumb"
      sx={{ px: { xs: 2, sm: 3, md: 3 }, py: 1 }}
    >
      <Breadcrumbs
        maxItems={4}
        itemsBeforeCollapse={1}
        itemsAfterCollapse={2}
        aria-label="breadcrumb"
      >
        {crumbs.map((match, index) => {
          const isLast = index === crumbs.length - 1;

          if (isLast) {
            // Current page — plain text, not a link; aria-current for a11y
            return (
              <Typography
                key={match.pathname}
                color="text.primary"
                aria-current="page"
                variant="body2"
              >
                {match.handle.crumb}
              </Typography>
            );
          }

          return (
            <Link
              key={match.pathname}
              component={RouterLink}
              to={match.pathname}
              color="primary.dark"
              underline="hover"
              variant="body2"
            >
              {match.handle.crumb}
            </Link>
          );
        })}
      </Breadcrumbs>
    </Box>
  );
}
