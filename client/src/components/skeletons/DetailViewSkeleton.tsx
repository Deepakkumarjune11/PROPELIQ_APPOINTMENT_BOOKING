import { Box, Divider, Skeleton } from '@mui/material';

interface DetailViewSkeletonProps {
  /** Number of content sections (each with a heading + fact rows). Default: 3. */
  sections?: number;
  /** Number of label+value fact rows per section. Default: 4. */
  factsPerSection?: number;
}

/**
 * Detail view / 360-panel skeleton — used in:
 * - SCR-017 (360-degree patient view)
 * - SCR-019 (code verification panel)
 *
 * Each section renders a heading skeleton followed by two-column (label + value) fact rows.
 * Section spacing (32px = spacing: 4) matches the detail panel section gap design token.
 */
export function DetailViewSkeleton({ sections = 3, factsPerSection = 4 }: DetailViewSkeletonProps) {
  const sectionItems = Array.from({ length: sections }, (_, i) => i);
  const factItems = Array.from({ length: factsPerSection }, (_, i) => i);

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
      {sectionItems.map((s) => (
        <Box key={s}>
          {/* Section heading — 25% width matches typical short section title */}
          <Skeleton
            variant="text"
            width="25%"
            sx={{ fontSize: '1.125rem', mb: 1 }}
          />
          <Divider sx={{ mb: 1.5 }} />

          {/* Fact rows: label (30%) + value (55%) side-by-side */}
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
            {factItems.map((f) => (
              <Box key={f} sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
                <Skeleton variant="text" width="30%" sx={{ fontSize: '0.875rem' }} />
                <Skeleton variant="text" width="55%" sx={{ fontSize: '0.875rem' }} />
              </Box>
            ))}
          </Box>
        </Box>
      ))}
    </Box>
  );
}
