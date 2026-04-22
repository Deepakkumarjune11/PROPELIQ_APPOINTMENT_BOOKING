// SCR-014 — Document Upload (US_018, AC-1, AC-2, AC-3, AC-4)
// Drag-and-drop upload zone with per-file progress bars and inline validation.
// States: Default (idle), Loading (progress bars), Error (Alert + retry), Validation (inline alert).
import CloudUploadIcon from '@mui/icons-material/CloudUpload';
import DescriptionIcon from '@mui/icons-material/Description';
import {
  Alert,
  Box,
  Breadcrumbs,
  Button,
  CircularProgress,
  LinearProgress,
  Link,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useRef, useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router-dom';

import { DOCUMENTS_QUERY_KEY } from '@/hooks/useDocuments';
import { useDocumentUpload } from '@/hooks/useDocumentUpload';

// ── Component ─────────────────────────────────────────────────────────────────

export default function DocumentUploadPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const {
    queue,
    addFiles,
    uploadAll,
    removeEntry,
    isUploading,
    validationError,
    clearValidationError,
  } = useDocumentUpload();

  const [isDragOver, setIsDragOver] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // ── Drag-and-drop handlers ────────────────────────────────────────────────

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    // Only clear dragover when leaving the drop zone itself (not child elements)
    if (!e.currentTarget.contains(e.relatedTarget as Node)) {
      setIsDragOver(false);
    }
  }, []);

  const handleDrop = useCallback(
    async (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragOver(false);
      setUploadError(null);
      if (e.dataTransfer.files.length > 0) {
        await addFiles(e.dataTransfer.files);
      }
    },
    [addFiles],
  );

  // ── File input handler ────────────────────────────────────────────────────

  const handleFileChange = useCallback(
    async (e: React.ChangeEvent<HTMLInputElement>) => {
      setUploadError(null);
      if (e.target.files && e.target.files.length > 0) {
        await addFiles(e.target.files);
        // Reset input so same file can be re-selected after validation failure
        e.target.value = '';
      }
    },
    [addFiles],
  );

  const handleBrowseClick = () => fileInputRef.current?.click();

  // ── Upload submit ─────────────────────────────────────────────────────────

  const handleUpload = async () => {
    setUploadError(null);
    await uploadAll((records) => {
      if (records.length > 0) {
        // Invalidate document list so SCR-015 shows the new entries with "queued" badge (AC-3)
        void queryClient.invalidateQueries({ queryKey: DOCUMENTS_QUERY_KEY });
        navigate('/documents');
      }
    }).catch(() => {
      setUploadError('One or more files failed to upload. Please retry the failed items.');
    });
  };

  const pendingCount = queue.filter((e) => e.status === 'pending').length;
  const hasQueue = queue.length > 0;

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <Box sx={{ maxWidth: 800, mx: 'auto' }}>
      {/* Breadcrumb — UXR-002 */}
      <Breadcrumbs sx={{ mb: 2 }}>
        <Link component={RouterLink} to="/" underline="hover" color="inherit">
          Home
        </Link>
        <Link component={RouterLink} to="/documents" underline="hover" color="inherit">
          My Documents
        </Link>
        <Typography color="text.primary">Upload</Typography>
      </Breadcrumbs>

      <Typography variant="h4" component="h1" gutterBottom>
        Upload clinical documents
      </Typography>

      {/* Validation error — inline below breadcrumb, above drop zone (AC-4) */}
      {validationError && (
        <Alert
          severity="error"
          onClose={clearValidationError}
          sx={{ mb: 2 }}
          role="alert"
        >
          {validationError}
        </Alert>
      )}

      {/* Upload error — API-level failure */}
      {uploadError && (
        <Alert
          severity="error"
          onClose={() => setUploadError(null)}
          sx={{ mb: 2 }}
          role="alert"
        >
          {uploadError}
        </Alert>
      )}

      {/* Drop zone — SCR-014 wireframe: dashed border, cloud icon, instructions (AC-1) */}
      <Paper
        variant="outlined"
        component="div"
        role="region"
        aria-label="File drop zone. Drag and drop PDF files here or click Browse to select."
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={!hasQueue ? handleBrowseClick : undefined}
        sx={{
          border: '2px dashed',
          borderColor: isDragOver ? 'primary.main' : 'divider',
          borderRadius: 2,
          p: 4,
          textAlign: 'center',
          cursor: hasQueue ? 'default' : 'pointer',
          backgroundColor: isDragOver
            ? 'rgba(33, 150, 243, 0.05)'
            : 'background.paper',
          transition: 'border-color 150ms, background-color 150ms',
          '&:hover': !hasQueue
            ? { borderColor: 'primary.main', backgroundColor: 'rgba(33, 150, 243, 0.05)' }
            : {},
          mb: 3,
        }}
      >
        {/* Hidden file input — multiple PDFs only */}
        <input
          ref={fileInputRef}
          type="file"
          multiple
          accept=".pdf,application/pdf"
          style={{ display: 'none' }}
          aria-hidden="true"
          onChange={handleFileChange}
        />

        <CloudUploadIcon
          sx={{ fontSize: 64, color: isDragOver ? 'primary.main' : 'text.disabled', mb: 2 }}
        />

        <Typography variant="h6" gutterBottom>
          Drop files here or click to browse
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Supported format: PDF &bull; Max 25 MB per file
        </Typography>

        {!hasQueue && (
          <Button
            variant="contained"
            onClick={(e) => {
              e.stopPropagation();
              handleBrowseClick();
            }}
            sx={{ mt: 3 }}
            aria-label="Browse files to upload"
          >
            Browse Files
          </Button>
        )}
      </Paper>

      {/* Per-file progress stack — visible once files are queued (AC-2) */}
      {hasQueue && (
        <Paper sx={{ p: 3, mb: 3 }}>
          <Typography variant="subtitle1" fontWeight={500} gutterBottom>
            {isUploading ? 'Uploading files…' : 'Ready to upload'}
          </Typography>

          <Stack spacing={2}>
            {queue.map((entry) => (
              <Box key={entry.id}>
                <Box
                  sx={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 1.5,
                    mb: 0.5,
                  }}
                >
                  <DescriptionIcon sx={{ color: 'text.secondary', flexShrink: 0 }} />

                  <Typography
                    variant="body2"
                    fontWeight={500}
                    sx={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
                  >
                    {entry.file.name}
                  </Typography>

                  {/* Status label */}
                  {entry.status === 'uploading' && (
                    <Typography variant="caption" color="text.secondary">
                      {entry.progress}%
                    </Typography>
                  )}
                  {entry.status === 'done' && (
                    <Typography variant="caption" color="success.main">
                      Done
                    </Typography>
                  )}
                  {(entry.status === 'error' || entry.status === 'rejected') && (
                    <Button
                      size="small"
                      color="error"
                      onClick={() => removeEntry(entry.id)}
                      aria-label={`Remove ${entry.file.name} from queue`}
                    >
                      Remove
                    </Button>
                  )}
                </Box>

                {/* LinearProgress — determinate while uploading, full bar on done */}
                {(entry.status === 'uploading' || entry.status === 'pending') && (
                  <LinearProgress
                    variant="determinate"
                    value={entry.progress}
                    aria-label={`Upload progress for ${entry.file.name}: ${entry.progress}%`}
                    sx={{ height: 8, borderRadius: 4 }}
                  />
                )}
                {entry.status === 'done' && (
                  <LinearProgress
                    variant="determinate"
                    value={100}
                    color="success"
                    sx={{ height: 8, borderRadius: 4 }}
                    aria-label={`${entry.file.name} uploaded successfully`}
                  />
                )}

                {/* Inline file-level error */}
                {(entry.status === 'error' || entry.status === 'rejected') &&
                  entry.errorMsg && (
                    <Typography variant="caption" color="error.main" role="alert">
                      {entry.errorMsg}
                    </Typography>
                  )}
              </Box>
            ))}
          </Stack>

          {/* Action row */}
          <Stack direction="row" spacing={2} sx={{ mt: 3 }} justifyContent="flex-end">
            <Button
              variant="outlined"
              onClick={handleBrowseClick}
              disabled={isUploading}
              aria-label="Add more files"
            >
              Add More Files
            </Button>
            <Button
              variant="contained"
              onClick={handleUpload}
              disabled={isUploading || pendingCount === 0}
              startIcon={isUploading ? <CircularProgress size={18} color="inherit" /> : null}
              aria-label={
                isUploading ? 'Uploading files, please wait' : `Upload ${pendingCount} file(s)`
              }
            >
              {isUploading ? 'Uploading…' : `Upload ${pendingCount} File${pendingCount !== 1 ? 's' : ''}`}
            </Button>
          </Stack>
        </Paper>
      )}
    </Box>
  );
}
