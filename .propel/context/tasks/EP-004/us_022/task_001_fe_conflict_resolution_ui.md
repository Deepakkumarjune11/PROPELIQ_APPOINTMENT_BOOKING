# Task - task_001_fe_conflict_resolution_ui

## Requirement Reference

- **User Story**: US_022 — Conflict Detection & Resolution
- **Story Location**: `.propel/context/tasks/EP-004/us_022/us_022.md`
- **Acceptance Criteria**:
  - AC-1: When contradictory facts are detected, the system flags them as conflict items with a red conflict badge and count on the 360-view per FR-013.
  - AC-2: When reviewing a conflict item, staff sees all conflicting values from their respective source documents displayed side-by-side per AIR-004.
  - AC-3: When staff selects a resolution (accept source A/B or manual override), the system updates the view and records the justification per FR-013.
  - AC-4: When unresolved conflicts exist, the system blocks verification — the "Mark Verified" button is disabled with an inline tooltip explaining why per FR-013.
- **Edge Cases**:
  - Manual override selected but no justification entered → form-level validation error "Justification is required for manual override" before submission (UC-005 extension 3a).
  - Multiple conflicts (≥ 2) → each conflict rendered as its own `ConflictCard` in a scrollable list; resolving one hides it from the unresolved list without a full-page reload.
  - All conflicts resolved → `PatientView360.conflict_flags` empty → verification button enabled; brief `Alert success` "All conflicts resolved. Summary ready for verification."

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-018-conflict-resolution.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-018` |
| **UXR Requirements** | UXR-002, UXR-003 |
| **Design Tokens** | `designsystem.md#semantic-colors` (`error.main=#F44336`, `error.50=#FFEBEE`), `designsystem.md#typography` |

### CRITICAL: Wireframe Implementation Requirement

**Wireframe Status = AVAILABLE:**
- **MUST** open and reference `wireframe-SCR-018-conflict-resolution.html` during implementation
- **Key wireframe details**:
  - `conflict-card`: `background: #FFF; border-left: 4px solid #F44336; border-radius: 8px; padding: 24px; box-shadow: elevation-1`
  - `source-comparison`: 2-column CSS grid (`grid-template-columns: 1fr 1fr; gap: 16px`)
  - Each source panel: `background: #FAFAFA; padding: 16px`; Source name (500 14px), value (400 14px), confidence (400 12px `#9E9E9E`)
  - Radio options: `border: 1px solid #E0E0E0; border-radius: 4px; padding: 16px`; hover state `background: #FAFAFA`
  - `textarea` for justification: `min-height: 80px; resize: vertical; width: 100%`
  - CTA button: `min-height: 44px; width: 100%; background: #2196F3`
  - States: Default, Loading (Skeleton per conflict), N/A (no conflicts), Error, Validation (justification missing)
- **MUST** validate implementation at 375px, 768px, 1440px (2-column source grid collapses to 1-column at ≤ 600px)
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

> All code and libraries MUST be compatible with versions above. Conflict error colour `#F44336` sourced from `designsystem.md#semantic-colors error.main`. MUI `RadioGroup` + `FormControlLabel` for resolution options. MUI `TextField` multiline for justification. No additional third-party form libraries needed.

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

Build **SCR-018 — Conflict Resolution** at route `/staff/patients/:patientId/conflict-resolution`, inside `<StaffRouteGuard>`.

**Page flow (from UC-005 sequence diagram):**
- Staff arrives from SCR-017 via red conflict badge click → SCR-018 loads all unresolved conflicts for the patient.
- Each conflict rendered as a `ConflictCard` — shows fact type, side-by-side source values, radio group (Accept A / Accept B / Manual override), optional justification field.
- When manual override is selected the justification `TextField` becomes required (form validation).
- "Resolve" button per card submits `POST /api/v1/360-view/{view360Id}/resolve-conflict` → on success, card transitions to resolved state (green checkmark, collapsed) without navigating away.
- A progress indicator at the top of the page shows `{resolved} of {total} conflicts resolved`.
- Once all conflicts resolved → "Continue to code verification" button (navigates to SCR-019) becomes active. If any remain unresolved the button is disabled (`disabled + Tooltip "Resolve all conflicts before continuing"`).
- **Verification gate (AC-4)**: The "Mark Verified" button on SCR-017 reads `PatientView360.conflict_flags.length > 0` (from the `usePatientView360` hook already built in US_021). This task adds the `disabled` + `Tooltip` guard to that button — no new endpoint needed.

**States per SCR-018 (from figma_spec.md):**
- **Default**: conflict cards visible; progress bar; radio selections
- **Loading**: `Skeleton` per conflict card while `useConflicts` fetches
- **N/A (empty)**: "No conflicts detected" `Alert info`; Continue button immediately active
- **Error**: API error `Alert error` with retry button
- **Validation**: inline red helper text "Justification is required" when manual override selected with empty justification

