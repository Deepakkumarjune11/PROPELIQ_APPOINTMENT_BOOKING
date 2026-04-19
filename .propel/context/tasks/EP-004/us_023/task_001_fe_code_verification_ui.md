# Task - task_001_fe_code_verification_ui

## Requirement Reference

- **User Story**: US_023 — ICD-10/CPT Code Suggestion & Verification
- **Story Location**: `.propel/context/tasks/EP-004/us_023/us_023.md`
- **Acceptance Criteria**:
  - AC-1: ICD-10 and CPT code suggestions are displayed with evidence breadcrumb chips linking to extracted facts per FR-014 and AIR-005.
  - AC-2: Clicking an evidence breadcrumb chip navigates to the source fact and opens the `SourceCitationDrawer` per FR-014.
  - AC-3: Clicking Accept sets `staff_reviewed = true`, records `review_outcome = 'accepted'`, and writes an audit entry per FR-014.
  - AC-4: Clicking Reject with justification marks the code rejected with justification and updates AI-human agreement metrics per FR-014.
  - AC-5: After all codes are reviewed and conflicts resolved, clicking "Finalize Patient Summary" sets `verification_status = 'verified'`, increments version, and navigates to SCR-020 with chart prep completion time per UC-005.
- **Edge Cases**:
  - Staff clicks Reject without entering justification → inline validation error "Justification is required to reject a code" blocks submission (UC-005 edge case).
  - All codes rejected → "Reject all" workflow shows a batch justification field; AI-human agreement degradation noted in AuditLog payload.
  - No code suggestions generated (no `CodeSuggestion` rows for patient) → SCR-019 shows empty state "No code suggestions generated"; "Finalize Patient Summary" still available.
  - "Finalize" clicked when pending codes remain → tooltip "Review all codes before finalizing"; button stays disabled.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-019-code-verification.html`, `.propel/context/wireframes/Hi-Fi/wireframe-SCR-020-verification-complete.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-019`, `.propel/context/docs/figma_spec.md#SCR-020` |
| **UXR Requirements** | UXR-002, UXR-003, UXR-303, UXR-403 |
| **Design Tokens** | `designsystem.md#healthcare-specific-colors` (ICD-10 badge: `#673AB7` diagnoses, CPT badge: `#009688` procedures), `designsystem.md#semantic-colors` (`success.500 = #4CAF50`) |

### CRITICAL: Wireframe Implementation Requirement

**Wireframe Status = AVAILABLE:**
- **MUST** open and reference both wireframe files during implementation
- **SCR-019 key details** (`wireframe-SCR-019-code-verification.html`):
  - `.code-grid`: `grid-template-columns: repeat(auto-fill, minmax(400px, 1fr))` — 1 column at ≤ 600px
  - `.code-card`: `border: 1px solid #E0E0E0; border-radius: 8px; padding: 24px; box-shadow: elevation-1`
  - `.code-badge`: ICD-10 → `background: #673AB7` (diagnoses colour); CPT → `background: #009688` (procedures colour)
  - `.evidence-breadcrumbs`: wrapped flexbox, gap 8px; `.breadcrumb-chip` hover `background: #2196F3; color: #FFF`
  - Three action buttons per card: Accept (green `#4CAF50`), Reject (outlined neutral), Modify (outlined primary)
  - Fixed bottom action bar with "Finalize Patient Summary" primary + "Back to review" secondary
  - States: Default, Loading (Skeleton), Empty, Error
- **SCR-020 key details** (`wireframe-SCR-020-verification-complete.html`):
  - Centred `completion-card` max-width 600px
  - Green circular check icon (80px diameter, `#4CAF50` background)
  - `success-alert`: `background: #E8F5E9; border-left: 4px solid #4CAF50`
  - `stats` grid (2 columns): "Time to verify" + "AI confidence avg"
  - Single "Next patient" button → navigate to SCR-016
- **MUST** validate at 375px, 768px, 1440px; code-grid collapses to 1-col at ≤ 600px
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

