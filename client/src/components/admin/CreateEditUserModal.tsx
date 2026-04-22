import { useEffect, useState } from 'react';
import axios from 'axios';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormHelperText,
  IconButton,
  MenuItem,
  Select,
  SelectChangeEvent,
  TextField,
  Typography,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';

import type { AdminUserDto, CreateUserRequest, UpdateUserRequest } from '@/api/adminUsers';
import { useCreateUser } from '@/hooks/admin/useCreateUser';
import { useUpdateUser } from '@/hooks/admin/useUpdateUser';

interface Props {
  open: boolean;
  onClose: () => void;
  /** Undefined = create mode; populated = edit mode */
  user?: AdminUserDto;
}

interface FormState {
  name: string;
  email: string;
  role: string;
  department: string;
}

interface FormErrors {
  name: string;
  email: string;
  role: string;
}

const EMPTY_FORM: FormState = { name: '', email: '', role: '', department: '' };
const EMPTY_ERRORS: FormErrors = { name: '', email: '', role: '' };

function validateName(v: string)  { return v.trim() ? '' : 'Full name is required'; }
function validateEmail(v: string) { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.trim()) ? '' : 'Valid email is required'; }
function validateRole(v: string)  { return v ? '' : 'Role is required'; }

export function CreateEditUserModal({ open, onClose, user }: Props) {
  const isEditMode = Boolean(user);

  const [form, setForm]           = useState<FormState>(EMPTY_FORM);
  const [errors, setErrors]       = useState<FormErrors>(EMPTY_ERRORS);
  const [conflictError, setConflictError] = useState<string | null>(null);

  const { mutate: createUser, isPending: creating } = useCreateUser();
  const { mutate: updateUser, isPending: updating } = useUpdateUser(user?.id);
  const isPending = creating || updating;

  // Populate form when opened in edit mode
  useEffect(() => {
    if (open) {
      setForm(
        user
          ? { name: user.name, email: user.email, role: user.role, department: user.department ?? '' }
          : EMPTY_FORM,
      );
      setErrors(EMPTY_ERRORS);
      setConflictError(null);
    }
  }, [open, user]);

  const handleClose = () => { if (!isPending) onClose(); };

  const setField = (field: keyof FormState, value: string) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const blurValidate = (field: keyof FormErrors) => {
    const validate = { name: validateName, email: validateEmail, role: validateRole };
    setErrors((prev) => ({ ...prev, [field]: validate[field](form[field]) }));
  };

  const handleSubmit = () => {
    const newErrors: FormErrors = {
      name:  validateName(form.name),
      email: validateEmail(form.email),
      role:  validateRole(form.role),
    };
    setErrors(newErrors);
    if (Object.values(newErrors).some(Boolean)) return;

    const payload = {
      name:       form.name.trim(),
      email:      form.email.trim(),
      role:       form.role,
      department: form.department.trim() || undefined,
    };

    const onError = (err: Error) => {
      if (axios.isAxiosError(err) && err.response?.status === 422) {
        const msg = (err.response.data as { message?: string })?.message ?? 'Conflict error';
        setConflictError(msg);
      }
    };

    if (isEditMode) {
      updateUser(payload as UpdateUserRequest, { onSuccess: handleClose, onError });
    } else {
      createUser(payload as CreateUserRequest, { onSuccess: handleClose, onError });
    }
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth
      PaperProps={{ sx: { borderRadius: 2 } }}>
      <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', pb: 1 }}>
        <Typography variant="h6" component="span">
          {isEditMode ? 'Edit user' : 'Create new user'}
        </Typography>
        <IconButton onClick={handleClose} size="small" aria-label="Close" sx={{ color: 'text.secondary' }}>
          <CloseIcon />
        </IconButton>
      </DialogTitle>

      <DialogContent dividers sx={{ pt: 2 }}>
        {/* Full name */}
        <TextField
          label="Full name"
          required
          fullWidth
          value={form.name}
          onChange={(e) => setField('name', e.target.value)}
          onBlur={() => blurValidate('name')}
          error={Boolean(errors.name)}
          helperText={errors.name || ' '}
          sx={{ mb: 1 }}
          inputProps={{ 'aria-required': 'true' }}
        />

        {/* Email */}
        <TextField
          label="Email address"
          required
          fullWidth
          type="email"
          value={form.email}
          onChange={(e) => setField('email', e.target.value)}
          onBlur={() => blurValidate('email')}
          error={Boolean(errors.email)}
          helperText={errors.email || ' '}
          placeholder="john.doe@hospital.org"
          sx={{ mb: 1 }}
        />

        {/* Role select */}
        <Box sx={{ mb: 1 }}>
          <Typography variant="body2" sx={{ mb: 0.5, fontWeight: 500, color: 'text.secondary' }}>
            Role *
          </Typography>
          <Select
            value={form.role}
            onChange={(e: SelectChangeEvent) => setField('role', e.target.value)}
            onBlur={() => blurValidate('role')}
            displayEmpty
            fullWidth
            error={Boolean(errors.role)}
            size="small"
            inputProps={{ 'aria-label': 'Role' }}
          >
            <MenuItem value="" disabled>Select role</MenuItem>
            <MenuItem value="Patient">Patient</MenuItem>
            <MenuItem value="Staff">Staff</MenuItem>
            <MenuItem value="Admin">Admin</MenuItem>
          </Select>
          {errors.role && (
            <FormHelperText error sx={{ mx: '14px' }}>{errors.role}</FormHelperText>
          )}
        </Box>

        {/* Department (optional) */}
        <TextField
          label="Department"
          fullWidth
          value={form.department}
          onChange={(e) => setField('department', e.target.value)}
          helperText=" "
          placeholder="e.g., Cardiology"
          sx={{ mb: 1 }}
        />

        {/* 422 conflict error — shown above footer per wireframe SCR-022 */}
        {conflictError && (
          <Alert severity="error" sx={{ mt: 1 }}>
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
          onClick={handleSubmit}
          disabled={isPending}
          startIcon={isPending ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {isEditMode ? 'Save changes' : 'Create user'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
