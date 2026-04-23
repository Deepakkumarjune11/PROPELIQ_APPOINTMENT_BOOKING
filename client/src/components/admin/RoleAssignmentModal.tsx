import { useEffect, useState } from 'react';
import axios from 'axios';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  FormGroup,
  IconButton,
  MenuItem,
  Select,
  SelectChangeEvent,
  Tooltip,
  Typography,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';

import { Permissions, type PermissionKey } from '@/lib/permissions';
import { useAssignRole } from '@/hooks/admin/useAssignRole';

interface Props {
  open: boolean;
  onClose: () => void;
  userId: string;
  currentRole: string;
  currentPermissions: number;
}

interface PermissionItem {
  key: PermissionKey;
  label: string;
  tooltip: string;
}

const permissionItems: PermissionItem[] = [
  {
    key:     'ViewPatientCharts',
    label:   'View patient charts',
    tooltip: 'Allows reading patient clinical data',
  },
  {
    key:     'VerifyClinicalData',
    label:   'Verify clinical data',
    tooltip: 'Allows confirming extracted facts and codes',
  },
  {
    key:     'ManageAppointments',
    label:   'Manage appointments',
    tooltip: 'Allows creating and modifying appointments',
  },
  {
    key:     'UploadDocuments',
    label:   'Upload documents',
    tooltip: 'Allows uploading patient documents',
  },
  {
    key:     'ViewMetrics',
    label:   'View metrics',
    tooltip: 'Allows accessing operational dashboards',
  },
];

export function RoleAssignmentModal({ open, onClose, userId, currentRole, currentPermissions }: Props) {
  const [role, setRole]                   = useState<string>('FrontDesk');
  const [permissionsBitfield, setBitfield] = useState(currentPermissions);
  const [conflictError, setConflictError]  = useState<string | null>(null);

  const { mutate: assignRole, isPending } = useAssignRole(userId);

  // Reset on open with new props
  useEffect(() => {
    if (open) {
      // Map top-level role to closest StaffRole default; Admins use FrontDesk as base
      setRole(currentRole === 'FrontDesk' || currentRole === 'CallCenter' || currentRole === 'ClinicalReviewer'
        ? currentRole
        : 'FrontDesk');
      setBitfield(currentPermissions);
      setConflictError(null);
    }
  }, [open, currentRole, currentPermissions]);

  const handleClose = () => { if (!isPending) onClose(); };

  const isChecked = (key: PermissionKey) => (permissionsBitfield & Permissions[key]) !== 0;

  const togglePermission = (key: PermissionKey) => {
    setBitfield((prev) =>
      isChecked(key) ? prev & ~Permissions[key] : prev | Permissions[key],
    );
  };

  const handleSave = () => {
    setConflictError(null);
    assignRole(
      { staffRole: role, permissionsBitfield },
      {
        onSuccess: handleClose,
        onError: (err) => {
          if (axios.isAxiosError(err) && err.response?.status === 422) {
            const msg = (err.response.data as { message?: string })?.message ?? 'Conflict error';
            setConflictError(msg);
          }
        },
      },
    );
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth
      PaperProps={{ sx: { borderRadius: 2 } }}>
      <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', pb: 1 }}>
        <Typography variant="h6" component="span">
          Assign role &amp; permissions
        </Typography>
        <IconButton onClick={handleClose} size="small" aria-label="Close" sx={{ color: 'text.secondary' }}>
          <CloseIcon />
        </IconButton>
      </DialogTitle>

      <DialogContent dividers sx={{ pt: 2 }}>
        {/* Role select */}
        <Box sx={{ mb: 3 }}>
          <Typography variant="body2" sx={{ mb: 0.5, fontWeight: 500, color: 'text.secondary' }}>
            Role
          </Typography>
          <Select
            value={role}
            onChange={(e: SelectChangeEvent) => setRole(e.target.value)}
            fullWidth
            size="small"
            inputProps={{ 'aria-label': 'Role' }}
          >
            <MenuItem value="FrontDesk">Front Desk</MenuItem>
            <MenuItem value="CallCenter">Call Center</MenuItem>
            <MenuItem value="ClinicalReviewer">Clinical Reviewer</MenuItem>
          </Select>
        </Box>

        {/* Permissions */}
        <Typography variant="body2" sx={{ mb: 1, fontWeight: 500, color: 'text.secondary' }}>
          Permissions
        </Typography>
        <FormGroup sx={{ gap: 1 }}>
          {permissionItems.map(({ key, label, tooltip }) => (
            <Tooltip key={key} title={tooltip} placement="right">
              <Box
                sx={{
                  display: 'flex',
                  alignItems: 'center',
                  bgcolor: 'grey.50',
                  borderRadius: 1,
                  px: 2,
                  py: 1,
                }}
              >
                <FormControlLabel
                  control={
                    <Checkbox
                      checked={isChecked(key)}
                      onChange={() => togglePermission(key)}
                      size="small"
                    />
                  }
                  label={label}
                  sx={{ flex: 1, m: 0, '& .MuiFormControlLabel-label': { fontSize: 14 } }}
                />
              </Box>
            </Tooltip>
          ))}
        </FormGroup>

        {/* 422 conflict error — above footer per wireframe SCR-023 */}
        {conflictError && (
          <Alert severity="error" sx={{ mt: 2 }}>
            Conflict: {conflictError}
          </Alert>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2, gap: 2 }}>
        <Button variant="outlined" onClick={handleClose} disabled={isPending}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSave}
          disabled={isPending}
          startIcon={isPending ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          Save changes
        </Button>
      </DialogActions>
    </Dialog>
  );
}
