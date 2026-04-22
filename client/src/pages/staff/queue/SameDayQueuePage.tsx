// SCR-012 + SCR-013 — Same-Day Queue & Arrival Marking (US_017).
//
// SCR-012: Sortable queue table with DnD reorder, status badges, real-time SignalR updates.
// SCR-013: Inline bulk/individual arrival marking (checkbox + "Mark selected as arrived" toolbar).
//
// States: Loading (Skeleton rows), Error (Alert + Retry), Empty (no patients + CTA), Default.
// Breadcrumb: Home > Staff Dashboard > Same-Day Queue (UXR-002).
import { useCallback, useEffect, useState } from 'react';
import {
  DndContext,
  DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import {
  SortableContext,
  arrayMove,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import AddIcon from '@mui/icons-material/Add';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import {
  Alert,
  Box,
  Breadcrumbs,
  Button,
  CircularProgress,
  Link,
  Paper,
  Skeleton,
  Snackbar,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Toolbar,
  Tooltip,
  Typography,
} from '@mui/material';
import { useQueryClient } from '@tanstack/react-query';
import { Link as RouterLink, useNavigate } from 'react-router-dom';

import { type QueueEntry, reorderQueue } from '@/api/staff';
import { useQueueSignalR } from '@/hooks/useQueueSignalR';
import { QUEUE_QUERY_KEY, useSameDayQueue } from '@/hooks/useSameDayQueue';
import { useUpdateAppointmentStatus } from '@/hooks/useUpdateAppointmentStatus';
import { QueueRow } from './QueueRow';

// ── Snackbar state helper ────────────────────────────────────────────────────

interface ToastState {
  open: boolean;
  message: string;
  severity: 'success' | 'info' | 'warning' | 'error';
}

// ── Loading skeleton rows ────────────────────────────────────────────────────

function QueueSkeletonRows() {
  return (
    <>
      {Array.from({ length: 4 }).map((_, i) => (
        <TableRow key={i}>
          {Array.from({ length: 8 }).map((_, j) => (
            <TableCell key={j}>
              <Skeleton variant="text" width="80%" />
            </TableCell>
          ))}
        </TableRow>
      ))}
    </>
  );
}

// ── Component ────────────────────────────────────────────────────────────────

export default function SameDayQueuePage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: serverEntries, isLoading, isError, refetch } = useSameDayQueue();

  // Local sorted copy — updated optimistically on drag end.
  const [entries, setEntries] = useState<QueueEntry[]>([]);

  // Keep local entries in sync with server data (but not while dragging).
  useEffect(() => {
    if (serverEntries) setEntries(serverEntries);
  }, [serverEntries]);

  // Bulk selection state (SCR-013).
  const [selected, setSelected] = useState<Set<string>>(new Set());

  // Toast state.
  const [toast, setToast] = useState<ToastState>({
    open: false,
    message: '',
    severity: 'success',
  });

  const showToast = useCallback(
    (severity: ToastState['severity'], message: string) => {
      setToast({ open: true, severity, message });
    },
    [],
  );

  // ── Status mutation ────────────────────────────────────────────────────────
  const { mutate: updateStatus, isLoading: statusPending } = useUpdateAppointmentStatus({
    onSuccess: (msg) => showToast('success', msg),
    onError: (msg) => showToast('error', msg),
  });

  // ── SignalR real-time updates (AC-4) ────────────────────────────────────────
  useQueueSignalR({
    onReconnecting: () => showToast('warning', 'Reconnecting to live queue…'),
    onReconnected: () => showToast('info', 'Live queue reconnected.'),
  });

  // ── DnD sensors ─────────────────────────────────────────────────────────────
  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  );

  // ── Drag end — reorder locally then persist (AC-2) ──────────────────────────
  const handleDragEnd = useCallback(
    async (event: DragEndEvent) => {
      const { active, over } = event;
      if (!over || active.id === over.id) return;

      const oldIndex = entries.findIndex((e) => e.appointmentId === active.id);
      const newIndex = entries.findIndex((e) => e.appointmentId === over.id);
      if (oldIndex === -1 || newIndex === -1) return;

      const reordered = arrayMove(entries, oldIndex, newIndex).map((e, i) => ({
        ...e,
        queuePosition: i + 1,
      }));

      // Optimistic local update.
      setEntries(reordered);

      // Persist to server.
      try {
        await reorderQueue(reordered.map((e) => e.appointmentId));
        void queryClient.invalidateQueries({ queryKey: QUEUE_QUERY_KEY });
      } catch {
        // Revert on failure.
        setEntries(entries);
        showToast('error', 'Reorder failed. Please try again.');
      }
    },
    [entries, queryClient, showToast],
  );

  // ── Bulk selection helpers ───────────────────────────────────────────────────
  const toggleSelect = useCallback((id: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const toggleSelectAll = useCallback(() => {
    const active = entries.filter(
      (e) => e.status !== 'completed' && e.status !== 'left',
    );
    if (selected.size === active.length) {
      setSelected(new Set());
    } else {
      setSelected(new Set(active.map((e) => e.appointmentId)));
    }
  }, [entries, selected.size]);

  // ── Bulk "Mark arrived" ──────────────────────────────────────────────────────
  const handleBulkArrived = useCallback(() => {
    for (const id of selected) {
      updateStatus({ appointmentId: id, status: 'arrived' });
    }
    setSelected(new Set());
  }, [selected, updateStatus]);

  // ── Individual status updates ────────────────────────────────────────────────
  const handleMarkArrived = useCallback(
    (id: string) => updateStatus({ appointmentId: id, status: 'arrived' }),
    [updateStatus],
  );

  const handleMarkLeft = useCallback(
    (id: string) => updateStatus({ appointmentId: id, status: 'left' }),
    [updateStatus],
  );

  // ── Active entries for display (hide completed/left) ─────────────────────────
  const activeEntries = entries.filter(
    (e) => e.status !== 'completed' && e.status !== 'left',
  );
  const inactiveEntries = entries.filter(
    (e) => e.status === 'completed' || e.status === 'left',
  );
  const allActive = [...activeEntries, ...inactiveEntries];

  // ── Render ────────────────────────────────────────────────────────────────────
  return (
    <Box sx={{ p: 3 }}>
      {/* Breadcrumb — UXR-002 */}
      <Breadcrumbs aria-label="breadcrumb" sx={{ mb: 2 }}>
        <Link component={RouterLink} to="/" underline="hover" color="inherit">
          Home
        </Link>
        <Link component={RouterLink} to="/staff/dashboard" underline="hover" color="inherit">
          Staff Dashboard
        </Link>
        <Typography color="text.primary">Same-Day Queue</Typography>
      </Breadcrumbs>

      <Typography variant="h5" fontWeight={500} mb={3}>
        Same-Day Queue
      </Typography>

      {/* ── Error state ─────────────────────────────────────────────────── */}
      {isError && !isLoading && (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => void refetch()}>
              Retry
            </Button>
          }
          sx={{ mb: 2 }}
        >
          Failed to load today's queue. Please retry or refresh.
        </Alert>
      )}

      {/* ── Bulk selection toolbar (SCR-013) ────────────────────────────── */}
      {selected.size > 0 && (
        <Toolbar
          variant="dense"
          sx={{
            mb: 1,
            bgcolor: 'primary.light',
            borderRadius: 1,
            color: 'primary.contrastText',
          }}
        >
          <Typography sx={{ flex: 1 }}>{selected.size} selected</Typography>
          <Button
            variant="contained"
            color="success"
            size="small"
            startIcon={
              statusPending ? (
                <CircularProgress size={14} color="inherit" />
              ) : (
                <CheckCircleOutlineIcon />
              )
            }
            disabled={statusPending}
            onClick={handleBulkArrived}
            aria-label={`Mark ${selected.size} selected patients as arrived`}
          >
            Mark selected as arrived ({selected.size})
          </Button>
        </Toolbar>
      )}

      {/* ── Table ────────────────────────────────────────────────────────── */}
      <TableContainer component={Paper} elevation={1}>
        <Table aria-label="Same-day queue" size="small">
          <TableHead>
            <TableRow>
              {/* Drag handle col */}
              <TableCell sx={{ width: 40, pr: 0 }} />
              {/* Bulk select */}
              <TableCell padding="checkbox">
                <Tooltip title={selected.size === activeEntries.length ? 'Deselect all' : 'Select all'}>
                  <input
                    type="checkbox"
                    aria-label="Select all active patients"
                    checked={activeEntries.length > 0 && selected.size === activeEntries.length}
                    onChange={toggleSelectAll}
                    style={{ width: 18, height: 18, cursor: 'pointer' }}
                  />
                </Tooltip>
              </TableCell>
              <TableCell sx={{ width: 48 }}>#</TableCell>
              <TableCell>Patient</TableCell>
              <TableCell>Time</TableCell>
              <TableCell>Visit Type</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Actions</TableCell>
            </TableRow>
          </TableHead>

          <TableBody>
            {/* Loading state */}
            {isLoading && <QueueSkeletonRows />}

            {/* Empty state */}
            {!isLoading && !isError && allActive.length === 0 && (
              <TableRow>
                <TableCell colSpan={8} align="center" sx={{ py: 6 }}>
                  <Stack alignItems="center" spacing={2}>
                    <Typography color="text.secondary">No patients in today's queue.</Typography>
                    <Button
                      variant="contained"
                      startIcon={<AddIcon />}
                      onClick={() => void navigate('/staff/walk-in')}
                    >
                      Book Walk-In
                    </Button>
                  </Stack>
                </TableCell>
              </TableRow>
            )}

            {/* Default state — DnD sortable rows */}
            {!isLoading && allActive.length > 0 && (
              <DndContext
                sensors={sensors}
                collisionDetection={closestCenter}
                onDragEnd={(e) => void handleDragEnd(e)}
              >
                <SortableContext
                  items={allActive.map((e) => e.appointmentId)}
                  strategy={verticalListSortingStrategy}
                >
                  {allActive.map((entry) => (
                    <QueueRow
                      key={entry.appointmentId}
                      entry={entry}
                      selected={selected.has(entry.appointmentId)}
                      onToggleSelect={toggleSelect}
                      onMarkArrived={handleMarkArrived}
                      onMarkLeft={handleMarkLeft}
                      isPending={statusPending}
                    />
                  ))}
                </SortableContext>
              </DndContext>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* ── Toast notifications (UXR-402) ────────────────────────────────── */}
      <Snackbar
        open={toast.open}
        autoHideDuration={4000}
        onClose={() => setToast((t) => ({ ...t, open: false }))}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          severity={toast.severity}
          onClose={() => setToast((t) => ({ ...t, open: false }))}
          variant="filled"
        >
          {toast.message}
        </Alert>
      </Snackbar>
    </Box>
  );
}
