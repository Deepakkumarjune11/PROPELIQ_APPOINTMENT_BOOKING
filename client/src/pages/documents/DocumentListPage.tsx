// SCR-015 — Document List (US_018, AC-3, AC-5)
// Status badge table, conditional polling, Skeleton loading, Empty state, delete dialog.
// States: Default (rows), Loading (Skeleton × 3), Empty (CTA), Error (Alert + retry).
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import DescriptionIcon from '@mui/icons-material/Description';
import {
  Alert,
  Box,
  Breadcrumbs,
  Button,
  Chip,
  ChipProps,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  Link,
  Paper,
  Skeleton,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';

import { type ExtractionStatus } from '@/api/documents';
import { useDeleteDocument, useDocuments } from '@/hooks/useDocuments';

// ── Status badge helpers ───────────────────────────────────────────────────────

const STATUS_LABEL: Record<ExtractionStatus, string> = {
  queued:        'Queued',
  processing:    'Processing',
  completed:     'Completed',
  manual_review: 'Manual Review',
  failed:        'Failed',
};

/**
 * Maps `ExtractionStatus` to MUI Chip color + optional sx overrides.
 * Colour tokens sourced from `designsystem.md`:
 * - queued        → info (blue)
 * - processing    → warning (orange)
 * - completed     → success (green)
 * - manual_review → warning-dark (#F57C00 amber)
 * - failed        → error (red)
 */
function getStatusChipProps(status: ExtractionStatus): {
  color: ChipProps['color'];
  sx?: ChipProps['sx'];
} {
  switch (status) {
    case 'queued':
      return { color: 'info' };
    case 'processing':
      return { color: 'warning' };
    case 'completed':
      return { color: 'success' };
    case 'manual_review':
      // warning-dark: #F57C00 (designsystem.md warning.700)
      return { color: 'warning', sx: { backgroundColor: '#F57C00', color: '#fff' } };
    case 'failed':
      return { color: 'error' };
  }
}

// ── Date formatter ─────────────────────────────────────────────────────────────

/** Formats ISO-8601 string as "dd MMM yyyy HH:mm" per task spec. */
function formatUploadDate(iso: string): string {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return iso;

  const day  = String(d.getDate()).padStart(2, '0');
  const mon  = d.toLocaleString('en-GB', { month: 'short' });
  const year = d.getFullYear();
  const hh   = String(d.getHours()).padStart(2, '0');
  const mm   = String(d.getMinutes()).padStart(2, '0');

  return `${day} ${mon} ${year} ${hh}:${mm}`;
}

// ── Delete dialog ─────────────────────────────────────────────────────────────

interface DeleteDialogProps {
  fileName: string;
  open: boolean;
  isDeleting: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

function DeleteConfirmDialog({
  fileName,
  open,
  isDeleting,
  onConfirm,
  onCancel,
}: DeleteDialogProps) {
  return (
    <Dialog open={open} onClose={onCancel} maxWidth="xs" fullWidth>
      <DialogTitle>Delete document</DialogTitle>
      <DialogContent>
        <Typography>
          Are you sure you want to delete <strong>{fileName}</strong>? This action cannot be
          undone.
        </Typography>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={isDeleting}>
          Cancel
        </Button>
        <Button
          onClick={onConfirm}
          color="error"
          variant="contained"
          disabled={isDeleting}
          aria-label="Confirm document deletion"
        >
          {isDeleting ? 'Deleting…' : 'Delete'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ── Skeleton loading rows ─────────────────────────────────────────────────────

function SkeletonRows() {
  return (
    <>
      {[1, 2, 3].map((i) => (
        <TableRow key={i}>
          <TableCell>
            <Skeleton variant="text" width={200} />
          </TableCell>
          <TableCell>
            <Skeleton variant="text" width={140} />
          </TableCell>
          <TableCell>
            <Skeleton variant="rectangular" width={80} height={24} sx={{ borderRadius: 1 }} />
          </TableCell>
          <TableCell>
            <Skeleton variant="circular" width={32} height={32} />
          </TableCell>
        </TableRow>
      ))}
    </>
  );
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function DocumentListPage() {
  const { data: documents, isLoading, isError, refetch } = useDocuments();
  const { mutate: deleteDoc, isLoading: isDeleting } = useDeleteDocument();

  const [pendingDelete, setPendingDelete] = useState<{
    documentId: string;
    fileName: string;
  } | null>(null);

  const handleDeleteClick = (documentId: string, fileName: string) => {
    setPendingDelete({ documentId, fileName });
  };

  const handleDeleteConfirm = () => {
    if (!pendingDelete) return;
    deleteDoc(pendingDelete.documentId, {
      onSettled: () => setPendingDelete(null),
    });
  };

  const handleDeleteCancel = () => setPendingDelete(null);

  // ── Render ──────────────────────────────────────────────────────────────────

  return (
    <Box sx={{ maxWidth: 1200, mx: 'auto' }}>
      {/* Breadcrumb — UXR-002 */}
      <Breadcrumbs sx={{ mb: 2 }}>
        <Link component={RouterLink} to="/" underline="hover" color="inherit">
          Home
        </Link>
        <Typography color="text.primary">My Documents</Typography>
      </Breadcrumbs>

      {/* Page header + upload CTA */}
      <Box
        sx={{
          display: 'flex',
          alignItems: { xs: 'flex-start', sm: 'center' },
          flexDirection: { xs: 'column', sm: 'row' },
          justifyContent: 'space-between',
          gap: 2,
          mb: 3,
        }}
      >
        <Typography variant="h4" component="h1">
          My Documents
        </Typography>
        <Button
          component={RouterLink}
          to="/documents/upload"
          variant="contained"
          aria-label="Upload a new document"
        >
          Upload Document
        </Button>
      </Box>

      {/* Error state */}
      {isError && (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => void refetch()}>
              Retry
            </Button>
          }
          sx={{ mb: 3 }}
          role="alert"
        >
          Failed to load documents. Please try again.
        </Alert>
      )}

      {/* Empty state */}
      {!isLoading && !isError && documents?.length === 0 && (
        <Paper
          sx={{
            p: 6,
            textAlign: 'center',
            border: '1px dashed',
            borderColor: 'divider',
          }}
        >
          <DescriptionIcon sx={{ fontSize: 56, color: 'text.disabled', mb: 2 }} />
          <Typography variant="h6" gutterBottom>
            No documents yet
          </Typography>
          <Typography variant="body2" color="text.secondary" gutterBottom>
            Upload your first clinical document to get started.
          </Typography>
          <Button
            component={RouterLink}
            to="/documents/upload"
            variant="contained"
            sx={{ mt: 2 }}
            aria-label="Upload your first document"
          >
            Upload your first document
          </Button>
        </Paper>
      )}

      {/* Document table — loading skeleton + data rows */}
      {(isLoading || (documents && documents.length > 0)) && (
        <TableContainer component={Paper}>
          <Table aria-label="Clinical documents table">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 500 }}>Document</TableCell>
                <TableCell sx={{ fontWeight: 500 }}>Uploaded</TableCell>
                <TableCell sx={{ fontWeight: 500 }}>Status</TableCell>
                <TableCell sx={{ fontWeight: 500 }} align="right">
                  Actions
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <SkeletonRows />
              ) : (
                documents!.map((doc) => {
                  const chipProps = getStatusChipProps(doc.extractionStatus);
                  return (
                    <TableRow
                      key={doc.documentId}
                      hover
                      sx={{ '&:last-child td, &:last-child th': { border: 0 } }}
                    >
                      {/* Filename */}
                      <TableCell>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                          <DescriptionIcon sx={{ color: 'text.secondary', flexShrink: 0 }} />
                          <Typography
                            variant="body2"
                            sx={{
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                              whiteSpace: 'nowrap',
                              maxWidth: { xs: 160, sm: 280, md: 400 },
                            }}
                            title={doc.originalFileName}
                          >
                            {doc.originalFileName}
                          </Typography>
                        </Box>
                      </TableCell>

                      {/* Upload date */}
                      <TableCell>
                        <Typography variant="body2" color="text.secondary">
                          {formatUploadDate(doc.uploadedAt)}
                        </Typography>
                      </TableCell>

                      {/* Status badge */}
                      <TableCell>
                        <Chip
                          label={STATUS_LABEL[doc.extractionStatus]}
                          size="small"
                          color={chipProps.color}
                          sx={chipProps.sx}
                          aria-label={`Document status: ${STATUS_LABEL[doc.extractionStatus]}`}
                        />
                      </TableCell>

                      {/* Actions */}
                      <TableCell align="right">
                        <Tooltip title="Delete document">
                          <span>
                            <IconButton
                              size="small"
                              color="default"
                              onClick={() =>
                                handleDeleteClick(doc.documentId, doc.originalFileName)
                              }
                              aria-label={`Delete ${doc.originalFileName}`}
                            >
                              <DeleteOutlineIcon fontSize="small" />
                            </IconButton>
                          </span>
                        </Tooltip>
                      </TableCell>
                    </TableRow>
                  );
                })
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Delete confirmation dialog */}
      <DeleteConfirmDialog
        open={pendingDelete !== null}
        fileName={pendingDelete?.fileName ?? ''}
        isDeleting={isDeleting}
        onConfirm={handleDeleteConfirm}
        onCancel={handleDeleteCancel}
      />
    </Box>
  );
}