> All code and libraries MUST be compatible with versions above. ICD-10 badge colour = `FACT_CATEGORY_COLORS.Diagnoses` (`#673AB7`); CPT badge colour = `FACT_CATEGORY_COLORS.Procedures` (`#009688`) — both already exported from `healthcare-theme.ts` (US_021/task_001). `SourceCitationDrawer` component already built in US_021/task_001; reuse directly.

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

Build two staff-facing screens inside `<StaffRouteGuard>`:

**SCR-019 — Code Verification** (`/staff/patients/:patientId/code-verification`):
- Displays all `CodeSuggestion` rows for the patient grouped in a responsive card grid.
- Each `CodeSuggestionCard` shows: code value + type badge (ICD-10 purple / CPT teal), description, evidence breadcrumb chips (each chip = one linked `ExtractedFact`), three action buttons (Accept / Reject / Modify).
- Accept → fires `useConfirmCode` mutation, card transitions to accepted state (green check, disabled buttons).
- Reject → expands inline `TextField` for justification (required); fires `useConfirmCode` mutation with `reviewOutcome = 'rejected'`.
- Evidence breadcrumb chip click → opens `SourceCitationDrawer` for the linked fact (reuses `useFactSource` from US_021).
- Fixed bottom bar: "Finalize Patient Summary" (disabled + Tooltip when any code still `pending`) + "Back to review" button.
- Progress indicator: `{reviewed} of {total} codes reviewed`.

**SCR-020 — Verification Complete** (`/staff/patients/:patientId/verification-complete`):
- Navigated to after successful `PATCH /360-view/{id}/status = 'verified'`.
- Centred card: success icon, patient name/MRN, chart summary (`factCount`, `codesConfirmed`, `conflictsResolved`), timing stats (time to verify from `view360.LastUpdated` to now, AI confidence average).
- "Next patient" button → navigate to `/staff/patients` (SCR-016).

---

## Dependent Tasks

