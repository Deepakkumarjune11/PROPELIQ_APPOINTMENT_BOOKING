// API client for POST /api/v1/appointments/{slotId}/register
// Creates or identifies a patient record and associates it with the selected slot.
const BASE_URL = (import.meta.env.VITE_API_URL as string | undefined) ?? '';

export interface PatientRegistrationRequest {
  email: string;
  name: string;
  /** ISO-8601 date string e.g. "1990-06-15" */
  dob: string;
  phone: string;
  insuranceProvider: string;
  insuranceMemberId: string;
}

export interface PatientRegistrationResponse {
  patientId: string;
  insuranceStatus: 'pass' | 'partial-match' | 'fail' | 'pending';
  /** Server-assigned appointment ID — used for PDF download and calendar sync on SCR-006. */
  appointmentId?: string;
}

/** Error shape returned by the API on 409 duplicate-email conflict. */
export interface RegistrationConflictError {
  emailConflictMessage: string;
}

export class RegistrationApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly body?: unknown,
  ) {
    super(message);
    this.name = 'RegistrationApiError';
  }
}

export async function registerPatient(
  slotId: string,
  payload: PatientRegistrationRequest,
): Promise<PatientRegistrationResponse> {
  const response = await fetch(`${BASE_URL}/api/v1/appointments/${encodeURIComponent(slotId)}/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    let body: unknown;
    try {
      body = await response.json();
    } catch {
      // Non-JSON error body — ignore
    }
    throw new RegistrationApiError(response.status, `Registration failed with status ${response.status}`, body);
  }

  return response.json() as Promise<PatientRegistrationResponse>;
}
