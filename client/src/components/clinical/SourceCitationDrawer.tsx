// SourceCitationDrawer — right-slide MUI Drawer for character-level source citation (US_021, AC-3).
// XSS-safe: all source text is HTML-escaped before inserting the <mark> tag.
// Offset guards: Math.max/Math.min prevent out-of-range slices from crashing.
import CloseIcon from '@mui/icons-material/Close';
import { Box, Drawer, IconButton, Skeleton, Typography } from '@mui/material';

import { type SourceCitationDto } from '@/api/patientView360';

interface SourceCitationDrawerProps {
  open: boolean;
  onClose: () => void;
  citation: SourceCitationDto | undefined;
  isFetching: boolean;
}

/**
 * Escapes HTML special characters to prevent XSS when injecting source text into innerHTML.
 * Only the un-highlighted portions are escaped; the <mark> tag itself is safe (no user data).
 */
function escapeHtml(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

/**
 * Builds the innerHTML string with a <mark> highlight around the cited character range.
 * Offset-out-of-range guard: clamps start/end to [0, sourceText.length].
 */
function buildHighlightedText({
  sourceText,
  sourceCharOffset,
  sourceCharLength,
}: SourceCitationDto): string {
  const start = Math.max(0, sourceCharOffset);
  const end = Math.min(sourceText.length, sourceCharOffset + sourceCharLength);

  // If start >= end the citation offsets are malformed — show full text, no highlight
  if (start >= end) {
    return escapeHtml(sourceText);
  }

  return (
    escapeHtml(sourceText.slice(0, start)) +
    '<mark style="background:rgba(33,150,243,0.2);padding:2px 4px;border-radius:2px">' +
    escapeHtml(sourceText.slice(start, end)) +
    '</mark>' +
    escapeHtml(sourceText.slice(end))
  );
}

const SourceCitationDrawer: React.FC<SourceCitationDrawerProps> = ({
  open,
  onClose,
  citation,
  isFetching,
}) => {
  const highlighted = citation ? buildHighlightedText(citation) : null;

  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
      PaperProps={{ sx: { width: { xs: '100%', sm: 500 }, display: 'flex', flexDirection: 'column' } }}
    >
      {/* Header */}
      <Box
        display="flex"
        justifyContent="space-between"
        alignItems="center"
        px={3}
        py={2}
        borderBottom={1}
        borderColor="divider"
      >
        <Typography variant="h6">
          Source citation{citation ? ` — ${citation.documentName}` : ''}
        </Typography>
        <IconButton onClick={onClose} aria-label="Close citation drawer" edge="end">
          <CloseIcon />
        </IconButton>
      </Box>

      {/* Body */}
      <Box flex={1} overflow="auto" p={3}>
        {isFetching && <Skeleton variant="rectangular" height={200} sx={{ borderRadius: 1 }} />}

        {!isFetching && citation && (
          <>
            <Box mb={2}>
              <Typography variant="body2" fontWeight={500} color="text.primary">
                Document: {citation.documentName}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Uploaded: {new Date(citation.uploadedAt).toLocaleDateString()}
                {' • '}
                AI confidence: {Math.round(citation.confidenceScore * 100)}%
              </Typography>
            </Box>

            {/* Monospace source text with <mark> highlight — innerHTML is XSS-safe (escaped above) */}
            <Box
              component="pre"
              sx={{
                fontFamily: "'Courier New', monospace",
                fontSize: 14,
                whiteSpace: 'pre-wrap',
                wordBreak: 'break-word',
                bgcolor: 'grey.50',
                p: 2,
                border: 1,
                borderColor: 'divider',
                borderRadius: 1,
                m: 0,
              }}
              dangerouslySetInnerHTML={{ __html: highlighted ?? '' }}
            />
          </>
        )}

        {!isFetching && !citation && open && (
          <Typography variant="body2" color="text.secondary">
            Unable to load source text. Please try again.
          </Typography>
        )}
      </Box>
    </Drawer>
  );
};

export default SourceCitationDrawer;