---

## Dependent Tasks

- **task_001_fe_patient_chart_review_360_view.md** (US_021) — `PatientView360Page.tsx` hosts the conflict badge that navigates to SCR-018; `usePatientView360` hook provides `conflict_flags` count.
- **task_002_be_conflict_detection_resolution_api.md** (US_022) — `GET /api/v1/patients/{patientId}/conflicts` and `POST /api/v1/360-view/{view360Id}/resolve-conflict` must exist.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/staff/ConflictResolutionPage.tsx` | SCR-018: progress bar + list of `ConflictCard` + Continue button with verification gate |
| CREATE | `client/src/components/clinical/ConflictCard.tsx` | Single conflict item: side-by-side sources, RadioGroup, justification TextField, Resolve button |
| CREATE | `client/src/hooks/useConflicts.ts` | React Query: `GET /api/v1/patients/{patientId}/conflicts` |
| CREATE | `client/src/hooks/useResolveConflict.ts` | React Query mutation: `POST /api/v1/360-view/{view360Id}/resolve-conflict` |
| MODIFY | `client/src/pages/staff/PatientView360Page.tsx` | Add `disabled + Tooltip` guard to "Mark Verified" button when `conflictFlags.length > 0` (AC-4) |
| MODIFY | `client/src/App.tsx` | Register `/staff/patients/:patientId/conflict-resolution` inside `<StaffRouteGuard>` |

---

## Implementation Plan

1. **`useConflicts.ts`** — fetch conflict list:
   ```typescript
   export function useConflicts(patientId: string) {
     return useQuery({
       queryKey: ['conflicts', patientId],
       queryFn: () =>
         api.get<ConflictItemDto[]>(`/api/v1/patients/${patientId}/conflicts`).then(r => r.data),
       staleTime: 30_000,
     });
   }
   ```

2. **`useResolveConflict.ts`** — resolve mutation with optimistic list update:
   ```typescript
   export function useResolveConflict(patientId: string) {
     const queryClient = useQueryClient();
     return useMutation({
       mutationFn: (payload: ResolveConflictPayload) =>
         api.post(`/api/v1/360-view/${payload.view360Id}/resolve-conflict`, payload).then(r => r.data),
       onSuccess: (_, variables) => {
         // Remove resolved conflict from cache — no full refetch needed
         queryClient.setQueryData<ConflictItemDto[]>(['conflicts', patientId], (old = []) =>
           old.filter(c => c.conflictId !== variables.conflictId));
         // Invalidate 360-view so conflictFlags count updates
         queryClient.invalidateQueries({ queryKey: ['patientView360', patientId] });
       },
     });
   }
   ```

3. **`ConflictCard.tsx`** — single conflict resolution card:
   ```typescript
   type ResolutionChoice = 'sourceA' | 'sourceB' | 'manual';

   const ConflictCard: React.FC<{ conflict: ConflictItemDto; view360Id: string; patientId: string }> = ({
     conflict, view360Id, patientId }) => {
     const [choice, setChoice] = useState<ResolutionChoice | ''>('');
     const [justification, setJustification] = useState('');
     const [justErr, setJustErr] = useState(false);
     const { mutate, isPending } = useResolveConflict(patientId);

     const handleResolve = () => {
       if (choice === 'manual' && !justification.trim()) { setJustErr(true); return; }
       mutate({ view360Id, conflictId: conflict.conflictId, resolution: choice,
                manualValue: choice === 'manual' ? justification.trim() : undefined,
                justification: justification.trim() });
     };

     return (
       <Card sx={{ borderLeft: '4px solid', borderLeftColor: 'error.main', mb: 3, borderRadius: 2 }}>
         <CardContent>
           <Stack direction="row" justifyContent="space-between" mb={2}>
             <Typography variant="h6">{conflict.factType} Conflict</Typography>
             <Chip label="Unresolved" color="error" size="small" />
           </Stack>
           {/* Side-by-side source comparison (2-col grid, 1-col mobile) */}
           <Box display="grid" gridTemplateColumns={{ xs: '1fr', sm: '1fr 1fr' }} gap={2} mb={2}>
             {conflict.sources.map((src, i) => (
               <Box key={src.documentId} sx={{ bgcolor: 'grey.50', p: 2, borderRadius: 1 }}>
                 <Typography variant="subtitle2">Source {String.fromCharCode(65 + i)}: {src.documentName}</Typography>
                 <Typography variant="body2" mt={0.5}>{src.value}</Typography>
                 <Typography variant="caption" color="text.secondary">Confidence: {Math.round(src.confidenceScore * 100)}%</Typography>
               </Box>
             ))}
           </Box>
           <RadioGroup value={choice} onChange={e => setChoice(e.target.value as ResolutionChoice)}>
             {conflict.sources.map((src, i) => (
               <FormControlLabel key={src.documentId}
                 value={i === 0 ? 'sourceA' : 'sourceB'}
                 control={<Radio />}
                 label={`Accept Source ${String.fromCharCode(65 + i)} (${src.value})`}
                 sx={{ border: 1, borderColor: 'divider', borderRadius: 1, px: 2, mb: 1 }}
               />
             ))}
             <FormControlLabel value="manual" control={<Radio />}
               label="Manual override (enter correct value)"
               sx={{ border: 1, borderColor: 'divider', borderRadius: 1, px: 2, mb: 1 }}
             />
           </RadioGroup>
           <TextField
             multiline minRows={2} fullWidth
             placeholder="Justification for resolution"
             value={justification}
             onChange={e => { setJustification(e.target.value); setJustErr(false); }}
             error={justErr}
             helperText={justErr ? 'Justification is required for manual override' : ''}
             sx={{ mt: 1, mb: 2 }}
           />
           <Button variant="contained" fullWidth disabled={!choice || isPending} onClick={handleResolve}
             sx={{ minHeight: 44 }}>
             {isPending ? 'Resolving…' : 'Resolve this conflict'}
           </Button>
         </CardContent>
       </Card>
     );
   };
   ```

4. **`ConflictResolutionPage.tsx`** — page layout + progress + continue gate (AC-4):
   ```typescript
   const ConflictResolutionPage: React.FC = () => {
     const { patientId } = useParams<{ patientId: string }>();
     const { data: view360 } = usePatientView360(patientId!);
     const { data: conflicts = [], isLoading, isError } = useConflicts(patientId!);
     const view360Id = view360?.view360Id ?? '';
     const unresolvedCount = conflicts.length;
     const allResolved = unresolvedCount === 0;

     return (
       <Box maxWidth={900} mx="auto" p={3}>
         <Breadcrumbs sx={{ mb: 2 }}>
           <Link href="/staff/patients">Chart Review</Link>
           <Link href={`/staff/patients/${patientId}/360-view`}>360-View</Link>
           <Typography>Resolve Conflicts</Typography>
         </Breadcrumbs>
         <Typography variant="h4" mb={3}>Resolve conflicts</Typography>

         {/* Progress indicator */}
         {!isLoading && !isError && (
           <Alert severity={allResolved ? 'success' : 'warning'} sx={{ mb: 3 }}>
             {allResolved
               ? 'All conflicts resolved. Summary ready for verification.'
               : `${unresolvedCount} conflict${unresolvedCount !== 1 ? 's' : ''} remaining`}
           </Alert>
         )}

         {isLoading && [0, 1].map(i => <Skeleton key={i} variant="rectangular" height={220} sx={{ mb: 3, borderRadius: 2 }} />)}
         {isError && <Alert severity="error" action={<Button onClick={() => /* retry */ {}}>Retry</Button>}>Failed to load conflicts</Alert>}
         {!isLoading && !isError && conflicts.map(conflict => (
           <ConflictCard key={conflict.conflictId} conflict={conflict} view360Id={view360Id} patientId={patientId!} />
         ))}

         <Tooltip title={!allResolved ? 'Resolve all conflicts before continuing' : ''}>
           <span>
             <Button variant="contained" size="large" fullWidth disabled={!allResolved || !view360Id}
               onClick={() => navigate(`/staff/patients/${patientId}/code-verification`)}
               sx={{ minHeight: 44 }}>
               Continue to code verification
             </Button>
           </span>
         </Tooltip>
       </Box>
     );
   };
   ```

5. **Verification gate on `PatientView360Page.tsx`** (AC-4) — add `disabled + Tooltip` to "Mark Verified":
   ```typescript
   // In PatientView360Page — the data.conflictFlags comes from usePatientView360
   const hasConflicts = (data?.conflictFlags?.length ?? 0) > 0;
   // ...
   <Tooltip title={hasConflicts ? 'Resolve all conflicts before marking verified' : ''}>
     <span>
       <Button variant="contained" color="success" disabled={hasConflicts}
         onClick={handleMarkVerified}>
         Mark Verified
       </Button>
     </span>
   </Tooltip>
   ```

---

## Current Project State

```
client/src/
  App.tsx                                       ← add SCR-018 route
  pages/
    staff/
      PatientChartReviewPage.tsx                ← us_021/task_001
      PatientView360Page.tsx                    ← us_021/task_001 (extend: add verified guard)
      ConflictResolutionPage.tsx                ← THIS TASK (create) [SCR-018]
  components/
    clinical/
      FactCard.tsx                              ← us_021/task_001
      SourceCitationDrawer.tsx                  ← us_021/task_001
      ConflictCard.tsx                          ← THIS TASK (create)
  hooks/
    usePatientView360.ts                        ← us_021/task_001
    useConflicts.ts                             ← THIS TASK (create)
    useResolveConflict.ts                       ← THIS TASK (create)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/staff/ConflictResolutionPage.tsx` | SCR-018: breadcrumb + progress alert + `ConflictCard` list + Continue button with verification gate |
| CREATE | `client/src/components/clinical/ConflictCard.tsx` | Side-by-side sources; RadioGroup (Accept A / B / Manual); justification `TextField`; per-card Resolve button |
| CREATE | `client/src/hooks/useConflicts.ts` | React Query `GET /api/v1/patients/{patientId}/conflicts`; 30s staleTime |
| CREATE | `client/src/hooks/useResolveConflict.ts` | `useMutation` POST; optimistic cache removal; invalidates `patientView360` query |
| MODIFY | `client/src/pages/staff/PatientView360Page.tsx` | Add `disabled + Tooltip` guard to "Mark Verified" when `conflictFlags.length > 0` (AC-4) |
| MODIFY | `client/src/App.tsx` | Register `/staff/patients/:patientId/conflict-resolution` in `<StaffRouteGuard>` |

---

## External References

- [MUI 5 — `RadioGroup` + `FormControlLabel` for resolution options](https://mui.com/material-ui/react-radio-button/)
- [MUI 5 — `TextField` multiline with `error` + `helperText` for validation state](https://mui.com/material-ui/react-text-field/)
- [MUI 5 — `Tooltip` wrapping `disabled` `Button` (must wrap in `<span>`)](https://mui.com/material-ui/react-tooltip/#disabled-elements)
- [MUI 5 — CSS Grid `display: grid` via `sx.gridTemplateColumns` responsive breakpoints](https://mui.com/system/grid/)
- [React Query 4 — `setQueryData` for optimistic cache mutation removal](https://tanstack.com/query/v4/docs/react/reference/QueryClient#queryclientsetquerydata)
- [React Query 4 — `invalidateQueries` to refresh dependent queries after mutation](https://tanstack.com/query/v4/docs/react/guides/invalidations-from-mutations)
- [FR-013 — conflict detection, mandatory acknowledgement before verification](../.propel/context/docs/spec.md)
- [AIR-004 — detect clinically meaningful conflicts; flag for mandatory staff review](../.propel/context/docs/design.md#AIR-004)
- [UXR-003 — inline guidance; tooltips for complex workflows](../.propel/context/docs/figma_spec.md)
- [designsystem.md#semantic-colors — error.main `#F44336` for conflict badges](../.propel/context/docs/designsystem.md)

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

