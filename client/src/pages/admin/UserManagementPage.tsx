import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  IconButton,
  InputAdornment,
  Skeleton,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
  Paper,
} from '@mui/material';
import BlockIcon from '@mui/icons-material/Block';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import EditIcon from '@mui/icons-material/Edit';
import ManageAccountsIcon from '@mui/icons-material/ManageAccounts';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import SearchIcon from '@mui/icons-material/Search';

import type { AdminUserDto } from '@/api/adminUsers';
import { CreateEditUserModal } from '@/components/admin/CreateEditUserModal';
import { RoleAssignmentModal } from '@/components/admin/RoleAssignmentModal';
import { useAdminUsers } from '@/hooks/admin/useAdminUsers';
import { useToggleUserStatus } from '@/hooks/admin/useToggleUserStatus';
import { useDebounce } from '@/hooks/useDebounce';
import { useAuthStore } from '@/stores/auth-store';

// ── Role badge colour map per wireframe SCR-021 ────────────────────────────
type ChipColor = 'primary' | 'success' | 'default';
const roleBadgeColor: Record<string, ChipColor> = {
  Staff:   'primary',
  Admin:   'success',
  Patient: 'default',
};

// ── Confirmation dialog state ──────────────────────────────────────────────
interface ConfirmState {
  open: boolean;
  userId: string;
  userName: string;
  isActive: boolean;
}

const CONFIRM_INITIAL: ConfirmState = { open: false, userId: '', userName: '', isActive: true };

// ── Row action: Toggle disable/enable ─────────────────────────────────────
function ToggleStatusButton({
  user,
  currentUserId,
}: {
  user: AdminUserDto;
  currentUserId: string | undefined;
  onConfirm: (u: AdminUserDto) => void;
}) {
  const isSelf = user.id === currentUserId;
  const icon   = user.isActive ? <BlockIcon fontSize="small" /> : <CheckCircleOutlineIcon fontSize="small" />;
  const title  = isSelf
    ? 'You cannot disable your own account'
    : user.isActive
      ? 'Disable user'
      : 'Enable user';

  return (
    <Tooltip title={title}>
      {/* Span wrapper required for Tooltip on disabled element */}
      <span>
        <IconButton size="small" disabled={isSelf} aria-label={title} sx={{ color: 'text.secondary' }}>
          {icon}
        </IconButton>
      </span>
    </Tooltip>
  );
}

