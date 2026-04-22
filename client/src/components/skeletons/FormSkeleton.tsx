import { Box, Skeleton } from '@mui/material';

interface FormSkeletonProps {
  /** Number of label+input field pairs to render. Default: 4. */
  fields?: number;
}

/**
 * Form section skeleton — used in:
 * - SCR-003 (patient details form)
 * - SCR-011 (walk-in booking form)
 * - SCR-022 (create/edit user)
 *
 * Renders stacked label + input pairs. Input height (56px) matches MUI `OutlinedInput`
 * default height. Border radius (4px) matches `--border-radius-small` design token.
 */
export function FormSkeleton({ fields = 4 }: FormSkeletonProps) {
  const items = Array.from({ length: fields }, (_, i) => i);

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      {items.map((i) => (
        <Box key={i}>
          {/* Field label — 35% width matches typical short label text */}
          <Skeleton
            variant="text"
            width="35%"
            sx={{ fontSize: '0.75rem', mb: 0.5 }}
          />
          {/* Input block — 56px matches MUI OutlinedInput rendered height (AC-1 CLS=0) */}
          <Skeleton
            variant="rectangular"
            height={56}
            width="100%"
            sx={{ borderRadius: '4px' }}
          />
        </Box>
      ))}
    </Box>
  );
}
