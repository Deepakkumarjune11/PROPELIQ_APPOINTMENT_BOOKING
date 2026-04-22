# Bug Fix Task - BUG-001

## Bug Report Reference

- **Bug ID**: BUG-001
- **Source**: Runtime discovery — `GET /api/v1/patients/search` → HTTP 500

---

## Bug Summary

### Issue Classification

- **Priority**: Critical
- **Severity**: All patient PHI columns unreadable; every patient search/read returns 500
- **Affected Version**: HEAD (main)
- **Environment**: Dev — IIS Express / `dotnet run`, PostgreSQL 15, .NET 8

### Steps to Reproduce

1. Start the API (`F5` in Visual Studio or `dotnet run`)
2. Login as any staff user (e.g. `seed-staff-front-desk`)
3. `GET /api/v1/patients/search?q=seed-patient`
4. **Expected**: 200 OK with patient list
5. **Actual**: 500 Internal Server Error — `CryptographicException: The payload was invalid`

**Error Output**:

```text
System.Security.Cryptography.CryptographicException: The payload was invalid.
   at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.UnprotectCore(...)
   at PatientAccess.Data.Converters.PhiEncryptedConverter.<>c__DisplayClass0_0.<.ctor>b__1(String ciphertext)
   at PatientAccess.Data.Repositories.PatientStaffRepository.SearchByEmailOrPhoneAsync(...)
```

### Root Cause Analysis

- **File 1**: `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` (line ~55)
- **File 2**: `server/src/PropelIQ.Api/Program.cs` (line 392)
- **Component**: Data Protection / PHI column encryption
- **Cause**: Two separate `AddDataProtection()` registrations with **different** `SetApplicationName` values:
  - `ServiceCollectionExtensions.cs` → `.SetApplicationName("PropelIQ")`
  - `Program.cs` → `.SetApplicationName("propeliq-phi")`

  ASP.NET Core Data Protection uses the application name as the purpose discriminator for key isolation.
  The **last registration wins** at runtime (`"propeliq-phi"`).
  Any data seeded or written while `"PropelIQ"` was active cannot be decrypted with `"propeliq-phi"`, causing a `CryptographicException` on every read of PHI columns (`Name`, `Phone`, `ConsolidatedFacts`, etc.).

### Impact Assessment

- **Affected Features**: Patient search, walk-in booking, patient view 360, intake, documents — any feature that reads PHI columns
- **User Impact**: All staff/admin users receive 500 on any patient-related query
- **Data Integrity Risk**: Yes — existing PHI data persisted with old discriminator is permanently unreadable unless re-encrypted
- **Security Implications**: OWASP A02 — inadvertent key isolation break; previously encrypted PHI data may appear "corrupted"

---

## Fix Overview

Remove the duplicate `AddDataProtection()` call from `ServiceCollectionExtensions.cs`. The canonical, fully-configured registration in `Program.cs` (with `PersistKeysToFileSystem`, `SetDefaultKeyLifetime`, and `SetApplicationName("propeliq-phi")`) is the authoritative one and must be the only registration.

Additionally truncate and re-seed patient rows so the seeder re-encrypts them with the correct discriminator.

---

## Fix Dependencies

- None — self-contained

---

## Impacted Components

### Backend — .NET 8

- `PatientAccess.Presentation/ServiceCollectionExtensions.cs` — MODIFIED (duplicate removed)
- Dev database `patient` table — DATA (re-seed required)

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Remove `services.AddDataProtection().SetApplicationName("PropelIQ")` block (~5 lines) |
| DATA   | PostgreSQL `patient` table | Truncate seed rows; restart API to trigger DevelopmentDataSeeder re-seed |

---

## Implementation Plan

1. Delete the `AddDataProtection()` call from `ServiceCollectionExtensions.cs` (keep only the Program.cs version)
2. Run SQL to delete stale seed patients and their appointments:
   ```sql
   DELETE FROM appointment WHERE "PatientId" IN (
       SELECT "Id" FROM patient WHERE "Email" LIKE 'seed-patient-%'
   );
   DELETE FROM patient WHERE "Email" LIKE 'seed-patient-%';
   ```
3. Restart API — `DevelopmentDataSeeder.SeedAsync` recreates patients with the correct key

---

## Regression Prevention Strategy

- [ ] Unit test: assert `IDataProtectionProvider` is registered exactly once in the DI container
- [ ] Integration test: `GET /api/v1/patients/search?q=seed-patient` returns 200 after a clean seed cycle
- [ ] Edge case: verify decryption succeeds after a server restart (key ring persistence)

---

## Rollback Procedure

1. Restore the `AddDataProtection()` call in `ServiceCollectionExtensions.cs` if needed
2. Re-truncate and re-seed patients so all rows use the same discriminator

---

## External References

- [ASP.NET Core Data Protection — Application Isolation](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview#setapplicationname)

---

## Build Commands

```powershell
cd server/src/PropelIQ.Api
dotnet build -v minimal
```

---

## Implementation Validation Strategy

- [ ] `GET /api/v1/patients/search?q=seed-patient` returns 200 with patient list
- [ ] No `CryptographicException` in server logs
- [ ] All existing tests pass

## Implementation Checklist

- [x] Removed duplicate `AddDataProtection()` from `ServiceCollectionExtensions.cs`
- [ ] Stale seed data truncated and re-seeded
- [ ] Regression test added
