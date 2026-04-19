# Task - task_001_fe_user_management_ui

## Requirement Reference

- **User Story**: US_025 — Admin User Lifecycle Management
- **Story Location**: `.propel/context/tasks/EP-005/us_025/us_025.md`
- **Acceptance Criteria**:
  - AC-1: SCR-021 renders a searchable, filterable table of all user accounts (name, email, role badge, status, actions) accessible only under `<RoleGuard roles={['admin']}>` per FR-015.
  - AC-2: "Create User" opens SCR-022 modal; valid form submission calls `POST /api/v1/admin/users`, closes modal, and updates the table via `invalidateQueries` per UC-006.
  - AC-3: Edit icon on a row opens SCR-022 pre-populated; "Assign Role" opens SCR-023; on save the table row updates optimistically and the backend is called per UC-006.
  - AC-4: "Disable" icon triggers a confirmation `Dialog`; on confirm calls `PATCH /api/v1/admin/users/{id}/disable`; row status changes to "Disabled" with a muted style and action buttons removed per FR-015.
  - AC-5: Conflicting role/permission combinations detected by the API return a 422 response shown as an inline `Alert` inside the modal ("Conflict: {error_message}") per UC-006 extension 2a.
- **Edge Cases**:
  - Admin attempts to disable their own account → API returns 400; modal shows "You cannot disable your own account."
  - Search input debounced 300ms; filters applied client-side on existing page data (no additional API calls).
  - Disabled user row has "Disable" action replaced with "Enable" (re-activate) — `PATCH /api/v1/admin/users/{id}/enable`.
  - Empty state (no users) → table shows centred "No users found" with a "Create user" CTA.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-021-user-management.html`, `.propel/context/wireframes/Hi-Fi/wireframe-SCR-022-create-edit-user.html`, `.propel/context/wireframes/Hi-Fi/wireframe-SCR-023-role-assignment.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-021`, `.propel/context/docs/figma_spec.md#SCR-022`, `.propel/context/docs/figma_spec.md#SCR-023` |
| **UXR Requirements** | UXR-502 |
| **Design Tokens** | `designsystem.md#colors` (primary `#2196F3`, success `#4CAF50`, neutral scale); `designsystem.md#typography` (Roboto, 8px grid) |

### CRITICAL: Wireframe Implementation Requirement

**Wireframe Status = AVAILABLE:**
- **MUST** open and reference all three wireframe files during implementation

- **SCR-021 key details** (`wireframe-SCR-021-user-management.html`):
  - Page layout: sidebar (240px, `border-right: 1px solid #E0E0E0`) + main content area
  - Header: `h1 "User management"` left + "Create user" primary button right (`min-height: 44px`)
  - Search bar: `width: 300px`, `border: 1px solid #E0E0E0`, `border-radius: 4px`, `placeholder: "Search users by name, email, or role"`
  - MUI `Table`: columns Name / Email / Role / Status / Actions; `th background: #FAFAFA; border-bottom: 2px solid #E0E0E0`; row hover `#F5F5F5`
  - Role badges: Staff → `background: #2196F3`; Admin → `background: #4CAF50`; text white, `border-radius: 4px`, `12px 500 weight`
  - Actions column: Edit icon (`edit`) + Disable icon (`block`) as `IconButton`; icon colour `#9E9E9E`
  - States: Default, Loading (Skeleton rows), Empty (centred message), Error (Alert)

- **SCR-022 key details** (`wireframe-SCR-022-create-edit-user.html`):
  - MUI `Dialog` modal: `max-width: 500px`, `border-radius: 8px`, `box-shadow: elevation-16`
  - Header: title + close `IconButton` (`close` icon, `#9E9E9E`)
  - Fields: Full name `TextField` + Email `TextField` + Role `Select` (Patient/Staff/Admin) + Department `TextField` (optional)
  - Footer: "Cancel" outlined + "Create user" / "Save changes" contained primary
  - Validation triggers `onBlur` (UXR-502); error text below field
  - 422 conflict → `Alert severity="error"` above footer ("Conflict: {error_message}")

