/**
 * Permission bitmask constants shared across the frontend.
 * Each constant maps to a single bit in Staff.PermissionsBitfield (PatientAccess.Data).
 * Bit position matches the backend PermissionsBitfield convention.
 */
export const Permissions = {
  ViewPatientCharts:  1 << 0,  // 1
  VerifyClinicalData: 1 << 1,  // 2
  ManageAppointments: 1 << 2,  // 4
  UploadDocuments:    1 << 3,  // 8
  ViewMetrics:        1 << 4,  // 16
} as const;

export type PermissionKey = keyof typeof Permissions;
