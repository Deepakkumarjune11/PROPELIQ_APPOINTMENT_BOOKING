// API client for conflict detection and resolution (US_022).
// GET  /api/v1/patients/{patientId}/conflicts          — unresolved conflict list
// POST /api/v1/360-view/{view360Id}/resolve-conflict   — resolve a single conflict
import api from '@/lib/api';

// ── Domain types ──────────────────────────────────────────────────────────────

/** A single conflicting source value within a ConflictItem. */
export interface ConflictSource {
  documentId: string;
  documentName: string;
  value: string;
  confidenceScore: number;
}

/** A detected conflict between two or more facts of the same FactType (AIR-004, FR-013). */
export interface ConflictItemDto {
  conflictId: string;
  view360Id: string;
  factType: string;
  sources: ConflictSource[];
}

/** Resolution choice sent to the server. */
export type ResolutionChoice = 'sourceA' | 'sourceB' | 'manual';

/** Request body for POST /api/v1/360-view/{view360Id}/resolve-conflict. */
export interface ResolveConflictPayload {
  view360Id: string;
  conflictId: string;
  resolution: ResolutionChoice;
  /** Only required when resolution === 'manual'. */
  manualValue?: string;
  /** Free-text staff justification — required for manual override. */
  justification: string;
}

// ── API functions ─────────────────────────────────────────────────────────────

export async function getConflicts(patientId: string): Promise<ConflictItemDto[]> {
  const res = await api.get<ConflictItemDto[]>(`/api/v1/patients/${encodeURIComponent(patientId)}/conflicts`);
  return res.data;
}

export async function resolveConflict(payload: ResolveConflictPayload): Promise<void> {
  await api.post(`/api/v1/360-view/${encodeURIComponent(payload.view360Id)}/resolve-conflict`, payload);
}