- **SCR-023 key details** (`wireframe-SCR-023-role-assignment.html`):
  - MUI `Dialog` modal: `max-width: 500px`, `border-radius: 8px`
  - Role `Select` (Staff/Admin/Patient) with current value pre-selected
  - Permissions group: `FormGroup` of `FormControlLabel` wrapping `Checkbox` items; each item in `background: #FAFAFA; border-radius: 4px; padding: 16px`
  - 5 permissions: View patient charts / Verify clinical data / Manage appointments / Upload documents / View metrics
  - Each checkbox has a `Tooltip` explaining the permission on hover (UXR-003)
  - Footer: "Cancel" + "Save changes" primary
  - 422 conflict → `Alert severity="error"` above footer

- **MUST** validate at 375px (modal full width on mobile), 768px, 1440px
- Run `/analyze-ux` after implementation to verify wireframe alignment

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| UI Components | Material-UI (MUI) | 5.x |
| State Management | React Query + Zustand | 4.x / 4.x |
| HTTP Client | Axios | 1.x |
| Routing | React Router | 6.x |
| Language | TypeScript | 5.x |
| Build Tool | Vite | 5.x |

> Route `/admin/users` must be inside `<RoleGuard roles={['admin']}>` (created in US_024/task_001). `useAuthStore` is used for the self-disable guard (compare current user id against target user id before showing Disable button).

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Task Overview

Build three admin-scoped screens for user lifecycle management:

**SCR-021 — `UserManagementPage.tsx`** (`/admin/users`):
- Fetches all users via `useAdminUsers()` (`GET /api/v1/admin/users`); `staleTime: 30_000`.
- Loading state: 5 `Skeleton` table rows. Empty state: centred Typography + "Create user" `Button`.
- Search `TextField` debounced 300ms; filters the `users` array in-memory (name/email/role substring match).
- Role badges: Staff blue (`#2196F3`), Admin green (`#4CAF50`), Patient neutral (`#9E9E9E`) using MUI `Chip`.
- Actions per row: Edit (`IconButton`) → opens `CreateEditUserModal` pre-populated; Assign Role (`IconButton` `manage_accounts`) → opens `RoleAssignmentModal`; Disable/Enable (`IconButton` `block`/`check_circle`) — if target user is current admin user, button is disabled + `Tooltip` "You cannot disable your own account".
- Confirmation `Dialog` for Disable/Enable: "Disable [name]?" + "This will immediately terminate their active sessions." + Confirm/Cancel.
- On successful disable/enable → `invalidateQueries(['adminUsers'])`.

**SCR-022 — `CreateEditUserModal.tsx`** (MUI Dialog, triggered from SCR-021):
- Props: `open`, `onClose`, `user?: AdminUserDto` (null = create mode, populated = edit mode).
- Fields: Full Name, Email, Role `Select`, Department (optional). All validated `onBlur`.
- `useCreateUser` mutation (`POST /api/v1/admin/users`) or `useUpdateUser` (`PUT /api/v1/admin/users/{id}`).
- On 422 response: show `Alert severity="error"` with `error.response.data.message`.
- On success: `invalidateQueries(['adminUsers'])`, `onClose()`.

**SCR-023 — `RoleAssignmentModal.tsx`** (MUI Dialog, triggered from SCR-021):
- Props: `open`, `onClose`, `userId: string`, `currentRole: string`, `currentPermissions: number`.
- Role `Select` pre-populated. `FormGroup` with 5 permission `Checkbox` items (each with `Tooltip`).
- Permissions bitmask constants (bit 0 = ViewPatientCharts, bit 1 = VerifyClinicalData, bit 2 = ManageAppointments, bit 3 = UploadDocuments, bit 4 = ViewMetrics).
- `useAssignRole` mutation (`PATCH /api/v1/admin/users/{id}/role`).
- On 422 conflict → `Alert severity="error"` above footer.
- On success → `invalidateQueries(['adminUsers'])`, `onClose()`.