- **task_001_fe_patient_chart_review_360_view.md** (US_021) — `SourceCitationDrawer`, `useFactSource`, `usePatientView360`, `FACT_CATEGORY_COLORS` all available.
- **task_002_be_code_suggestion_generation_api.md** (US_023) — `GET /api/v1/patients/{patientId}/code-suggestions` and `POST /api/v1/code-suggestions/confirm` endpoints must exist.
- **task_003_db_code_suggestion_schema.md** (US_023) — `CodeSuggestion` table must exist.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/staff/CodeVerificationPage.tsx` | SCR-019: responsive code card grid + fixed bottom bar + finalize gate |
| CREATE | `client/src/pages/staff/VerificationCompletePage.tsx` | SCR-020: centred completion card + stats + "Next patient" |
| CREATE | `client/src/components/clinical/CodeSuggestionCard.tsx` | Code card: type badge, description, evidence chips, Accept/Reject/Modify actions |
| CREATE | `client/src/hooks/useCodeSuggestions.ts` | React Query: `GET /api/v1/patients/{patientId}/code-suggestions` |
| CREATE | `client/src/hooks/useConfirmCode.ts` | React Query mutation: `POST /api/v1/code-suggestions/confirm` |
| MODIFY | `client/src/App.tsx` | Register `/staff/patients/:patientId/code-verification` + `/staff/patients/:patientId/verification-complete` inside `<StaffRouteGuard>` |

---

## Implementation Plan

1. **`useCodeSuggestions.ts`** — fetch all code suggestions for patient:
   ```typescript
   export function useCodeSuggestions(patientId: string) {
     return useQuery({
       queryKey: ['codeSuggestions', patientId],
       queryFn: () =>
         api.get<CodeSuggestionDto[]>(`/api/v1/patients/${patientId}/code-suggestions`).then(r => r.data),
       staleTime: 60_000,
     });
   }
   ```

2. **`useConfirmCode.ts`** — accept/reject mutation with optimistic update:
   ```typescript
   export function useConfirmCode(patientId: string) {
     const queryClient = useQueryClient();
     return useMutation({
       mutationFn: (payload: ConfirmCodePayload) =>
         api.post('/api/v1/code-suggestions/confirm', payload).then(r => r.data),
       onSuccess: (_, variables) => {
         queryClient.setQueryData<CodeSuggestionDto[]>(['codeSuggestions', patientId], (old = []) =>
           old.map(c => c.id === variables.codeId
             ? { ...c, staffReviewed: true, reviewOutcome: variables.reviewOutcome }
             : c));
       },
     });
   }
   ```

3. **`CodeSuggestionCard.tsx`** — code card with evidence breadcrumbs:
   ```typescript
   const CodeSuggestionCard: React.FC<{
     code: CodeSuggestionDto; patientId: string;
     onEvidenceClick: (factId: string) => void;
   }> = ({ code, patientId, onEvidenceClick }) => {
     const { mutate, isPending } = useConfirmCode(patientId);
     const [showRejectField, setShowRejectField] = useState(false);
     const [justification, setJustification] = useState('');
     const [justErr, setJustErr] = useState(false);

     const isIcd10 = code.codeType === 'ICD-10';
     const badgeColor = isIcd10 ? FACT_CATEGORY_COLORS.Diagnoses : FACT_CATEGORY_COLORS.Procedures;

     const handleReject = () => {
       if (!justification.trim()) { setJustErr(true); return; }
       mutate({ codeId: code.id, reviewOutcome: 'rejected', justification: justification.trim() });
     };

     if (code.staffReviewed) {
       return (
         <Card sx={{ border: 1, borderColor: code.reviewOutcome === 'accepted' ? 'success.main' : 'error.main', borderRadius: 2, opacity: 0.8 }}>
           <CardContent>
             <Stack direction="row" justifyContent="space-between">
               <Typography variant="subtitle1" fontWeight={500}>{code.code} — {code.description}</Typography>
               <Chip label={code.reviewOutcome === 'accepted' ? 'Accepted' : 'Rejected'}
                 color={code.reviewOutcome === 'accepted' ? 'success' : 'error'} size="small" />
             </Stack>
           </CardContent>
         </Card>
       );
     }

     return (
       <Card sx={{ border: 1, borderColor: 'divider', borderRadius: 2, boxShadow: 1 }}>
         <CardContent>
           <Stack direction="row" justifyContent="space-between" alignItems="flex-start" mb={2}>
             <Box>
               <Typography variant="h6">{code.code} — {code.description}</Typography>
               <Chip label={code.codeType} size="small" sx={{ mt: 0.5, bgcolor: badgeColor, color: '#FFF', fontWeight: 500 }} />
             </Box>
           </Stack>
           <Typography variant="caption" color="text.secondary" fontWeight={500} display="block" mb={0.5}>
             Evidence trail:
           </Typography>
           <Box display="flex" flexWrap="wrap" gap={1} mb={2}>
             {code.evidenceFacts.map(fact => (
               <Chip key={fact.factId} label={fact.factSummary} size="small"
                 onClick={() => onEvidenceClick(fact.factId)}
                 sx={{ cursor: 'pointer', '&:hover': { bgcolor: 'primary.main', color: '#FFF' } }}
               />
             ))}
           </Box>
           {showRejectField && (
             <TextField multiline minRows={2} fullWidth placeholder="Justification for rejection (required)"
               value={justification} onChange={e => { setJustification(e.target.value); setJustErr(false); }}
               error={justErr} helperText={justErr ? 'Justification is required to reject a code' : ''}
               sx={{ mb: 2 }} />
           )}
           <Stack direction="row" gap={1}>
             <Button variant="contained" color="success" startIcon={<CheckIcon />}
               disabled={isPending} onClick={() => mutate({ codeId: code.id, reviewOutcome: 'accepted' })}
               sx={{ minHeight: 44 }}>Accept</Button>
             <Button variant="outlined" startIcon={<CloseIcon />}
               disabled={isPending} onClick={() => showRejectField ? handleReject() : setShowRejectField(true)}
               sx={{ minHeight: 44 }}>{showRejectField ? 'Confirm reject' : 'Reject'}</Button>
             <Button variant="outlined" color="primary" startIcon={<EditIcon />}
               disabled={isPending} sx={{ minHeight: 44 }}>Modify</Button>
           </Stack>
         </CardContent>
       </Card>
     );
   };
   ```

4. **`CodeVerificationPage.tsx`** — page with fixed bottom bar:
   ```typescript
   const CodeVerificationPage: React.FC = () => {
     const { patientId } = useParams<{ patientId: string }>();
     const { data: codes = [], isLoading, isError } = useCodeSuggestions(patientId!);
     const { data: view360 } = usePatientView360(patientId!);
     const navigate = useNavigate();
     const [citationFactId, setCitationFactId] = useState<string | null>(null);
     const { data: citation, isFetching, refetch } = useFactSource(citationFactId);

     const pendingCount = codes.filter(c => !c.staffReviewed).length;
     const allReviewed = pendingCount === 0;

     const handleFinalize = async () => {
       await api.patch(`/api/v1/360-view/${view360!.view360Id}/status`, { status: 'verified' });
       navigate(`/staff/patients/${patientId}/verification-complete`,
         { state: { factCount: view360?.factCount, codesConfirmed: codes.filter(c => c.reviewOutcome === 'accepted').length } });
     };

     return (
       <Box sx={{ minHeight: '100vh', pb: 10 }}>
         {/* header + breadcrumbs omitted for brevity */}
         <Box maxWidth={1200} mx="auto" p={3}>
           {isLoading && [0,1,2].map(i => <Skeleton key={i} variant="rectangular" height={200} sx={{ mb: 3, borderRadius: 2 }} />)}
           {isError && <Alert severity="error">Failed to load code suggestions</Alert>}
           {!isLoading && !isError && codes.length === 0 && (
             <Alert severity="info">No code suggestions generated for this patient.</Alert>
           )}
           <Box display="grid" gridTemplateColumns={{ xs: '1fr', sm: 'repeat(auto-fill, minmax(400px, 1fr))' }} gap={3}>
             {codes.map(code => (
               <CodeSuggestionCard key={code.id} code={code} patientId={patientId!}
                 onEvidenceClick={factId => { setCitationFactId(factId); refetch(); }} />
             ))}
           </Box>
         </Box>
         {/* Fixed bottom action bar */}
         <Box position="fixed" bottom={0} left={0} right={0} bgcolor="#FFF" boxShadow="0 -1px 3px rgba(0,0,0,0.12)" p={2} display="flex" justifyContent="center" gap={2}>
           <Button variant="outlined" onClick={() => navigate(`/staff/patients/${patientId}/360-view`)} sx={{ minHeight: 44, px: 4 }}>Back to review</Button>
           <Tooltip title={!allReviewed ? 'Review all codes before finalizing' : ''}>
             <span>
               <Button variant="contained" disabled={!allReviewed || !view360} onClick={handleFinalize} sx={{ minHeight: 44, px: 4 }}>
                 Finalize Patient Summary
               </Button>
             </span>
           </Tooltip>
         </Box>
         <SourceCitationDrawer open={!!citationFactId} onClose={() => setCitationFactId(null)} citation={citation} loading={isFetching} />
       </Box>
     );
   };
   ```

5. **`VerificationCompletePage.tsx`** — SCR-020 completion card:
   ```typescript
   const VerificationCompletePage: React.FC = () => {
     const { patientId } = useParams<{ patientId: string }>();
     const { state } = useLocation();
     const navigate = useNavigate();
     const completionTime = useRef(Date.now());
     const verifyDurationMs = completionTime.current - (state?.startedAt ?? completionTime.current);
     const durationFormatted = `${Math.floor(verifyDurationMs / 60000)}:${String(Math.floor((verifyDurationMs % 60000) / 1000)).padStart(2, '0')}`;

     return (
       <Box display="flex" alignItems="center" justifyContent="center" minHeight="100vh" p={4} bgcolor="grey.50">
         <Card sx={{ maxWidth: 600, width: '100%', p: 4, textAlign: 'center', borderRadius: 2, boxShadow: 1 }}>
           <Box width={80} height={80} borderRadius="50%" bgcolor="success.main" color="white"
             display="flex" alignItems="center" justifyContent="center" mx="auto" mb={3}>
             <CheckCircleIcon sx={{ fontSize: 48 }} />
           </Box>
           <Typography variant="h4" fontWeight={500} mb={1}>Verification complete</Typography>
           <Typography color="text.secondary" mb={3}>Patient chart verified and ready for appointment</Typography>
           <Alert severity="success" icon={false} sx={{ textAlign: 'left', mb: 3 }}>
             <Typography variant="subtitle2">Chart Summary</Typography>
             <Typography variant="body2">{state?.patientName} • {state?.factCount ?? 0} facts extracted • {state?.codesConfirmed ?? 0} codes confirmed</Typography>
           </Alert>
           <Box display="grid" gridTemplateColumns="1fr 1fr" gap={2} mb={3}>
             <Box bgcolor="grey.50" p={2} borderRadius={1}>
               <Typography variant="h5" fontWeight={500}>{durationFormatted}</Typography>
               <Typography variant="caption" color="text.secondary">Time to verify</Typography>
             </Box>
             <Box bgcolor="grey.50" p={2} borderRadius={1}>
               <Typography variant="h5" fontWeight={500}>{state?.avgConfidence ?? '—'}%</Typography>
               <Typography variant="caption" color="text.secondary">AI confidence avg</Typography>
             </Box>
           </Box>
           <Button variant="contained" fullWidth sx={{ minHeight: 44 }}
             startIcon={<NavigateNextIcon />}
             onClick={() => navigate('/staff/patients')}>
             Next patient
           </Button>
         </Card>
       </Box>
     );
   };
   ```

---

## Current Project State

```
client/src/
  App.tsx                                               ← add SCR-019 + SCR-020 routes
  pages/
    staff/
      PatientChartReviewPage.tsx                        ← us_021/task_001
      PatientView360Page.tsx                            ← us_021/task_001
      ConflictResolutionPage.tsx                        ← us_022/task_001
      CodeVerificationPage.tsx                          ← THIS TASK (create) [SCR-019]
      VerificationCompletePage.tsx                      ← THIS TASK (create) [SCR-020]
  components/
    clinical/
      FactCard.tsx                                      ← us_021/task_001
      SourceCitationDrawer.tsx                          ← us_021/task_001 (reuse)
      ConflictCard.tsx                                  ← us_022/task_001
      CodeSuggestionCard.tsx                            ← THIS TASK (create)
  hooks/
    usePatientView360.ts                                ← us_021/task_001
    useFactSource.ts                                    ← us_021/task_001 (reuse)
    useCodeSuggestions.ts                               ← THIS TASK (create)
    useConfirmCode.ts                                   ← THIS TASK (create)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/staff/CodeVerificationPage.tsx` | SCR-019: code grid + evidence chips + Accept/Reject/Modify actions + fixed bottom bar + finalize gate |
| CREATE | `client/src/pages/staff/VerificationCompletePage.tsx` | SCR-020: centred completion card with stats (time to verify, AI confidence avg) + "Next patient" CTA |
| CREATE | `client/src/components/clinical/CodeSuggestionCard.tsx` | ICD-10/CPT badge; evidence breadcrumb chips; Accept/Reject (with inline justification) / Modify actions |
| CREATE | `client/src/hooks/useCodeSuggestions.ts` | React Query `GET /api/v1/patients/{patientId}/code-suggestions`; 60s staleTime |
| CREATE | `client/src/hooks/useConfirmCode.ts` | Mutation `POST /api/v1/code-suggestions/confirm`; `setQueryData` optimistic outcome update |
| MODIFY | `client/src/App.tsx` | Register `/staff/patients/:patientId/code-verification` + `/staff/patients/:patientId/verification-complete` inside `<StaffRouteGuard>` |

---

## External References

- [MUI 5 — `Chip` with `onClick` for clickable evidence breadcrumbs](https://mui.com/material-ui/react-chip/#clickable)
- [MUI 5 — Fixed position `Box` bottom action bar pattern](https://mui.com/system/positioning/)
- [MUI 5 — CSS Grid via `sx.gridTemplateColumns` responsive object syntax](https://mui.com/system/grid/)
- [MUI 5 — `Tooltip` wrapping `disabled` `Button` via `<span>`](https://mui.com/material-ui/react-tooltip/#disabled-elements)
- [React Router 6 — `useLocation` state for navigation data passing](https://reactrouter.com/en/main/hooks/use-location)
- [React Query 4 — `setQueryData` optimistic update after mutation](https://tanstack.com/query/v4/docs/react/reference/QueryClient#queryclientsetquerydata)
- [designsystem.md#clinical — `Diagnoses #673AB7` (ICD-10), `Procedures #009688` (CPT)](../.propel/context/docs/designsystem.md)
- [figma_spec.md#SCR-019 — Code Verification screen spec](../.propel/context/docs/figma_spec.md)
- [figma_spec.md#SCR-020 — Verification Complete screen spec](../.propel/context/docs/figma_spec.md)
- [FR-014 — ICD-10/CPT code suggestion workflow with mandatory human confirmation](../.propel/context/docs/spec.md)
- [AIR-005 — code suggestions with supporting evidence from extracted facts](../.propel/context/docs/design.md#AIR-005)
- [UXR-003 — inline guidance; tooltips for complex verification workflows](../.propel/context/docs/figma_spec.md)

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

- [ ] Unit test: `CodeSuggestionCard` with empty justification + Reject click → `justErr = true`, mutation NOT called
- [ ] Unit test: `useConfirmCode` `onSuccess` — optimistic `setQueryData` updates `staffReviewed = true` and `reviewOutcome` in cache without full refetch
- [ ] **[UI Tasks]** Visual comparison against `wireframe-SCR-019-code-verification.html` at 375px, 768px, 1440px; code-grid collapses to 1-col at ≤ 600px; breadcrumb chip hover state; fixed bottom bar visible above content
- [ ] **[UI Tasks]** Visual comparison against `wireframe-SCR-020-verification-complete.html`; success icon circle; 2-col stats grid; "Next patient" navigates to `/staff/patients`
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment for both screens
- [ ] Finalize gate: "Finalize Patient Summary" disabled when any code `!staffReviewed`; Tooltip message correct; enabled when all reviewed
- [ ] Evidence breadcrumb chip click → `SourceCitationDrawer` opens with correct fact source highlighted

---

## Implementation Checklist

- [ ] Create `useCodeSuggestions.ts` (60s staleTime) and `useConfirmCode.ts` (optimistic `setQueryData` on success)
- [ ] Create `CodeSuggestionCard.tsx`: ICD-10 badge purple (`#673AB7`), CPT badge teal (`#009688`) from `FACT_CATEGORY_COLORS`; evidence `Chip` array; Accept (green) / Reject (expand inline `TextField` + Confirm) / Modify; accepted/rejected completed state with colour-coded `Chip`
- [ ] Create `CodeVerificationPage.tsx` (SCR-019): breadcrumb UXR-002; responsive `code-grid` (`minmax(400px, 1fr)`); Loading/Empty/Error states; fixed bottom bar with finalize gate `Tooltip`; reuse `SourceCitationDrawer` + `useFactSource` on evidence chip click; call `PATCH /360-view/{id}/status` → navigate to SCR-020
- [ ] Create `VerificationCompletePage.tsx` (SCR-020): centred card; green success circle icon; `Alert success` summary; 2-col stats (time to verify from `location.state.startedAt`, AI confidence avg); "Next patient" → `/staff/patients`
- [ ] Register both routes in `App.tsx` inside `<StaffRouteGuard>`
- [ ] **[UI Tasks - MANDATORY]** Reference `wireframe-SCR-019-*.html` and `wireframe-SCR-020-*.html` from Design References table during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate UI matches both wireframes before marking task complete
