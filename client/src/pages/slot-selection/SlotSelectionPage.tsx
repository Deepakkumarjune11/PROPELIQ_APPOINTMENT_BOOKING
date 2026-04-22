// SCR-002: Slot Selection — step 2 of the booking flow (FL-001).
//
// Receives a single slot via React Router state from SCR-001 SlotCard "Select" click
// ({ state: { slot } }). The slot is immediately stored in booking-store (optimistic).
//
// UXR-403: BookingProgressStepper — step 1 (index 1) active.
// UXR-404: 409 Conflict → revert booking-store selection + show SlotConflictToast.
// UXR-101: keyboard accessible throughout.
// UXR-102: 44px min touch targets on all interactive elements.
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import EditIcon from '@mui/icons-material/Edit';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Grid from '@mui/material/Grid';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

import type { AvailabilitySlot } from '@/api/availability';
import BookingProgressStepper from '@/pages/availability/components/BookingProgressStepper';
import { useBookingStore } from '@/stores/booking-store';
import SelectableSlotCard from './components/SelectableSlotCard';
import SlotConflictToast from './components/SlotConflictToast';

// Narrowed router-state shape arriving from SCR-001
interface SlotSelectionRouterState {
  slot?: AvailabilitySlot;
  slots?: AvailabilitySlot[];
}

// SCR-002: Slot Selection page
export default function SlotSelectionPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const routerState = (location.state ?? {}) as SlotSelectionRouterState;

  const { selectedSlot, hasConflictError, setSelectedSlot, setConflictError, clearBooking } =
    useBookingStore();

  // Toast visibility driven by booking-store conflict flag
  const [toastOpen, setToastOpen] = useState(false);

  // Derive the list of slots to display.
  // Single slot from task_001 "Select" click → wrap in array for grid rendering.
  const slots: AvailabilitySlot[] = routerState.slots
    ? routerState.slots
    : routerState.slot
    ? [routerState.slot]
    : [];

  // Auto-select when a single slot arrives from SCR-001 (optimistic pre-selection).
  useEffect(() => {
    if (routerState.slot && !selectedSlot) {
      setSelectedSlot(routerState.slot);
    }
    // Run only on initial mount to avoid overwriting user changes
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Sync conflict error from store to local toast state
  useEffect(() => {
    if (hasConflictError) {
      setToastOpen(true);
    }
  }, [hasConflictError]);

  const handleSlotSelect = (slot: AvailabilitySlot) => {
    // Optimistic: update store immediately — visual highlight fires before any API call
    setSelectedSlot(slot);
  };

  const handleToastClose = () => {
    setToastOpen(false);
    setConflictError(false);
  };

  const handleBack = () => {
    clearBooking();
    navigate('/appointments/search');
  };

  const handleChangeSlot = () => {
    clearBooking();
    navigate('/appointments/search');
  };

  const handleContinue = () => {
    if (!selectedSlot) return;
    navigate('/appointments/patient-details', { state: { slot: selectedSlot } });
  };

  return (
    <Box component="main" sx={{ maxWidth: 900, mx: 'auto' }}>
      {/* UXR-403: step index 1 = "Select" */}
      <BookingProgressStepper activeStep={1} />

      {/* Back button row */}
      <Button
        startIcon={<ArrowBackIcon />}
        onClick={handleBack}
        sx={{ mb: 2, minHeight: 44 }}
        aria-label="Go back to availability search"
      >
        Back
      </Button>

      <Typography variant="h4" component="h1" gutterBottom>
        Confirm your appointment
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 4 }}>
        Review the appointment details before continuing
      </Typography>

      {/* Selectable slot grid (single slot pre-selected when coming from SCR-001) */}
      {slots.length > 0 ? (
        <Grid container spacing={3} sx={{ mb: 3 }}>
          {slots.map((slot) => (
            <Grid item key={slot.id} xs={12}>
              <SelectableSlotCard
                slot={slot}
                isSelected={selectedSlot?.id === slot.id}
                onSelect={handleSlotSelect}
              />
            </Grid>
          ))}
        </Grid>
      ) : (
        <Typography color="text.secondary" sx={{ mb: 3 }}>
          No slot data. Please go back and select an appointment.
        </Typography>
      )}

      {/* Change slot affordance — navigates back to SCR-001 */}
      <Button
        variant="outlined"
        startIcon={<EditIcon />}
        onClick={handleChangeSlot}
        sx={{ mb: 4, minHeight: 44 }}
        aria-label="Change selected appointment slot"
      >
        Change slot
      </Button>

      {/* Action buttons — matches wireframe .actions layout */}
      <Stack
        direction={{ xs: 'column-reverse', sm: 'row' }}
        spacing={2}
        sx={{ mt: 2 }}
      >
        <Button
          variant="outlined"
          onClick={handleBack}
          fullWidth
          sx={{ minHeight: 44 }}
          aria-label="Cancel and return to search"
        >
          Cancel
        </Button>

        <Button
          variant="contained"
          color="primary"
          onClick={handleContinue}
          disabled={!selectedSlot}
          fullWidth
          sx={{ minHeight: 44 }}
          aria-label="Continue to patient details"
        >
          Continue to patient details
        </Button>
      </Stack>

      {/* UXR-404: 409 Conflict toast — auto-hides after 5 s */}
      <SlotConflictToast open={toastOpen} onClose={handleToastClose} />
    </Box>
  );
}