---

## Dependent Tasks

- **task_001_fe_login_session_guards.md** (US_024) — `<RoleGuard roles={['admin']}>` must exist.
- **task_002_be_user_lifecycle_api.md** (US_025) — all backend endpoints must be available.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/admin/UserManagementPage.tsx` | SCR-021: searchable table + row actions + confirmation dialog |
| CREATE | `client/src/components/admin/CreateEditUserModal.tsx` | SCR-022: create/edit modal with role select + validation |
| CREATE | `client/src/components/admin/RoleAssignmentModal.tsx` | SCR-023: role dropdown + permissions bitmask checkboxes |
| CREATE | `client/src/hooks/admin/useAdminUsers.ts` | `GET /api/v1/admin/users`; 30s staleTime |
| CREATE | `client/src/hooks/admin/useCreateUser.ts` | `POST /api/v1/admin/users` mutation |
| CREATE | `client/src/hooks/admin/useUpdateUser.ts` | `PUT /api/v1/admin/users/{id}` mutation |
| CREATE | `client/src/hooks/admin/useAssignRole.ts` | `PATCH /api/v1/admin/users/{id}/role` mutation |
| CREATE | `client/src/hooks/admin/useToggleUserStatus.ts` | `PATCH /api/v1/admin/users/{id}/disable` or `/enable` mutation |
| MODIFY | `client/src/App.tsx` | Register `/admin/users` route inside `<RoleGuard roles={['admin']}>` |

---

## Current Project State

```
client/src/
  App.tsx                                               ← MODIFY — add /admin/users route under admin RoleGuard
  pages/
    admin/
      UserManagementPage.tsx                            ← THIS TASK (create) [SCR-021]
  components/
    admin/
      CreateEditUserModal.tsx                           ← THIS TASK (create) [SCR-022]
      RoleAssignmentModal.tsx                           ← THIS TASK (create) [SCR-023]
  hooks/
    admin/
      useAdminUsers.ts                                  ← THIS TASK (create)
      useCreateUser.ts                                  ← THIS TASK (create)
      useUpdateUser.ts                                  ← THIS TASK (create)
      useAssignRole.ts                                  ← THIS TASK (create)
      useToggleUserStatus.ts                            ← THIS TASK (create)
