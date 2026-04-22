// API client for 360-degree patient view (US_021).
// GET /api/v1/patients/{patientId}/360-view  — consolidated fact summary per category
// GET /api/v1/facts/{factId}/source          — source document text + citation offsets
import api from '@/lib/api';

// ── Domain types ──────────────────────────────────────────────────────────────

/** FactType values aligned with the backend FactType enum (DR-005). */
export type FactType = 'Vitals' | 'Medications' | 'History' | 'Diagnoses' | 'Procedures';

export interface FactSource {
  factId: string;
  documentId: string;
  documentName: string;
  sourceCharOffset: number;
  sourceCharLength: number;
}

/** A single AI-extracted fact after de-duplication and consolidation (FR-012). */
export interface ConsolidatedFact {
  factId: string;
  factType: FactType;
  /** Decrypted/display value — server decrypts before returning. */
  value: string;
  confidenceScore: number;
  sources: FactSource[];
}

/** Patient identity header data returned by the 360-view endpoint. */
export interface PatientIdentity {
  patientId: string;
  fullName: string;
  dateOfBirth: string;      // ISO-8601 date
  mrn: string;
  insuranceName?: string;
  insuranceMemberId?: string;
}

/** Response shape for GET /api/v1/patients/{patientId}/360-view. */
export interface PatientView360Dto {
  patient: PatientIdentity;
  conflictCount: number;
  facts: ConsolidatedFact[];
  assemblyStatus: 'assembled' | 'pending' | 'empty';
}

/** Response shape for GET /api/v1/facts/{factId}/source. */
export interface SourceCitationDto {
  factId: string;
  documentId: string;
  documentName: string;
  uploadedAt: string;       // ISO-8601 datetime
  sourceText: string;
  sourceCharOffset: number;
  sourceCharLength: number;
  confidenceScore: number;
}

/** A row in the SCR-016 verification queue. */
export interface VerificationQueueEntry {
  patientId: string;
  patientName: string;
  mrn: string;
  appointmentDatetime: string;  // ISO-8601
  documentCount: number;
  conflictCount: number;
  priority: 'conflict' | 'pending';
}

// ── API functions ─────────────────────────────────────────────────────────────

export async function getPatientView360(patientId: string): Promise<PatientView360Dto> {
  const res = await api.get<PatientView360Dto>(`/api/v1/patients/${encodeURIComponent(patientId)}/360-view`);
  return res.data;
}

export async function getFactSource(factId: string): Promise<SourceCitationDto> {
  const res = await api.get<SourceCitationDto>(`/api/v1/facts/${encodeURIComponent(factId)}/source`);
  return res.data;
}

export async function getVerificationQueue(): Promise<VerificationQueueEntry[]> {
  const res = await api.get<VerificationQueueEntry[]>('/api/v1/staff/verification-queue');
  return res.data;
}
