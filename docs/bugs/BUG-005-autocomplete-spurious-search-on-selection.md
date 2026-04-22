# Bug Fix Task - BUG-005

## Bug Report Reference

- **Bug ID**: BUG-005
- **Source**: Runtime discovery — selecting a patient from the autocomplete dropdown triggers a second search with the full display label, returning "No patient found"

---

## Bug Summary

### Issue Classification

- **Priority**: High
- **Severity**: Walk-in booking unusable — selecting a patient immediately clears the selection and shows an error
- **Affected Version**: HEAD (main)
- **Environment**: All — browser, Vite dev server

### Steps to Reproduce

1. Login as `seed-staff-front-desk` or `seed-admin-1`
2. Navigate to **Walk-In Booking** (`/staff/walk-in`)
3. Type `seed-patient-1@dev.local` in the patient search field
4. Wait for dropdown — `Alice Dev — seed-patient-1@dev.local` appears
5. Click the option to select it
6. **Expected**: Input shows `Alice Dev`, patient is selected, no further search fires
7. **Actual**:
   - Input briefly shows `Alice Dev`
   - A second `GET /api/v1/patients/search?q=Alice+Dev+%E2%80%94+seed-patient-1%40dev.local` fires
   - API returns `[]` (no match for the full label string)
   - UI shows: *No patient found for "Alice Dev — seed-patient-1@dev.local"*
   - Selected patient is cleared

**Error Output**:

```
GET /api/v1/patients/search?q=Alice+Dev+%E2%80%94+seed-patient-1%40dev.local
→ 200 OK  []   (empty array — ciphertext email doesn't match label string)
```

### Root Cause Analysis

- **File**: `client/src/pages/staff/walk-in/WalkInBookingPage.tsx`
- **Component**: MUI `<Autocomplete>` `onInputChange` handler
- **Cause**: MUI `Autocomplete` fires two events in sequence when a user selects an option:
  1. `onChange(event, value)` — the component calls our handler which sets:
     - `selectedPatient = value`
     - `inputValue = value.fullName` (e.g. `"Alice Dev"`)
  2. `onInputChange(event, newValue, reason="reset")` — MUI immediately overwrites the input with `getOptionLabel(value)` = `"Alice Dev — seed-patient-1@dev.local"` (the full display label)

  The original `onInputChange` handler did not check `reason`, so it accepted the `"reset"` overwrite, set `inputValue = "Alice Dev — seed-patient-1@dev.local"`, and the 300ms debounce triggered a new search with that full label. The search returns empty because the email in the label does not match the `ILIKE` pattern against any `Email` column value.

### Impact Assessment

- **Affected Features**: Walk-in patient booking — `WalkInBookingPage` patient search autocomplete
- **User Impact**: Staff cannot select a patient from the dropdown; the selection is immediately invalidated and a confusing error is shown
- **Data Integrity Risk**: No
- **Security Implications**: None

---

## Fix Overview

Guard `onInputChange` against MUI's internal `"reset"` reason. When `reason === "reset"`, MUI is programmatically resetting the input after a selection — not the user typing. Return early to preserve the `inputValue` set by `onChange`.

---

## Fix Dependencies

- None

---

## Impacted Components

### Frontend — React / TypeScript

- `client/src/pages/staff/walk-in/WalkInBookingPage.tsx` — MODIFIED

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `client/src/pages/staff/walk-in/WalkInBookingPage.tsx` | Add `reason` parameter to `onInputChange`; return early when `reason === 'reset'` |

### Before

```tsx
onInputChange={(_e, value) => {
  setInputValue(value);
  if (selectedPatient && value !== selectedPatient.fullName) {
    setSelectedPatient(null);
  }
}}
```

### After

```tsx
onInputChange={(_e, value, reason) => {
  // "reset" fires after an option is selected — MUI sets the input to
  // getOptionLabel(). We already set inputValue in onChange; skip here
  // to prevent re-triggering a search with the full display label.
  if (reason === 'reset') return;
  setInputValue(value);
  if (selectedPatient && value !== selectedPatient.fullName) {
    setSelectedPatient(null);
  }
}}
```

---

## Implementation Plan

1. Destructure `reason` from the third parameter of `onInputChange`
2. Add early return guard: `if (reason === 'reset') return;`

---

## Regression Prevention Strategy

- [ ] Manual test: select a patient → input shows `fullName` only, no second network request fires
- [ ] Manual test: type a query, select a result, then clear the field (backspace) → `selectedPatient` clears and new search fires
- [ ] Manual test: type a query, select a result, then type a new query → `selectedPatient` clears and new search fires
- [ ] Unit test: simulate `onInputChange` with `reason="reset"` → assert `inputValue` unchanged and no search query update

---

## Rollback Procedure

1. Remove the `reason` guard from `onInputChange` in `WalkInBookingPage.tsx`

---

## External References

- [MUI Autocomplete — `onInputChange` API](https://mui.com/material-ui/api/autocomplete/#autocomplete-prop-onInputChange)
- MUI `AutocompleteInputChangeReason` values: `"input"` | `"reset"` | `"clear"`

---

## Build Commands

```powershell
cd client
npm run dev
```

---

## Implementation Validation Strategy

- [ ] Selecting a patient from the dropdown sets the input to their `fullName` with no further search
- [ ] No second network request fires after selection
- [ ] Browser DevTools Network tab shows only one `GET /api/v1/patients/search` per keystroke, not on selection

## Implementation Checklist

- [x] `reason` parameter added to `onInputChange` signature
- [x] Early return guard `if (reason === 'reset') return;` added
- [ ] Regression test added
