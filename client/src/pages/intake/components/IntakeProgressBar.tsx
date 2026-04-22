// MUI LinearProgress showing how many required intake questions have been answered.
// Renders "X of Y required questions answered" caption below the bar.
import Box from '@mui/material/Box';
import LinearProgress from '@mui/material/LinearProgress';
import Typography from '@mui/material/Typography';

interface IntakeProgressBarProps {
  answeredCount: number;
  totalRequired: number;
}

export default function IntakeProgressBar({ answeredCount, totalRequired }: IntakeProgressBarProps) {
  const value = totalRequired > 0 ? (answeredCount / totalRequired) * 100 : 0;

  return (
    <Box sx={{ mb: 3 }}>
      <LinearProgress
        variant="determinate"
        value={value}
        aria-label={`Intake progress: ${answeredCount} of ${totalRequired} required questions answered`}
        sx={{ height: 4, borderRadius: 2 }}
      />
      <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
        {answeredCount} of {totalRequired} required questions answered
      </Typography>
    </Box>
  );
}
