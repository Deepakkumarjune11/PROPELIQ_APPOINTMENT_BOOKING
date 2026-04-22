// API client for ICD-10/CPT code suggestions and confirmation (US_023, FR-014).
// GET  /api/v1/patients/{patientId}/code-suggestions — list suggestions per patient
// POST /api/v1/code-suggestions/confirm              — accept/reject a code suggestion
import api from '@/lib/api';

// ── Domain types ──────────────────────────────────────────────────────────────

/** Code type determines badge colour: ICD-10 = Diagnoses purple, CPT = Procedures teal. */
export type CodeType = 'ICD-10' | 'CPT';

/** Review outcome recorded per FR-014 after staff action. */
export type ReviewOutcome = 'accepted' | 'rejected';

/** One evidence fact linked to a code suggestion (AIR-005). */
export interface EvidenceFact {
  /** GUID of the ExtractedFact — used by useFactSource for citation drawer. */
  factId: string;
  /** Short label shown inside the breadcrumb chip (e.g. "BP: 120/80 mmHg"). */
  factSummary: string;
}

/** A single AI-generated code suggestion row (US_023, FR-014, AIR-005). */
export interface CodeSuggestionDto {
  /** Suggestion row GUID. */
  id: string;
  /** Code value (e.g. "I10", "99213"). */
  code: string;
  /** Human-readable description. */
  description: string;
  /** "ICD-10" or "CPT". */
  codeType: CodeType;
  /** AI extraction confidence (0–1). */
  confidenceScore: number;
  /** Evidence facts that the AI used to generate this code (AIR-005). */
  evidenceFacts: EvidenceFact[];
  /** True after staff reviews via Accept or Reject. */
  staffReviewed: boolean;
  /** Populated after staff review (FR-014). */
  reviewOutcome: ReviewOutcome | null;
  /** Staff-entered justification for a rejection (UC-005 edge case). */
  justification?: string;
}

/** Payload for POST /api/v1/code-suggestions/confirm. */
export interface ConfirmCodePayload {
  codeId: string;
  reviewOutcome: ReviewOutcome;
  justification?: string;
}

// ── API functions ─────────────────────────────────────────────────────────────

export async function getCodeSuggestions(patientId: string): Promise<CodeSuggestionDto[]> {
  const res = await api.get<CodeSuggestionDto[]>(`/api/v1/patients/${encodeURIComponent(patientId)}/code-suggestions`);
  return res.data;
}

export async function confirmCode(payload: ConfirmCodePayload): Promise<void> {
  await api.post('/api/v1/code-suggestions/confirm', payload);
}

export async function patchView360Status(
  view360Id: string,
  status: string,
): Promise<void> {
  await api.patch(`/api/v1/360-view/${encodeURIComponent(view360Id)}/status`, { status });
}