```

---

## Implementation Plan

1. **Permission constants** (shared between FE and BE):
   ```typescript
   // client/src/lib/permissions.ts
   export const Permissions = {
     ViewPatientCharts:   1 << 0,  // 1
     VerifyClinicalData:  1 << 1,  // 2
     ManageAppointments:  1 << 2,  // 4
     UploadDocuments:     1 << 3,  // 8
     ViewMetrics:         1 << 4,  // 16
   } as const;
   export type PermissionKey = keyof typeof Permissions;
   ```

2. **`useAdminUsers.ts`**:
   ```typescript
   export function useAdminUsers() {
     return useQuery({
       queryKey: ['adminUsers'],
       queryFn: () => api.get<AdminUserDto[]>('/api/v1/admin/users').then(r => r.data),
       staleTime: 30_000,
     });
   }
   ```

3. **`UserManagementPage.tsx`** key patterns:
   ```typescript
   const [search, setSearch] = useState('');
   const debouncedSearch = useDebounce(search, 300);
   const { data: users = [], isLoading, isError } = useAdminUsers();
   const { user: currentUser } = useAuthStore();

   const filtered = users.filter(u =>
     u.name.toLowerCase().includes(debouncedSearch.toLowerCase()) ||
     u.email.toLowerCase().includes(debouncedSearch.toLowerCase()) ||
     u.role.toLowerCase().includes(debouncedSearch.toLowerCase())
   );
   ```
   - Role `Chip`: `color="primary"` for staff, `color="success"` for admin, default for patient.
   - Disable button: `disabled={u.id === currentUser?.id}` with `Tooltip` "You cannot disable your own account".

4. **`CreateEditUserModal.tsx`** key patterns:
   ```typescript
   const isEditMode = !!user;
   const { mutate: createUser, isPending: creating } = useCreateUser();
   const { mutate: updateUser, isPending: updating } = useUpdateUser(user?.id);
   const [conflictError, setConflictError] = useState<string | null>(null);

   const handleSubmit = () => {
     // validate...
     const mutation = isEditMode ? updateUser : createUser;
     mutation(formData, {
       onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['adminUsers'] }); onClose(); },
       onError: (err) => {
         if (err.response?.status === 422) setConflictError(err.response.data.message);
       },
     });
   };
   ```

5. **`RoleAssignmentModal.tsx`** permissions checkboxes:
   ```typescript
   const permissionItems: Array<{ key: PermissionKey; label: string; tooltip: string }> = [
     { key: 'ViewPatientCharts',  label: 'View patient charts',  tooltip: 'Allows reading patient clinical data' },
     { key: 'VerifyClinicalData', label: 'Verify clinical data', tooltip: 'Allows confirming extracted facts and codes' },
     { key: 'ManageAppointments', label: 'Manage appointments',  tooltip: 'Allows creating and modifying appointments' },
     { key: 'UploadDocuments',    label: 'Upload documents',     tooltip: 'Allows uploading patient documents' },
     { key: 'ViewMetrics',        label: 'View metrics',         tooltip: 'Allows accessing operational dashboards' },
   ];

   const isChecked = (key: PermissionKey) => (permissionsBitfield & Permissions[key]) !== 0;
   const togglePermission = (key: PermissionKey) => {
     setPermissionsBitfield(prev =>
       isChecked(key) ? prev & ~Permissions[key] : prev | Permissions[key]);
   };
   ```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/lib/permissions.ts` | Permission bitmask constants (shared across FE) |
| CREATE | `client/src/pages/admin/UserManagementPage.tsx` | SCR-021: table + search + Loading/Empty/Error states + row actions + confirm dialog |
| CREATE | `client/src/components/admin/CreateEditUserModal.tsx` | SCR-022: create/edit Dialog; 422 conflict `Alert`; blur validation |
| CREATE | `client/src/components/admin/RoleAssignmentModal.tsx` | SCR-023: role `Select` + bitmask `Checkbox` group with `Tooltip`; 422 conflict `Alert` |
| CREATE | `client/src/hooks/admin/useAdminUsers.ts` | `GET /api/v1/admin/users`; 30s staleTime |
| CREATE | `client/src/hooks/admin/useCreateUser.ts` | `POST /api/v1/admin/users` |
| CREATE | `client/src/hooks/admin/useUpdateUser.ts` | `PUT /api/v1/admin/users/{id}` |
| CREATE | `client/src/hooks/admin/useAssignRole.ts` | `PATCH /api/v1/admin/users/{id}/role` |
| CREATE | `client/src/hooks/admin/useToggleUserStatus.ts` | `PATCH /api/v1/admin/users/{id}/disable` or `/enable` |
| MODIFY | `client/src/App.tsx` | Add `/admin/users` route under `<RoleGuard roles={['admin']}>` |

---

## External References

