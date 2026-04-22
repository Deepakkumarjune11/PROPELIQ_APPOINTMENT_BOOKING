# Bug Fix Task - BUG-002

## Bug Report Reference

- **Bug ID**: BUG-002
- **Source**: Runtime discovery — `GET /api/v1/patients/search` returns empty results even when patients exist

---

## Bug Summary

### Issue Classification

- **Priority**: High
- **Severity**: Patient search silently returns 0 results for all phone-based queries; name sorting broken
- **Affected Version**: HEAD (main)
- **Environment**: Dev — all environments using PHI column encryption

### Steps to Reproduce

1. Ensure seed patients exist (`Alice Dev`, `Bob Dev`)
2. Login as `seed-staff-front-desk`
3. `GET /api/v1/patients/search?q=555-0001` (seed phone)
4. **Expected**: Returns Alice Dev's record
5. **Actual**: Empty array `[]`

Also:

1. `GET /api/v1/patients/search?q=alice` (partial name)
2. **Expected**: Returns Alice Dev
3. **Actual**: Empty array `[]`

### Root Cause Analysis

- **File**: `server/src/Modules/PatientAccess/PatientAccess.Data/Repositories/PatientStaffRepository.cs` (lines 28–37)
- **Component**: Patient search — `SearchByEmailOrPhoneAsync`
- **Function**: `SearchByEmailOrPhoneAsync`
- **Cause**:
  1. `Phone` and `Name` columns are **PHI-encrypted** at rest via `PhiEncryptedConverter` (AES-256 via Data Protection API). The database stores opaque ciphertext blobs — not plaintext strings.
  2. `EF.Functions.ILike(p.Phone, pattern)` pushes `ILIKE '%555-0001%'` to PostgreSQL, which runs against ciphertext. Ciphertext never matches a plaintext pattern → always 0 rows.
  3. `OrderBy(p => p.Name)` sorts by encrypted bytes — results are returned in arbitrary ciphertext order, not alphabetical.

### Impact Assessment

- **Affected Features**: Walk-in patient lookup, staff patient search panel, new patient creation deduplication
- **User Impact**: Staff cannot find existing patients by phone number or name — forced to always enter full exact email
- **Data Integrity Risk**: No — read-only query issue, no data corruption
- **Security Implications**: None — the encryption itself is correct; only the query predicate is wrong

---

## Fix Overview

Restrict the search predicate to the `Email` column only (the only **unencrypted** patient identifier). Remove the `ILike` on `Phone` and `Name`. Change `OrderBy` to use `Email` (plaintext).

If phone/name search is required in the future, a dedicated search index (e.g. trigram on decrypted values via a DB function or an application-side approach) is needed.

---

## Fix Dependencies

- BUG-001 must be resolved first (correct Data Protection discriminator) or all reads will throw `CryptographicException` regardless

---

## Impacted Components

### Backend — .NET 8

- `PatientAccess.Data/Repositories/PatientStaffRepository.cs` — MODIFIED

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Repositories/PatientStaffRepository.cs` | Remove `ILike` on `Phone` and `Name`; search `Email` only; `OrderBy(p => p.Email)` |

---

## Implementation Plan

1. In `SearchByEmailOrPhoneAsync`, replace:
   ```csharp
   .Where(p => !p.IsDeleted &&
               (EF.Functions.ILike(p.Email, pattern) ||
                EF.Functions.ILike(p.Phone, pattern)))
   .OrderBy(p => p.Name)
   ```
   with:
   ```csharp
   .Where(p => EF.Functions.ILike(p.Email, pattern))
   .OrderBy(p => p.Email)
   ```
   Note: `HasQueryFilter(p => !p.IsDeleted)` on `Patient` already excludes soft-deleted rows — explicit `!p.IsDeleted` is redundant.

---

## Regression Prevention Strategy

- [ ] Unit test: `SearchByEmailOrPhoneAsync("seed-patient-1")` returns exactly 1 result
- [ ] Unit test: phone query `SearchByEmailOrPhoneAsync("555-0001")` returns empty (expected — phone is encrypted)
- [ ] Integration test: search via email partial match returns correct patient

---

## Rollback Procedure

1. Revert `PatientStaffRepository.cs` to original `ILike` predicate

---

## External References

- [EF Core — Value Converters](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions)
- PHI column encryption: `PatientAccess.Data/Converters/PhiEncryptedConverter.cs`

---

## Build Commands

```powershell
cd server/src/Modules/PatientAccess/PatientAccess.Data
dotnet build -v minimal
```

---

## Implementation Validation Strategy

- [ ] `GET /api/v1/patients/search?q=seed-patient-1%40dev.local` returns Alice Dev
- [ ] `GET /api/v1/patients/search?q=seed-patient` returns both seed patients
- [ ] Phone/name queries return `[]` (documented behaviour — encrypted columns)

## Implementation Checklist

- [x] Removed `ILike` on `Phone` from `SearchByEmailOrPhoneAsync`
- [x] Changed `OrderBy` from `p.Name` to `p.Email`
- [x] Removed redundant `!p.IsDeleted` predicate (covered by `HasQueryFilter`)
- [ ] Regression test added
