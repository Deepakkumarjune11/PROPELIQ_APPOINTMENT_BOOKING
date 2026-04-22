// FactCard — SCR-017 fact grid card (US_021, AC-1, AC-3, AC-5).
// Left border colour per category from FACT_CATEGORY_COLORS (UXR-303).
// Confidence badge: blue (primary) ≥ 70%, orange (warning) < 70% per wireframe.
// Citation IconButton visible only when sourceCharOffset is present (AIR-006).
import DescriptionIcon from '@mui/icons-material/Description';
import { Card, CardContent, Chip, IconButton, Stack, Typography } from '@mui/material';

import { type ConsolidatedFact } from '@/api/patientView360';
import { FACT_CATEGORY_COLORS } from '@/theme/healthcare-theme';

interface FactCardProps {
  fact: ConsolidatedFact;
  onCiteClick: (factId: string) => void;
}

const FactCard: React.FC<FactCardProps> = ({ fact, onCiteClick }) => {
  const borderColor = FACT_CATEGORY_COLORS[fact.factType] ?? '#9E9E9E';
  const isHighConfidence = fact.confidenceScore >= 0.7;
  const primarySource = fact.sources[0];

  return (
    <Card
      sx={{
        borderLeft: `4px solid ${borderColor}`,
        mb: 2,
        boxShadow: 1,
        borderRadius: 1,
      }}
    >
      <CardContent>
        <Stack direction="row" justifyContent="space-between" alignItems="flex-start">
          <Typography variant="subtitle1" fontWeight={500} color="text.primary">
            {fact.value}
          </Typography>
          <Chip
            label={`${Math.round(fact.confidenceScore * 100)}%`}
            size="small"
            color={isHighConfidence ? 'primary' : 'warning'}
            sx={{ ml: 1, flexShrink: 0 }}
          />
        </Stack>

        <Stack direction="row" alignItems="center" mt={1} gap={0.5}>
          <Typography variant="caption" color="text.secondary">
            {fact.sources.length} source{fact.sources.length !== 1 ? 's' : ''}
          </Typography>
          {primarySource?.sourceCharOffset != null && (
            <IconButton
              size="small"
              aria-label="View source citation"
              title={`View source in ${primarySource.documentName}`}
              onClick={() => onCiteClick(fact.factId)}
              color="primary"
            >
              <DescriptionIcon fontSize="small" />
            </IconButton>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
};

export default FactCard;
