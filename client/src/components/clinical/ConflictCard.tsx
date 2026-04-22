// SCR-018 ConflictCard — single conflict item (US_022, AC-2, AC-3).
// Wireframe: .propel/context/wireframes/Hi-Fi/wireframe-SCR-018-conflict-resolution.html
//
// Design spec:
//   border-left: 4px solid error.main (#F44336)
//   source-comparison: 2-col CSS grid → 1-col at ≤ 600px (xs breakpoint)
//   radio options: border 1px divider, radius 4px, padding 16px
//   justification textarea: min-height 80px, resize vertical
//   CTA button: min-height 44px, full width
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  FormControlLabel,
  Radio,
  RadioGroup,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useState } from 'react';

import { type ConflictItemDto, type ResolutionChoice } from '@/api/conflicts';
import { useResolveConflict } from '@/hooks/useResolveConflict';

interface ConflictCardProps {
  conflict: ConflictItemDto;
  patientId: string;
}

const ConflictCard: React.FC<ConflictCardProps> = ({ conflict, patientId }) => {
  const [choice, setChoice] = useState<ResolutionChoice | ''>('');
  const [justification, setJustification] = useState('');
  const [justErr, setJustErr] = useState(false);
  const [resolved, setResolved] = useState(false);

  const { mutate, isPending, isError } = useResolveConflict(patientId);

  const handleChoiceChange = (value: string) => {
    setChoice(value as ResolutionChoice);
    if (value !== 'manual') setJustErr(false);
  };

  const handleResolve = () => {
    if (choice === 'manual' && !justification.trim()) {
      setJustErr(true);
      return;
    }
    mutate(
      {
        view360Id: conflict.view360Id,
        conflictId: conflict.conflictId,
        resolution: choice as ResolutionChoice,
        manualValue: choice === 'manual' ? justification.trim() : undefined,
        justification: justification.trim(),
      },
      {
        onSuccess: () => setResolved(true),
      },
    );
  };

  // ── Resolved state: collapse to green confirmation strip ─────────────────
  if (resolved) {
    return (
      <Card
        sx={{
          borderLeft: '4px solid',
          borderLeftColor: 'success.main',
          mb: 3,
          borderRadius: 2,
          boxShadow: 1,
        }}
      >
        <CardContent>
          <Stack direction="row" alignItems="center" gap={1}>
            <CheckCircleOutlineIcon color="success" />
            <Typography variant="body1" fontWeight={500}>
              {conflict.factType} conflict resolved
            </Typography>
          </Stack>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card
      sx={{
        borderLeft: '4px solid',
        borderLeftColor: 'error.main',
        mb: 3,
        borderRadius: 2,
        boxShadow: 1,
      }}
    >
      <CardContent>
        {/* Header: fact type + unresolved badge */}
        <Stack direction="row" justifyContent="space-between" alignItems="flex-start" mb={2}>
          <Typography variant="h6" fontWeight={500}>
            {conflict.factType} Conflict
          </Typography>
          <Chip label="Unresolved" color="error" size="small" />
        </Stack>

        {/* Side-by-side source comparison (2-col grid → 1-col at ≤ 600px) */}
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' },
            gap: 2,
            mb: 2,
          }}
        >
          {conflict.sources.map((src, i) => (
            <Box
              key={src.documentId}
              sx={{
                bgcolor: 'grey.50',
                p: 2,
                borderRadius: 1,
              }}
            >
              <Typography variant="subtitle2" fontWeight={500} mb={0.5}>
                Source {String.fromCharCode(65 + i)}: {src.documentName}
              </Typography>
              <Typography variant="body2">{src.value}</Typography>
              <Typography variant="caption" color="text.secondary" display="block" mt={0.5}>
                Confidence: {Math.round(src.confidenceScore * 100)}%
              </Typography>
            </Box>
          ))}
        </Box>

        {/* Resolution radio group */}
        <RadioGroup
          value={choice}
          onChange={(e) => handleChoiceChange(e.target.value)}
          aria-label="Resolution choice"
        >
          {conflict.sources.map((src, i) => (
            <FormControlLabel
              key={src.documentId}
              value={i === 0 ? 'sourceA' : 'sourceB'}
              control={<Radio size="small" />}
              label={`Accept Source ${String.fromCharCode(65 + i)}: ${src.value}`}
              sx={{
                border: 1,
                borderColor: 'divider',
                borderRadius: 1,
                px: 2,
                mb: 1,
                '&:hover': { bgcolor: 'grey.50' },
              }}
            />
          ))}
          <FormControlLabel
            value="manual"
            control={<Radio size="small" />}
            label="Manual override (enter correct value)"
            sx={{
              border: 1,
              borderColor: 'divider',
              borderRadius: 1,
              px: 2,
              mb: 1,
              '&:hover': { bgcolor: 'grey.50' },
            }}
          />
        </RadioGroup>

        {/* Justification textarea — required for manual override */}
        <TextField
          multiline
          minRows={2}
          fullWidth
          placeholder={
            choice === 'manual'
              ? 'Enter the correct value and justification (required)'
              : 'Justification for resolution (optional)'
          }
          value={justification}
          onChange={(e) => {
            setJustification(e.target.value);
            if (justErr && e.target.value.trim()) setJustErr(false);
          }}
          error={justErr}
          helperText={justErr ? 'Justification is required for manual override' : ''}
          inputProps={{ 'aria-label': 'Resolution justification' }}
          sx={{ mt: 1, mb: 2 }}
        />

        {/* API error feedback */}
        {isError && (
          <Alert severity="error" sx={{ mb: 2 }}>
            Failed to submit resolution. Please try again.
          </Alert>
        )}

        {/* Per-card resolve button */}
        <Button
          variant="contained"
          fullWidth
          disabled={!choice || isPending}
          onClick={handleResolve}
          aria-label={`Resolve ${conflict.factType} conflict`}
          sx={{ minHeight: 44 }}
        >
          {isPending ? 'Resolving…' : 'Resolve this conflict'}
        </Button>
      </CardContent>
    </Card>
  );
};

export default ConflictCard;
