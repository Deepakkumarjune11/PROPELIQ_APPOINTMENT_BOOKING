# Bug Fix Task - BUG-004

## Bug Report Reference

- **Bug ID**: BUG-004
- **Source**: Runtime discovery — `GET /api/v1/staff/dashboard/summary` → HTTP 403 when logged in as `seed-admin-1`

---

## Bug Summary

### Issue Classification

- **Priority**: High
- **Severity**: Admin users blocked from staff dashboard and walk-in endpoints
- **Affected Version**: HEAD (main)
- **Environment**: All environments

### Steps to Reproduce

1. Login as `seed-admin-1` (role = `Admin`)
2. Navigate to Staff Dashboard
3. `GET /api/v1/staff/dashboard/summary`
4. **Expected**: 200 OK with dashboard summary counts
5. **Actual**: 403 Forbidden

Also affects:
- `POST /api/v1/appointments/walk-in`
- `GET /api/v1/staff/queue`
- `PATCH /api/v1/staff/queue/reorder`
- `GET /api/v1/patients/search`
- `POST /api/v1/patients`

### Root Cause Analysis

- **File**: `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/StaffController.cs` (line ~24)
- **File**: `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/PatientsController.cs` (lines ~47, ~82)
- **Component**: Authorization policy — `[Authorize(Roles = ...)]`
- **Cause**: `StaffController` and several actions in `PatientsController` declare `[Authorize(Roles = "Staff")]`. The JWT issued for `seed-admin-1` carries `ClaimTypes.Role = "Admin"`. ASP.NET Core role-based auth requires an exact match — `"Admin"` does not satisfy `"Staff"` → 403.

  Admins are superusers in this platform and should be permitted to perform all staff operations (BRD requirement).

### Impact Assessment

- **Affected Features**: Staff dashboard, walk-in booking, queue management, patient search — all inaccessible to Admin role
- **User Impact**: Admin users (`seed-admin-1`) receive 403 on every staff-side screen
- **Data Integrity Risk**: No
- **Security Implications**: Fix must not grant Admin access to patient-scope endpoints that should remain patient-only

---

## Fix Overview

Extend `[Authorize(Roles = "Staff")]` to `[Authorize(Roles = "Staff,Admin")]` on `StaffController` (class level) and the two staff-only actions in `PatientsController` (`SearchPatients`, `CreatePatientByStaff`).

---

## Fix Dependencies

- None

---

## Impacted Components

### Backend — .NET 8

- `PatientAccess.Presentation/Controllers/StaffController.cs` — MODIFIED
- `PatientAccess.Presentation/Controllers/PatientsController.cs` — MODIFIED

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/StaffController.cs` | `[Authorize(Roles = "Staff")]` → `[Authorize(Roles = "Staff,Admin")]` on class |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/PatientsController.cs` | `[Authorize(Roles = "Staff")]` → `[Authorize(Roles = "Staff,Admin")]` on `SearchPatients` and `CreatePatientByStaff` actions |

---

## Implementation Plan

1. In `StaffController.cs`, change the class-level attribute:
   ```csharp
   [Authorize(Roles = "Staff,Admin")]
   public sealed class StaffController : ControllerBase
   ```
2. In `PatientsController.cs`, update both staff-restricted actions:
   ```csharp
   [Authorize(Roles = "Staff,Admin")]
   public async Task<IActionResult> SearchPatients(...)

   [Authorize(Roles = "Staff,Admin")]
   public async Task<IActionResult> CreatePatientByStaff(...)
   ```

---

## Regression Prevention Strategy

- [ ] Integration test: `seed-admin-1` token returns 200 on `GET /api/v1/staff/dashboard/summary`
- [ ] Integration test: `seed-admin-1` token returns 200 on `GET /api/v1/patients/search?q=seed`
- [ ] Integration test: `seed-staff-front-desk` token still returns 200 on all above (no regression)
- [ ] Integration test: `seed-patient-*` token (role = Patient if applicable) still returns 403

---

## Rollback Procedure

1. Revert `[Authorize(Roles = "Staff,Admin")]` back to `[Authorize(Roles = "Staff")]` on both controllers

---

## External References

- [ASP.NET Core — Role-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles)

---

## Build Commands

```powershell
cd server/src/Modules/PatientAccess/PatientAccess.Presentation
dotnet build -v minimal
```

---

## Implementation Validation Strategy

- [ ] `seed-admin-1` can access staff dashboard without 403
- [ ] `seed-admin-1` can search patients
- [ ] `seed-staff-front-desk` retains full staff access
- [ ] All existing tests pass

## Implementation Checklist

- [x] `StaffController` class attribute updated to `"Staff,Admin"`
- [ ] `PatientsController` `SearchPatients` action updated to `"Staff,Admin"`
- [ ] `PatientsController` `CreatePatientByStaff` action updated to `"Staff,Admin"`
- [ ] Regression tests added