// ── ConfirmToggleDialog ────────────────────────────────────────────────────
function ConfirmToggleDialog({
  state,
  onConfirm,
  onCancel,
}: {
  state: ConfirmState;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const { mutate: toggle, isPending } = useToggleUserStatus(state.userId, state.isActive);
  const action = state.isActive ? 'Disable' : 'Enable';

  const handleConfirm = () => {
    toggle(undefined, {
      onSuccess: onConfirm,
    });
  };

  return (
    <Dialog open={state.open} onClose={onCancel} maxWidth="xs" fullWidth>
      <DialogTitle>{action} {state.userName}?</DialogTitle>
      <DialogContent>
        <DialogContentText>
          {state.isActive
            ? 'This will immediately terminate their active sessions.'
            : 'This will re-activate the account and restore access.'}
        </DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} variant="outlined" disabled={isPending}>
          Cancel
        </Button>
        <Button
          onClick={handleConfirm}
          variant="contained"
          color={state.isActive ? 'error' : 'primary'}
          disabled={isPending}
        >
          {action}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ── Main page ──────────────────────────────────────────────────────────────
export default function UserManagementPage() {
  const { user: currentUser } = useAuthStore();
  const { data: users = [], isLoading, isError } = useAdminUsers();

  const [search, setSearch]             = useState('');
  const debouncedSearch                 = useDebounce(search, 300);

  const [createOpen, setCreateOpen]     = useState(false);
  const [editUser, setEditUser]         = useState<AdminUserDto | null>(null);
  const [roleUser, setRoleUser]         = useState<AdminUserDto | null>(null);
  const [confirmState, setConfirmState] = useState<ConfirmState>(CONFIRM_INITIAL);

  // Client-side filter on name / email / role substring
  const filtered = users.filter((u) => {
    const q = debouncedSearch.toLowerCase();
    return (
      u.name.toLowerCase().includes(q) ||
      u.email.toLowerCase().includes(q) ||
      u.role.toLowerCase().includes(q)
    );
  });

  const openConfirm = (u: AdminUserDto) =>
    setConfirmState({ open: true, userId: u.id, userName: u.name, isActive: u.isActive });

  const closeConfirm = () => setConfirmState(CONFIRM_INITIAL);

  return (
    <Box sx={{ p: 3 }}>
      {/* ── Header ── */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h4" component="h1" sx={{ fontWeight: 400 }}>
          User management
        </Typography>
        <Button
          variant="contained"
          startIcon={<PersonAddIcon />}
          onClick={() => setCreateOpen(true)}
          sx={{ minHeight: 44 }}
        >
          Create user
        </Button>
      </Box>

      {/* ── Search bar — width 300px per wireframe SCR-021 ── */}
      <TextField
        size="small"
        placeholder="Search users by name, email, or role"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        sx={{ mb: 2, width: 300 }}
        InputProps={{
          startAdornment: (
            <InputAdornment position="start">
              <SearchIcon fontSize="small" sx={{ color: 'text.secondary' }} />
            </InputAdornment>
          ),
        }}
      />

      {/* ── Error state ── */}
      {isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          Failed to load users. Please try again.
        </Alert>
      )}

      {/* ── Table ── */}
      <TableContainer
        component={Paper}
        elevation={1}
        sx={{ borderRadius: 1 }}
      >
        <Table>
          <TableHead>
            <TableRow>
              {['Name', 'Email', 'Role', 'Status', 'Actions'].map((col) => (
                <TableCell
                  key={col}
                  sx={{
                    bgcolor: 'grey.50',
                    fontWeight: 500,
                    fontSize: 14,
                    borderBottom: '2px solid',
                    borderColor: 'grey.300',
                  }}
                >
                  {col}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {/* Loading: 5 skeleton rows */}
            {isLoading &&
              Array.from({ length: 5 }).map((_, i) => (
                <TableRow key={i}>
                  {Array.from({ length: 5 }).map((__, j) => (
                    <TableCell key={j}>
                      <Skeleton variant="text" />
                    </TableCell>
                  ))}
                </TableRow>
              ))}

            {/* Empty state */}
            {!isLoading && !isError && filtered.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} align="center" sx={{ py: 6 }}>
                  <Typography color="text.secondary" gutterBottom>
                    No users found
                  </Typography>
                  <Button variant="contained" onClick={() => setCreateOpen(true)}>
                    Create user
                  </Button>
                </TableCell>
              </TableRow>
            )}

            {/* Data rows */}
            {!isLoading &&
              filtered.map((u) => (
                <TableRow
                  key={u.id}
                  sx={{ '&:hover': { bgcolor: 'grey.100' } }}
                >
                  <TableCell sx={{ fontSize: 14 }}>{u.name}</TableCell>
                  <TableCell sx={{ fontSize: 14 }}>{u.email}</TableCell>
                  <TableCell>
                    <Chip
                      label={u.role}
                      size="small"
                      color={roleBadgeColor[u.role] ?? 'default'}
                      sx={{ fontWeight: 500, fontSize: 12, borderRadius: '4px' }}
                    />
                  </TableCell>
                  <TableCell
                    sx={{
                      fontSize: 14,
                      color: u.isActive ? 'text.primary' : 'text.disabled',
                    }}
                  >
                    {u.isActive ? 'Active' : 'Disabled'}
                  </TableCell>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>
                    {/* Edit */}
                    <Tooltip title="Edit user">
                      <IconButton
                        size="small"
                        onClick={() => setEditUser(u)}
                        aria-label="Edit user"
                        sx={{ color: 'text.secondary' }}
                      >
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>

                    {/* Assign role */}
                    <Tooltip title="Assign role & permissions">
                      <IconButton
                        size="small"
                        onClick={() => setRoleUser(u)}
                        aria-label="Assign role"
                        sx={{ color: 'text.secondary' }}
                      >
                        <ManageAccountsIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>

                    {/* Disable / Enable */}
                    <span onClick={() => { if (u.id !== currentUser?.id) openConfirm(u); }}>
                      <ToggleStatusButton
                        user={u}
                        currentUserId={currentUser?.id}
                        onConfirm={openConfirm}
                      />
                    </span>
                  </TableCell>
                </TableRow>
              ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* ── Modals ── */}
      <CreateEditUserModal
        open={createOpen || editUser !== null}
        onClose={() => { setCreateOpen(false); setEditUser(null); }}
        user={editUser ?? undefined}
      />

      {roleUser && (
        <RoleAssignmentModal
          open
          onClose={() => setRoleUser(null)}
          userId={roleUser.id}
          currentRole={roleUser.role}
          currentPermissions={roleUser.permissionsBitfield}
        />
      )}

      {/* ── Confirm disable/enable dialog ── */}
      <ConfirmToggleDialog
        state={confirmState}
        onConfirm={closeConfirm}
        onCancel={closeConfirm}
      />
    </Box>
  );
}
