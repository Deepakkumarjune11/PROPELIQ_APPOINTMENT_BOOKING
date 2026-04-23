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
  InputAdornment,
  MenuItem,
  Select,
  SelectChangeEvent,
  TextField,
  Typography,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';

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
  staffRole: string;
  department: string;
  password: string;
}

interface FormErrors {
  name: string;
  email: string;
  role: string;
  staffRole: string;
  password: string;
}

const EMPTY_FORM: FormState = { name: '', email: '', role: '', staffRole: '', department: '', password: '' };
const EMPTY_ERRORS: FormErrors = { name: '', email: '', role: '', staffRole: '', password: '' };

function validateName(v: string)     { return v.trim() ? '' : 'Full name is required'; }
function validateEmail(v: string)    { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.trim()) ? '' : 'Valid email is required'; }
function validateRole(v: string)     { return v ? '' : 'Role is required'; }
function validateStaffRole(v: string, isStaff: boolean) { return isStaff && !v ? 'Staff role is required' : ''; }
function validatePassword(v: string, required: boolean) {
  if (!v && required) return 'Password is required';
  if (v && v.length < 8) return 'Password must be at least 8 characters';
  if (v && !/[A-Z]/.test(v)) return 'Password must contain an uppercase letter';
  if (v && !/[0-9!@#$%^&*]/.test(v)) return 'Password must contain a number or special character';
  return '';
}

export function CreateEditUserModal({ open, onClose, user }: Props) {
  const isEditMode = Boolean(user);

  const [form, setForm]                   = useState<FormState>(EMPTY_FORM);
  const [errors, setErrors]               = useState<FormErrors>(EMPTY_ERRORS);
  const [showPassword, setShowPassword]   = useState(false);
  const [conflictError, setConflictError] = useState<string | null>(null);

  const { mutate: createUser, isPending: creating } = useCreateUser();
  const { mutate: updateUser, isPending: updating } = useUpdateUser(user?.id);
  const isPending = creating || updating;

  // Populate form when opened in edit mode
  useEffect(() => {
    if (open) {
      setForm(
        user
          ? { name: user.name, email: user.email, role: user.role, staffRole: '', department: user.department ?? '', password: '' }
          : EMPTY_FORM,
      );
      setErrors(EMPTY_ERRORS);
      setConflictError(null);
      setShowPassword(false);
    }
  }, [open, user]);

  const handleClose = () => { if (!isPending) onClose(); };

  const setField = (field: keyof FormState, value: string) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const blurValidate = (field: keyof FormErrors) => {
    const validate: Record<keyof FormErrors, (v: string) => string> = {
      name:      validateName,
      email:     validateEmail,
      role:      validateRole,
      staffRole: (v) => validateStaffRole(v, form.role === 'Staff'),
      password:  (v) => validatePassword(v, !isEditMode),
    };
    // Skip role / password validation in edit mode
    if (isEditMode && (field === 'role' || field === 'staffRole' || field === 'password')) return;
    setErrors((prev) => ({ ...prev, [field]: validate[field](form[field]) }));
  };

  const handleSubmit = () => {
    const newErrors: FormErrors = {
      name:      validateName(form.name),
      email:     validateEmail(form.email),
      role:      isEditMode ? '' : validateRole(form.role),
      staffRole: isEditMode ? '' : validateStaffRole(form.staffRole, form.role === 'Staff'),
      password:  isEditMode ? '' : validatePassword(form.password, true),
    };
    setErrors(newErrors);
    if (Object.values(newErrors).some(Boolean)) return;

    const onError = (err: Error) => {
      if (axios.isAxiosError(err) && err.response?.status === 422) {
        const msg = (err.response.data as { message?: string })?.message ?? 'Conflict error';
        setConflictError(msg);
      }
    };

    if (isEditMode) {
      const payload: UpdateUserRequest = {
        name:       form.name.trim(),
        email:      form.email.trim(),
        role:       form.role,
        department: form.department.trim() || undefined,
      };
      updateUser(payload, { onSuccess: handleClose, onError });
    } else {
      const payload: CreateUserRequest = {
        name:       form.name.trim(),
        email:      form.email.trim(),
        role:       form.role,
        staffRole:  form.role === 'Staff' ? form.staffRole : undefined,
        department: form.department.trim() || undefined,
        password:   form.password.trim(),
      };
      createUser(payload, { onSuccess: handleClose, onError });
    }
  };

  return (
    <>
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
            Role {isEditMode ? '' : '*'}
          </Typography>
          <Select
            value={form.role}
            onChange={(e: SelectChangeEvent) => setField('role', e.target.value)}
            onBlur={() => blurValidate('role')}
            displayEmpty
            fullWidth
            disabled={isEditMode}
            error={Boolean(errors.role)}
            size="small"
            inputProps={{ 'aria-label': 'Role' }}
          >
            <MenuItem value="" disabled>Select role</MenuItem>
            <MenuItem value="Patient">Patient</MenuItem>
            <MenuItem value="Staff">Staff</MenuItem>
            <MenuItem value="Admin">Admin</MenuItem>
          </Select>
          {isEditMode && (
            <FormHelperText sx={{ mx: '14px' }}>Role cannot be changed after creation</FormHelperText>
          )}
          {errors.role && (
            <FormHelperText error sx={{ mx: '14px' }}>{errors.role}</FormHelperText>
          )}
        </Box>

        {/* Staff sub-role — only when Role = Staff and in create mode */}
        {!isEditMode && form.role === 'Staff' && (
          <Box sx={{ mb: 1 }}>
            <Typography variant="body2" sx={{ mb: 0.5, fontWeight: 500, color: 'text.secondary' }}>
              Staff role *
            </Typography>
            <Select
              value={form.staffRole}
              onChange={(e: SelectChangeEvent) => setField('staffRole', e.target.value)}
              onBlur={() => blurValidate('staffRole')}
              displayEmpty
              fullWidth
              error={Boolean(errors.staffRole)}
              size="small"
              inputProps={{ 'aria-label': 'Staff role' }}
            >
              <MenuItem value="" disabled>Select staff role</MenuItem>
              <MenuItem value="FrontDesk">Front Desk</MenuItem>
              <MenuItem value="CallCenter">Call Center</MenuItem>
              <MenuItem value="ClinicalReviewer">Clinical Reviewer</MenuItem>
            </Select>
            {errors.staffRole && (
              <FormHelperText error sx={{ mx: '14px' }}>{errors.staffRole}</FormHelperText>
            )}
          </Box>
        )}

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

        {/* Password — required on create only, hidden on edit */}
        {!isEditMode && (
          <TextField
            label="Password"
            required
            fullWidth
            type={showPassword ? 'text' : 'password'}
            value={form.password}
            onChange={(e) => setField('password', e.target.value)}
            onBlur={() => blurValidate('password')}
            error={Boolean(errors.password)}
            helperText={errors.password || 'Min 8 chars, 1 uppercase, 1 number or special character'}
            sx={{ mb: 1 }}
            InputProps={{
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton
                    size="small"
                    onClick={() => setShowPassword((v) => !v)}
                    aria-label={showPassword ? 'Hide password' : 'Show password'}
                    edge="end"
                  >
                    {showPassword ? <VisibilityOff fontSize="small" /> : <Visibility fontSize="small" />}
                  </IconButton>
                </InputAdornment>
              ),
            }}
          />
        )}

        {/* 422 conflict error */}
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

    {/* tempPassword dialog removed — password is now always set by the admin at create time */}
    </>
  );
}
