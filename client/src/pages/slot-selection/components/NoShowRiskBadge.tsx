// No-show risk badge — rendered when slot.noShowRisk > 0.7.
// Design token: warning.main (#FF9800) per designsystem.md.
// UXR-003: Tooltip provides contributing factors list; UXR-404: partial-scoring footer note.
import WarningAmberOutlinedIcon from '@mui/icons-material/WarningAmberOutlined';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';

const NO_SHOW_THRESHOLD = 0.7;

interface NoShowRiskBadgeProps {
  noShowRisk?: number;
  /** Human-readable contributing factor strings returned by the availability API (AC-2). */
  riskContributingFactors?: string[];
  /** When true, renders an italic footer: "Partial scoring — some signals unavailable". */
  isPartialScoring?: boolean;
}

export default function NoShowRiskBadge({
  noShowRisk,
  riskContributingFactors,
  isPartialScoring,
}: NoShowRiskBadgeProps) {
  if (!noShowRisk || noShowRisk <= NO_SHOW_THRESHOLD) {
    return null;
  }

  const factors = riskContributingFactors ?? [];

  const tooltipContent = (
    <Box sx={{ maxWidth: 280 }}>
      <Typography variant="caption" fontWeight="bold" display="block">
        Risk factors:
      </Typography>
      {factors.length > 0 ? (
        <Box component="ul" sx={{ m: '4px 0', pl: 2 }}>
          {factors.map((factor, i) => (
            <li key={i}>
              <Typography variant="caption">{factor}</Typography>
            </li>
          ))}
        </Box>
      ) : (
        <Typography variant="caption" display="block" sx={{ mt: 0.5 }}>
          High no-show probability based on scheduling signals.
        </Typography>
      )}
      {isPartialScoring && (
        <Typography
          variant="caption"
          fontStyle="italic"
          color="text.secondary"
          display="block"
          sx={{ mt: 0.5 }}
        >
          Partial scoring — some signals unavailable
        </Typography>
      )}
    </Box>
  );

  return (
    <Tooltip title={tooltipContent} arrow placement="top">
      <Chip
        icon={<WarningAmberOutlinedIcon />}
        label="High no-show risk detected"
        size="small"
        sx={{ bgcolor: 'warning.main', color: 'warning.contrastText', cursor: 'default' }}
        aria-label="High no-show risk detected for this slot"
      />
    </Tooltip>
  );
}
