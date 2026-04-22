// UXR-403: Progress stepper for the 4-step booking flow (Search → Select → Details → Intake).
import Box from '@mui/material/Box';
import Step from '@mui/material/Step';
import StepLabel from '@mui/material/StepLabel';
import Stepper from '@mui/material/Stepper';

const BOOKING_STEPS = ['Search', 'Select', 'Details', 'Intake'] as const;

interface BookingProgressStepperProps {
  /** Zero-based index of the currently active step. */
  activeStep: number;
}

export default function BookingProgressStepper({ activeStep }: BookingProgressStepperProps) {
  return (
    <Box sx={{ mb: 4 }} aria-label="Booking progress">
      <Stepper activeStep={activeStep} alternativeLabel>
        {BOOKING_STEPS.map((label) => (
          <Step key={label}>
            <StepLabel>{label}</StepLabel>
          </Step>
        ))}
      </Stepper>
    </Box>
  );
}
