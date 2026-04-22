// API client for POST /api/v1/patients/{patientId}/intake
// Submits the collected intake answers with the active intake mode tag.
const BASE_URL = (import.meta.env.VITE_API_URL as string | undefined) ?? '';

export interface IntakeSubmissionRequest {
  /** Canonical question-answer map: questionId → free-text answer. */
  answers: Record<string, string>;
  /** Which UI mode was active when the patient submitted. */
  mode: 'manual' | 'conversational';
}

export interface IntakeSubmissionResponse {
  intakeResponseId: string;
}

export async function submitIntake(
  patientId: string,
  payload: IntakeSubmissionRequest,
): Promise<IntakeSubmissionResponse> {
  const response = await fetch(
    `${BASE_URL}/api/v1/patients/${encodeURIComponent(patientId)}/intake`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    },
  );

  if (!response.ok) {
    throw new Error(`Intake submission failed with status ${response.status}`);
  }

  return response.json() as Promise<IntakeSubmissionResponse>;
}
