// Zustand store for the multi-step booking session (SCR-002 → SCR-003 → SCR-006).
// Persists selected slot across the booking flow and surfaces conflict errors from 409 responses.
import { create } from 'zustand';

import type { AvailabilitySlot } from '@/api/availability';

/** Patient contact and insurance fields collected on SCR-003. patientId is populated from the API response. */
export interface PatientDetailsFields {
  /** Server-assigned patient ID — populated after successful registration response. */
  patientId: string;
  email: string;
  name: string;
  dob: string;
  phone: string;
  insuranceProvider: string;
  insuranceMemberId: string;
}

interface BookingState {
  selectedSlot: AvailabilitySlot | null;
  /** True when a 409 Conflict was received for the selected slot. Consumed by SlotConflictToast. */
  hasConflictError: boolean;
  /** Patient details captured on SCR-003; null until the form is submitted successfully. */
  patientDetails: PatientDetailsFields | null;
  /** Server-assigned appointment ID from the registration response; used for PDF + calendar calls. */
  appointmentId: string | null;
  setSelectedSlot: (slot: AvailabilitySlot) => void;
  /** Clears the selected slot without resetting the rest of booking state (UXR-404 rollback). */
  clearSelectedSlot: () => void;
  setConflictError: (value: boolean) => void;
  setPatientDetails: (details: PatientDetailsFields) => void;
  setAppointmentId: (id: string) => void;
  /** Resets the full booking flow state on successful completion (called from SCR-006). */
  clearIntake: () => void;
  clearBooking: () => void;
}

export const useBookingStore = create<BookingState>((set) => ({
  selectedSlot: null,
  hasConflictError: false,
  patientDetails: null,
  appointmentId: null,

  setSelectedSlot: (slot) =>
    set({ selectedSlot: slot, hasConflictError: false }),

  clearSelectedSlot: () =>
    set({ selectedSlot: null }),

  setConflictError: (value) =>
    set({ hasConflictError: value }),

  setPatientDetails: (details) =>
    set({ patientDetails: details }),

  setAppointmentId: (id) =>
    set({ appointmentId: id }),

  clearIntake: () =>
    set({ selectedSlot: null, patientDetails: null, appointmentId: null, hasConflictError: false }),

  clearBooking: () =>
    set({ selectedSlot: null, hasConflictError: false, patientDetails: null, appointmentId: null }),
}));