- [MUI 5 — `Table` with sortable columns](https://mui.com/material-ui/react-table/)
- [MUI 5 — `Dialog` (modal) pattern](https://mui.com/material-ui/react-dialog/)
- [MUI 5 — `Chip` with `color` prop for role badges](https://mui.com/material-ui/react-chip/)
- [MUI 5 — `FormGroup` + `Checkbox` + `Tooltip` for permissions](https://mui.com/material-ui/react-checkbox/#form-group)
- [MUI 5 — `Skeleton` for loading rows](https://mui.com/material-ui/react-skeleton/)
- [React Query 4 — `invalidateQueries` after mutation](https://tanstack.com/query/v4/docs/react/reference/QueryClient#queryclientinvalidatequeries)
- [FR-015 — admin create/update/disable/role-assign accounts](../.propel/context/docs/spec.md)
- [UC-006 — Admin manages users and access (incl. extension 2a conflict)](../.propel/context/docs/spec.md)
- [figma_spec.md#SCR-021 — User Management table spec](../.propel/context/docs/figma_spec.md)
- [figma_spec.md#SCR-022 — Create/Edit User modal spec](../.propel/context/docs/figma_spec.md)
- [figma_spec.md#SCR-023 — Role Assignment modal spec](../.propel/context/docs/figma_spec.md)

---

## Build Commands

```bash
cd client
npm install
npm run build
npm run type-check
```

---

## Implementation Validation Strategy

- [ ] Unit test: `UserManagementPage` — Disable button for current admin user is `disabled` and shows `Tooltip` "You cannot disable your own account"
- [ ] Unit test: `CreateEditUserModal` — 422 response from mutation → `Alert` shows `error.response.data.message`; form stays open
- [ ] Unit test: `RoleAssignmentModal` — toggling "View patient charts" checkbox flips bit 0 of `permissionsBitfield` correctly
- [ ] **[UI Tasks - MANDATORY]** Visual comparison against `wireframe-SCR-021-user-management.html` at 375px, 768px, 1440px; role badge colours; table hover state; Skeleton loading rows
- [ ] **[UI Tasks - MANDATORY]** Visual comparison against `wireframe-SCR-022-create-edit-user.html`; modal max-width 500px; footer button alignment; blur validation
- [ ] **[UI Tasks - MANDATORY]** Visual comparison against `wireframe-SCR-023-role-assignment.html`; permission checkboxes with `#FAFAFA` background; Tooltip on hover
- [ ] **[UI Tasks - MANDATORY]** Run `/analyze-ux` to validate wireframe alignment for all three screens
- [ ] Search debounce 300ms — rapid typing does not fire multiple re-renders/API calls
- [ ] Disabled user row: "Disable" replaced with "Enable"; status cell shows "Disabled" in muted colour

---

## Implementation Checklist

- [ ] Create `client/src/lib/permissions.ts` with 5 bitmask constants (ViewPatientCharts=1, VerifyClinicalData=2, ManageAppointments=4, UploadDocuments=8, ViewMetrics=16)
- [ ] Create `useAdminUsers.ts` (30s staleTime) and four mutation hooks (`useCreateUser`, `useUpdateUser`, `useAssignRole`, `useToggleUserStatus`) each calling `invalidateQueries(['adminUsers'])` on success
- [ ] Create `UserManagementPage.tsx` (SCR-021): `<RoleGuard>` enforced at route level; debounced search (300ms); Loading/Empty/Error states; role `Chip` colour-coded (primary=staff, success=admin); Disable button disabled + `Tooltip` when `u.id === currentUser.id`; confirmation `Dialog` before disable/enable
- [ ] Create `CreateEditUserModal.tsx` (SCR-022): MUI `Dialog` max-width 500px; Full name/Email/Role/Department fields; blur validation (UXR-502); 422 conflict → `Alert severity="error"` with API message; create vs edit mode title/button label
- [ ] Create `RoleAssignmentModal.tsx` (SCR-023): role `Select` pre-populated; bitmask `FormGroup` using `permissionItems` array with `Tooltip` on each; `togglePermission` flips correct bit; 422 conflict `Alert`
- [ ] Register `/admin/users` route in `App.tsx` under admin `<RoleGuard>`
- [ ] **[UI Tasks - MANDATORY]** Reference all three wireframe files from Design References during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate all three screens match wireframes before marking complete