- [ ] Unit test: `ConflictCard` with `choice = 'manual'` and empty `justification` → `handleResolve` sets `justErr = true`, mutation NOT called
- [ ] Unit test: `useResolveConflict` `onSuccess` removes resolved conflict from `['conflicts', patientId]` cache; does NOT navigate away
- [ ] **[UI Tasks]** Visual comparison against `wireframe-SCR-018-conflict-resolution.html` at 375px, 768px, 1440px; source-comparison grid collapses to 1 column at ≤ 600px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [ ] Verification gate: "Mark Verified" button disabled when `data.conflictFlags.length > 0`; Tooltip visible on hover; enabled when `conflictFlags.length === 0`
- [ ] All-resolved state: `Alert success` "All conflicts resolved" rendered; Continue button enabled

---

## Implementation Checklist

- [ ] Create `useConflicts.ts`: `GET /api/v1/patients/{patientId}/conflicts`, 30s staleTime, retry 2
- [ ] Create `useResolveConflict.ts`: mutation `POST` → `setQueryData` removes resolved item → `invalidateQueries` patientView360 cache
- [ ] Create `ConflictCard.tsx`: 2-col source comparison (collapses to 1-col at ≤ 600px); `RadioGroup` with Accept A/B/Manual; mandatory `TextField` justification with `helperText` error when manual override + empty submission; per-card Resolve button disabled until `choice` selected
- [ ] Create `ConflictResolutionPage.tsx` (SCR-018): breadcrumb UXR-002; `Alert` progress (warning when unresolved, success when all resolved); Skeleton loading; error retry; Continue button with `Tooltip` gate disabled until `allResolved`
- [ ] MODIFY `PatientView360Page.tsx`: wrap "Mark Verified" `Button` in `Tooltip` (AC-4); `disabled` when `hasConflicts`
- [ ] Register `/staff/patients/:patientId/conflict-resolution` route in `App.tsx` inside `<StaffRouteGuard>`
- [ ] **[UI Tasks - MANDATORY]** Reference `wireframe-SCR-018-conflict-resolution.html` from Design References table during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate UI matches wireframe before marking task complete
