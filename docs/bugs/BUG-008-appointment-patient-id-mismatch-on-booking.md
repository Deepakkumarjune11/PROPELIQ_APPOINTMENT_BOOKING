# BUG-008 — Appointment invisible in My Appointments after patient booking

| Field       | Value                                                          |
|-------------|----------------------------------------------------------------|
| **ID**      | BUG-008                                                        |
| **Status**  | Fixed                                                          |
| **Severity**| Critical — primary patient journey fully broken                |
| **Area**    | Booking flow · Identity / patient-record association           |
| **Reported**| 2026-05-06                                                     |

---

## Symptom

After a patient completes the full booking flow (slot selection → patient details → intake → confirmation), the confirmed appointment does **not appear** in the **My Appointments** tab.

---

## Root Cause

`POST /api/v1/appointments/{slotId}/register` identifies the patient by the **email address in the request body** (`AppointmentRegistrationRepository.RegisterAsync` — email-based upsert).

`GET /api/v1/appointments` queries the database with `WHERE patient_id = <JWT sub>`, where `<JWT sub>` is the authenticated patient's `Patient.Id` (set at login via `AuthService.ResolveCredentialAsync`).

**When the email entered in the booking form differs from the patient's login email**, the repository creates a *second* `Patient` entity with a brand-new GUID. The appointment's `PatientId` is set to this new GUID. The authenticated user's JWT `sub` still points to their original `Patient.Id`. The two IDs never match, so `GET /api/v1/appointments` returns an empty list.

```
Patient login  →  JWT sub = Patient.Id (GUID-A)
Booking form   →  email lookup → new Patient (GUID-B)   ← different!
Appointment    →  PatientId = GUID-B
GET /api/v1/appointments → WHERE patient_id = GUID-A  → 0 rows
```

This also constitutes **OWASP A01 (Broken Access Control)** — an authenticated patient could book under any email and "own" appointments that should belong to another identity.

---

## Fix

Added `AuthenticatedPatientId` (`Guid?`) to the registration command/repository chain.

When the caller is a `Patient`-role principal, the controller extracts the patient ID from the JWT `NameIdentifier` claim and passes it to the command. The repository uses this ID directly (attach-by-ID upsert), bypassing the email lookup and guaranteeing the new appointment is linked to the authenticated user.

Staff/anonymous callers omit `AuthenticatedPatientId`; the email-based upsert is preserved for that path (e.g., staff booking on behalf of a walk-in patient).

### Files changed

| File | Change |
|------|--------|
| `AppointmentsController.cs` | Extracts `patientId` from JWT when `role = Patient`; passes as `AuthenticatedPatientId` |
| `RegisterForAppointmentCommand.cs` | Added `Guid? AuthenticatedPatientId = null` parameter |
| `RegisterForAppointmentHandler.cs` | Forwards `cmd.AuthenticatedPatientId` to `RegisterAsync` |
| `IAppointmentRegistrationRepository.cs` | Added `Guid? authenticatedPatientId = null` to `RegisterAsync` |
| `AppointmentRegistrationRepository.cs` | Skips email lookup when `authenticatedPatientId` is present; attaches patient by ID |
| `useRegisterPatient.ts` | (previous fix) Invalidates `['appointments']` cache on success |
| `useSubmitIntake.ts` | (previous fix) Invalidates `['appointments']` cache on success |

---

## Reproduction Steps

1. Log in as `seed-patient-1@dev.local`.
2. Open the booking flow and enter a **different** email address in the Patient Details form.
3. Complete booking through to confirmation.
4. Navigate to **My Appointments** → list is empty.

---

## Verification

After fix: regardless of the email entered in the booking form, the appointment is stored against the authenticated patient's ID and appears in My Appointments immediately after booking.
