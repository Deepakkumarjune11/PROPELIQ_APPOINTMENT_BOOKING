// API client for POST /api/v1/patients/{patientId}/intake
// Submits the collected intake answers with the active intake mode tag.
import api from '@/lib/api';

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
  const response = await api.post<IntakeSubmissionResponse>(
    `/api/v1/patients/${encodeURIComponent(patientId)}/intake`,
    payload,
  );
  return response.data;
}
