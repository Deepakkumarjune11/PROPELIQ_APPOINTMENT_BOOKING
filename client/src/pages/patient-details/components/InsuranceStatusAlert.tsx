// Non-blocking MUI Alert displayed after successful registration.
// Maps insuranceStatus from the API response to an appropriate severity/message.
// pass and pending statuses render nothing — no gate on the booking flow (AC-2, AC-3).
import Alert from '@mui/material/Alert';

import type { PatientRegistrationResponse } from '@/api/registration';

interface InsuranceStatusAlertProps {
  insuranceStatus: PatientRegistrationResponse['insuranceStatus'];
}

export default function InsuranceStatusAlert({ insuranceStatus }: InsuranceStatusAlertProps) {
  if (insuranceStatus === 'partial-match') {
    return (
      <Alert severity="warning" sx={{ mt: 2 }}>
        Insurance details partially matched. Staff may follow up to confirm your coverage.
      </Alert>
    );
  }

  if (insuranceStatus === 'fail') {
    return (
      <Alert severity="info" sx={{ mt: 2 }}>
        We could not verify your insurance on file. Your appointment is confirmed and staff will follow up.
      </Alert>
    );
  }

  // 'pass' and 'pending' — no alert rendered
  return null;
}
