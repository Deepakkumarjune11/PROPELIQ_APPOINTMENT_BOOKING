// CodeSuggestionCard — ICD-10/CPT code card with evidence breadcrumbs and Accept/Reject/Modify
// actions (US_023, SCR-019, FR-014, AIR-005).
//
// Badge colours sourced from FACT_CATEGORY_COLORS (designsystem.md#healthcare-specific-colors):
//   ICD-10 → Diagnoses #673AB7 (deep purple)
//   CPT    → Procedures #009688 (teal)
//
// Accept/Reject validation: Reject requires non-empty justification (UC-005 edge case).
// Reviewed state: collapses to colour-coded chip row (green accepted / red rejected).
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
import EditIcon from '@mui/icons-material/Edit';
import {
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import { useState } from 'react';

import { type CodeSuggestionDto } from '@/api/codeSuggestions';
import { useConfirmCode } from '@/hooks/useConfirmCode';
import { FACT_CATEGORY_COLORS } from '@/theme/healthcare-theme';

interface CodeSuggestionCardProps {
  code: CodeSuggestionDto;
  patientId: string;
  onEvidenceClick: (factId: string) => void;
}

const CodeSuggestionCard: React.FC<CodeSuggestionCardProps> = ({
  code,
  patientId,
  onEvidenceClick,
}) => {
  const { mutate, isPending } = useConfirmCode(patientId);
  const [showRejectField, setShowRejectField] = useState(false);
  const [justification, setJustification] = useState('');
  const [justErr, setJustErr] = useState(false);

  // ICD-10 → Diagnoses purple; CPT → Procedures teal (designsystem.md#healthcare-specific-colors)
  const badgeColor =
    code.codeType === 'ICD-10'
      ? FACT_CATEGORY_COLORS['Diagnoses']
      : FACT_CATEGORY_COLORS['Procedures'];

  const handleRejectClick = () => {
    if (!showRejectField) {
      setShowRejectField(true);
      return;
    }
    if (!justification.trim()) {
      setJustErr(true);
      return;
    }
    mutate({
      codeId: code.id,
      reviewOutcome: 'rejected',
      justification: justification.trim(),
    });
  };

  // ── Reviewed state: collapsed colour-coded row ──────────────────────────
  if (code.staffReviewed) {
    const accepted = code.reviewOutcome === 'accepted';
    return (
      <Card
        sx={{
          border: 1,
          borderColor: accepted ? 'success.main' : 'error.main',
          borderRadius: 2,
          opacity: 0.85,
        }}
      >
        <CardContent sx={{ py: 2, '&:last-child': { pb: 2 } }}>
          <Stack direction="row" justifyContent="space-between" alignItems="center">
            <Box>
              <Typography variant="subtitle1" fontWeight={500}>
                {code.code} — {code.description}
              </Typography>
              <Chip
                label={code.codeType}
                size="small"
                sx={{ mt: 0.5, bgcolor: badgeColor, color: '#FFF', fontWeight: 500 }}
              />
            </Box>
            <Chip
              label={accepted ? 'Accepted' : 'Rejected'}
              color={accepted ? 'success' : 'error'}
              size="small"
            />
          </Stack>
        </CardContent>
      </Card>
    );
  }

  // ── Active card: evidence chips + action buttons ─────────────────────────
  return (
    <Card
      sx={{
        border: '1px solid',
        borderColor: 'divider',
        borderRadius: 2,
        boxShadow: 1,
      }}
    >
      <CardContent>
        {/* Code title + type badge */}
        <Stack direction="row" justifyContent="space-between" alignItems="flex-start" mb={2}>
          <Box>
            <Typography variant="h6" component="div">
              {code.code} — {code.description}
            </Typography>
            <Chip
              label={code.codeType}
              size="small"
              sx={{ mt: 0.5, bgcolor: badgeColor, color: '#FFF', fontWeight: 500 }}
              aria-label={`Code type: ${code.codeType}`}
            />
          </Box>
          <Typography variant="caption" color="text.secondary">
            {Math.round(code.confidenceScore * 100)}% confidence
          </Typography>
        </Stack>

        {/* Evidence breadcrumb chips (AIR-005 / AC-2) */}
        {code.evidenceFacts.length > 0 && (
          <Box mb={2}>
            <Typography
              variant="caption"
              color="text.secondary"
              fontWeight={500}
              display="block"
              mb={0.5}
            >
              Evidence trail:
            </Typography>
            <Box display="flex" flexWrap="wrap" gap={1}>
              {code.evidenceFacts.map((fact) => (
                <Chip
                  key={fact.factId}
                  label={fact.factSummary}
                  size="small"
                  onClick={() => onEvidenceClick(fact.factId)}
                  sx={{
                    cursor: 'pointer',
                    bgcolor: 'grey.300',
                    color: 'text.primary',
                    '&:hover': { bgcolor: 'primary.main', color: '#FFF' },
                  }}
                  aria-label={`View evidence: ${fact.factSummary}`}
                />
              ))}
            </Box>
          </Box>
        )}

        {/* Inline justification field — shown when Reject is clicked (UC-005) */}
        {showRejectField && (
          <TextField
            multiline
            minRows={2}
            fullWidth
            placeholder="Justification for rejection (required)"
            value={justification}
            onChange={(e) => {
              setJustification(e.target.value);
              setJustErr(false);
            }}
            error={justErr}
            helperText={justErr ? 'Justification is required to reject a code' : ''}
            sx={{ mb: 2 }}
            inputProps={{ 'aria-label': 'Rejection justification' }}
          />
        )}

        {/* Action buttons: Accept / Reject / Modify */}
        <Stack
          direction={{ xs: 'column', sm: 'row' }}
          gap={1}
        >
          <Tooltip title={showRejectField ? 'Cancel accept after starting rejection' : ''}>
            <Button
              variant="contained"
              color="success"
              startIcon={<CheckIcon />}
              disabled={isPending || showRejectField}
              onClick={() => mutate({ codeId: code.id, reviewOutcome: 'accepted' })}
              sx={{ minHeight: 44 }}
              aria-label={`Accept code ${code.code}`}
            >
              Accept
            </Button>
          </Tooltip>

          <Button
            variant="outlined"
            startIcon={showRejectField ? <CloseIcon /> : <CloseIcon />}
            disabled={isPending}
            onClick={handleRejectClick}
            sx={{ minHeight: 44 }}
            aria-label={
              showRejectField ? `Confirm reject code ${code.code}` : `Reject code ${code.code}`
            }
          >
            {showRejectField ? 'Confirm reject' : 'Reject'}
          </Button>

          {showRejectField && (
            <Button
              variant="text"
              disabled={isPending}
              onClick={() => {
                setShowRejectField(false);
                setJustification('');
                setJustErr(false);
              }}
              sx={{ minHeight: 44 }}
            >
              Cancel
            </Button>
          )}

          {!showRejectField && (
            <Button
              variant="outlined"
              color="primary"
              startIcon={<EditIcon />}
              disabled={isPending}
              sx={{ minHeight: 44 }}
              aria-label={`Modify code ${code.code}`}
            >
              Modify
            </Button>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
};

export default CodeSuggestionCard;
