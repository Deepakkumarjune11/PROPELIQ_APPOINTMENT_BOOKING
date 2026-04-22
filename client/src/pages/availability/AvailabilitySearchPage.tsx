// SCR-001: Availability Search — entry point to the booking flow (FL-001 step 2).
// Handles all four UI states: Default/Results, Loading (skeleton), Empty (AC-4), Error.
// UXR-403: BookingProgressStepper shows step 1 (Search) active.
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { useState } from 'react';

import { useAvailability } from '@/hooks/useAvailability';
import AvailabilityEmptyState from './components/AvailabilityEmptyState';
import BookingProgressStepper from './components/BookingProgressStepper';
import DateRangeFilter from './components/DateRangeFilter';
import SlotGrid from './components/SlotGrid';
import SlotGridSkeleton from './components/SlotGridSkeleton';

function getTodayISO(): string {
  return new Date().toISOString().split('T')[0];
}

function getPlusDaysISO(days: number): string {
  return new Date(Date.now() + days * 24 * 60 * 60 * 1000).toISOString().split('T')[0];
}

const DEFAULT_START = getTodayISO();
const DEFAULT_END = getPlusDaysISO(7);

// SCR-001: Availability Search page
export default function AvailabilitySearchPage() {
  // Input state — what the user is editing in the filter
  const [startDate, setStartDate] = useState(DEFAULT_START);
  const [endDate, setEndDate] = useState(DEFAULT_END);
  const [validationError, setValidationError] = useState('');

  // Confirmed search params — drives the React Query fetch
  const [confirmedStart, setConfirmedStart] = useState(DEFAULT_START);
  const [confirmedEnd, setConfirmedEnd] = useState(DEFAULT_END);

  const { slots, isLoading, isError, refetch } = useAvailability(confirmedStart, confirmedEnd);

  const handleStartDateChange = (value: string) => {
    setStartDate(value);
    if (endDate && value > endDate) {
      setValidationError('End date must be on or after start date');
    } else {
      setValidationError('');
    }
  };

  const handleEndDateChange = (value: string) => {
    setEndDate(value);
    if (startDate && value < startDate) {
      setValidationError('End date must be on or after start date');
    } else {
      setValidationError('');
    }
  };

  const handleSearch = () => {
    if (endDate < startDate) {
      setValidationError('End date must be on or after start date');
      return;
    }
    // If dates haven't changed, force a fresh fetch bypassing staleTime
    if (startDate === confirmedStart && endDate === confirmedEnd) {
      void refetch();
    } else {
      setConfirmedStart(startDate);
      setConfirmedEnd(endDate);
    }
  };

  const handleReset = () => {
    // Advance to the next 7-day window so the query key changes and a new API call fires.
    const nextStart = getPlusDaysISO(7);
    const nextEnd   = getPlusDaysISO(14);
    setStartDate(nextStart);
    setEndDate(nextEnd);
    setValidationError('');
    setConfirmedStart(nextStart);
    setConfirmedEnd(nextEnd);
  };

  return (
    <Box component="main" sx={{ maxWidth: 1200, mx: 'auto' }}>
      {/* UXR-403: 3-step booking progress indicator — step 0 (Search) is active */}
      <BookingProgressStepper activeStep={0} />

      <Box sx={{ mb: 4 }}>
        <Typography variant="h4" component="h1" gutterBottom>
          Find your appointment
        </Typography>
        <Typography variant="body1" color="text.secondary">
          Search available time slots and book your visit
        </Typography>
      </Box>

      <DateRangeFilter
        startDate={startDate}
        endDate={endDate}
        onStartDateChange={handleStartDateChange}
        onEndDateChange={handleEndDateChange}
        onSearch={handleSearch}
        isSearching={isLoading}
        validationError={validationError}
      />

      {/* Loading state — 8 skeleton shimmer cards */}
      {isLoading && <SlotGridSkeleton />}

      {/* Error state — alert with retry */}
      {isError && !isLoading && (
        <Alert
          severity="error"
          action={
            <Button
              color="inherit"
              size="small"
              onClick={() => void refetch()}
              aria-label="Retry loading availability"
            >
              Retry
            </Button>
          }
        >
          Unable to load availability. Please try again.
        </Alert>
      )}

      {/* Empty state — AC-4: no slots for selected range */}
      {!isLoading && !isError && slots.length === 0 && (
        <AvailabilityEmptyState onReset={handleReset} />
      )}

      {/* Default / results state */}
      {!isLoading && !isError && slots.length > 0 && (
        <SlotGrid slots={slots} />
      )}
    </Box>
  );
}
