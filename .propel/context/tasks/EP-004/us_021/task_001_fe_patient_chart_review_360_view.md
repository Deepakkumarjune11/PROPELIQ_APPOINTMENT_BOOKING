# Task - task_001_fe_patient_chart_review_360_view

## Requirement Reference

- **User Story**: US_021 — 360-Degree Patient View Assembly
- **Story Location**: `.propel/context/tasks/EP-004/us_021/us_021.md`
- **Acceptance Criteria**:
  - AC-1: When a staff member opens the patient chart, the system displays a consolidated 360-degree view with de-duplicated facts grouped by category (Vitals, Medications, History, Diagnoses, Procedures) per FR-012.
  - AC-3: When a staff member clicks on a displayed fact, the system opens a source citation drawer showing the original document text with the extracted value highlighted at character-level precision per FR-012.
  - AC-5: Vitals appear in pink (`#E91E63`), medications in deep orange (`#FF5722`), history in brown (`#795548`), diagnoses in deep purple (`#673AB7`), and procedures in teal (`#009688`) per UXR-303.
- **Edge Cases**:
  - No extracted data available for patient → SCR-017 shows "No extracted data available" empty state; no tab error thrown (UC-005 extension 1a).
  - Source citation offset out of range (malformed data) → Drawer shows full source text with no highlight; no crash (character-level slice guards with `Math.min`/`Math.max`).
  - `GET 360-view` API returns 404 (patient has no PatientView360 record yet) → SCR-017 shows pending-assembly empty state "Summary is being assembled…".
  - SCR-016 verification queue empty → Empty state "No patients pending review".

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-016-patient-chart-review.html`, `.propel/context/wireframes/Hi-Fi/wireframe-SCR-017-360-patient-view.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-016`, `.propel/context/docs/figma_spec.md#SCR-017` |
| **UXR Requirements** | UXR-002, UXR-003, UXR-303, UXR-403 |
| **Design Tokens** | `designsystem.md#healthcare-specific-colors`, `designsystem.md#semantic-colors`, `designsystem.md#typography` |

### CRITICAL: Wireframe Implementation Requirement

**Wireframe Status = AVAILABLE:**
- **MUST** open and reference both wireframe files during implementation
- **SCR-016** (`wireframe-SCR-016-patient-chart-review.html`): Table layout with Patient, MRN, Appointment, Documents, Priority, Action columns; red `badge-conflict` and orange `badge-pending`; hover row highlight
- **SCR-017** (`wireframe-SCR-017-360-patient-view.html`): Patient header with conflict badge; 5 category tabs; `fact-grid` 350px min-width cards; left border per category colour; confidence badge (blue ≥ 70%, orange < 70%); citation `IconButton`; right-slide `Drawer` with `<pre>` source text + `<mark>` highlight
- **MUST** validate implementation at 375px, 768px, 1440px breakpoints (sidebar hides at ≤ 900px per wireframe)
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

> All code and libraries MUST be compatible with versions above. Healthcare semantic colours sourced from `designsystem.md#healthcare-specific-colors`. MUI `Tabs` + `Tab` for category navigation. MUI `Drawer` (right-slide) for `SourceCitationDrawer`. No additional third-party dependencies required.

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

Build two staff-facing screens within `<StaffRouteGuard>`:

**SCR-016 — Patient Chart Review** (`/staff/patients`): Verification queue showing all patients with pending extracted data. MUI `Table` with columns Patient Name, MRN, Appointment Date/Time, Documents, Priority (Conflict/Pending badge), Review button. Row click or Review button navigates to SCR-017.

**SCR-017 — 360-Degree Patient View** (`/staff/patients/:patientId/360-view`): Consolidated fact summary for a single patient. Components: patient identity header (name, DOB, MRN, insurance), conflict badge (red, navigates to SCR-018), 5 MUI `Tabs` (Vitals / Medications / History / Diagnoses / Procedures), `FactCard` grid, `SourceCitationDrawer`.

**Key UX details from wireframes:**
- `FactCard` left border uses category colour from `FACT_CATEGORY_COLORS` (`designsystem.md#clinical`)
- Confidence badge: blue (`primary.500`) for ≥ 70%; orange (`warning.500`) for < 70%
- Citation `IconButton` (description icon) triggers `useFactSource` and opens `SourceCitationDrawer`
- `SourceCitationDrawer`: 500px wide right-slide; monospace `<pre>`; highlighted span using `sourceCharOffset` + `sourceCharLength` string slice
- All 5 states per screen: Default, Loading (Skeleton), Empty, Error (retry button), N/A

---

## Dependent Tasks

- **task_002_be_360_view_assembly_api.md** (US_021) — `GET /api/v1/patients/{patientId}/360-view` and `GET /api/v1/facts/{factId}/source` endpoints must exist.
- **task_003_db_patient_view_360_schema.md** (US_021) — `PatientView360` table must exist.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/staff/PatientChartReviewPage.tsx` | SCR-016: verification queue MUI Table; Default/Loading/Empty/Error states |
| CREATE | `client/src/pages/staff/PatientView360Page.tsx` | SCR-017: patient header + conflict badge + 5-tab fact grid |
| CREATE | `client/src/components/clinical/FactCard.tsx` | Fact card: category border, confidence badge, citation button |
| CREATE | `client/src/components/clinical/SourceCitationDrawer.tsx` | Right-slide MUI Drawer with character-level highlighted source text |
| CREATE | `client/src/hooks/usePatientView360.ts` | React Query: `GET /api/v1/patients/{id}/360-view`; 60s staleTime |
| CREATE | `client/src/hooks/useFactSource.ts` | React Query: `GET /api/v1/facts/{id}/source`; `enabled: false` |
| MODIFY | `client/src/theme/healthcare-theme.ts` | Export `FACT_CATEGORY_COLORS` map (FactType → hex from designsystem.md) |
| MODIFY | `client/src/App.tsx` | Register routes `/staff/patients` + `/staff/patients/:patientId/360-view` under `<StaffRouteGuard>` |

---

## Implementation Plan

1. **`FACT_CATEGORY_COLORS`** in `healthcare-theme.ts` (sourced from `designsystem.md#clinical`):
   ```typescript
   export const FACT_CATEGORY_COLORS: Record<string, string> = {
     Vitals:      '#E91E63',   // pink
     Medications: '#FF5722',   // deep orange
     History:     '#795548',   // brown
     Diagnoses:   '#673AB7',   // deep purple
     Procedures:  '#009688',   // teal
   };
   ```

2. **`usePatientView360.ts`** — 360-view query:
   ```typescript
   export function usePatientView360(patientId: string) {
     return useQuery({
       queryKey: ['patientView360', patientId],
       queryFn: () => api.get<PatientView360Dto>(`/api/v1/patients/${patientId}/360-view`).then(r => r.data),
       staleTime: 60_000,
       retry: 2,
     });
   }
   ```

3. **`useFactSource.ts`** — lazy source citation fetch (enabled by user click):
   ```typescript
   export function useFactSource(factId: string | null) {
     return useQuery({
       queryKey: ['factSource', factId],
       queryFn: () => api.get<SourceCitationDto>(`/api/v1/facts/${factId}/source`).then(r => r.data),
       enabled: false,   // triggered by refetch() on citation button click
       staleTime: 300_000,
     });
   }
   ```

4. **`FactCard.tsx`** — category-coloured card with confidence badge and citation trigger:
   ```typescript
   const FactCard: React.FC<{ fact: ConsolidatedFact; onCiteClick: (factId: string) => void }> = ({
     fact, onCiteClick }) => {
     const borderColor = FACT_CATEGORY_COLORS[fact.factType] ?? '#9E9E9E';
     const isHighConfidence = fact.confidenceScore >= 0.70;
     return (
       <Card sx={{ borderLeft: `4px solid ${borderColor}`, mb: 2, boxShadow: 1 }}>
         <CardContent>
           <Stack direction="row" justifyContent="space-between" alignItems="flex-start">
             <Typography variant="subtitle1" fontWeight={500}>{fact.value}</Typography>
             <Chip
               label={`${Math.round(fact.confidenceScore * 100)}%`}
               size="small"
               color={isHighConfidence ? 'primary' : 'warning'}
             />
           </Stack>
           <Stack direction="row" alignItems="center" mt={1} gap={0.5}>
             <Typography variant="caption" color="text.secondary">
               {fact.sources.length} source{fact.sources.length !== 1 ? 's' : ''}
             </Typography>
             {fact.sources[0]?.sourceCharOffset != null && (
               <IconButton
                 size="small"
                 aria-label="View source citation"
                 onClick={() => onCiteClick(fact.factId)}
               >
                 <DescriptionIcon fontSize="small" />
               </IconButton>
             )}
           </Stack>
         </CardContent>
       </Card>
     );
   };
   ```

5. **`SourceCitationDrawer.tsx`** — character-level highlighting:
   ```typescript
   const SourceCitationDrawer: React.FC<{
     open: boolean; onClose: () => void; citation: SourceCitationDto | undefined; loading: boolean;
   }> = ({ open, onClose, citation, loading }) => {
     const highlighted = citation ? buildHighlightedText(citation) : null;
     return (
       <Drawer anchor="right" open={open} onClose={onClose} PaperProps={{ sx: { width: { xs: '100%', sm: 500 } } }}>
         <Box display="flex" justifyContent="space-between" alignItems="center" p={3} borderBottom={1} borderColor="divider">
           <Typography variant="h6">Source Citation — {citation?.documentName}</Typography>
           <IconButton onClick={onClose} aria-label="Close citation drawer"><CloseIcon /></IconButton>
         </Box>
         <Box flex={1} overflow="auto" p={3}>
           {loading && <Skeleton variant="rectangular" height={200} />}
           {highlighted && (
             <Box component="pre" sx={{ fontFamily: 'monospace', fontSize: 14, whiteSpace: 'pre-wrap', bgcolor: 'grey.50', p: 2, border: 1, borderColor: 'divider', borderRadius: 1 }}
               dangerouslySetInnerHTML={{ __html: highlighted }} />
           )}
         </Box>
       </Drawer>
     );
   };

   // Character-level highlight builder (XSS-safe: escapes before inserting <mark>)
   function buildHighlightedText({ sourceText, sourceCharOffset, sourceCharLength }: SourceCitationDto): string {
     const safe = (s: string) => s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
     const start = Math.max(0, sourceCharOffset);
     const end   = Math.min(sourceText.length, sourceCharOffset + sourceCharLength);
     return safe(sourceText.slice(0, start)) +
       `<mark style="background:rgba(33,150,243,0.2);padding:2px 4px;border-radius:2px">` +
       safe(sourceText.slice(start, end)) +
       '</mark>' +
       safe(sourceText.slice(end));
   }
   ```

6. **`PatientView360Page.tsx`** — tabbed category view:
   ```typescript
   const CATEGORIES = ['Vitals', 'Medications', 'History', 'Diagnoses', 'Procedures'] as const;

   const PatientView360Page: React.FC = () => {
     const { patientId } = useParams<{ patientId: string }>();
     const { data, isLoading, isError } = usePatientView360(patientId!);
     const [activeTab, setActiveTab] = useState(0);
     const [citationFactId, setCitationFactId] = useState<string | null>(null);
     const [drawerOpen, setDrawerOpen] = useState(false);
     const { data: citation, isFetching, refetch } = useFactSource(citationFactId);

     const handleCiteClick = (factId: string) => {
       setCitationFactId(factId);
       setDrawerOpen(true);
       refetch();
     };
     // ... Skeleton/Empty/Error states + Tab rendering
   };
   ```

---

## Current Project State

```
client/src/
  App.tsx                                          ← add staff routes
  theme/
    healthcare-theme.ts                            ← add FACT_CATEGORY_COLORS
  pages/
    staff/
      PatientChartReviewPage.tsx                   ← THIS TASK (create) [SCR-016]
      PatientView360Page.tsx                       ← THIS TASK (create) [SCR-017]
  components/
    clinical/
      FactCard.tsx                                 ← THIS TASK (create)
      SourceCitationDrawer.tsx                     ← THIS TASK (create)
  hooks/
    usePatientView360.ts                           ← THIS TASK (create)
    useFactSource.ts                               ← THIS TASK (create)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/staff/PatientChartReviewPage.tsx` | SCR-016 verification queue; MUI Table; Default/Loading/Empty/Error states |
| CREATE | `client/src/pages/staff/PatientView360Page.tsx` | SCR-017 patient header + 5-tab fact grid + citation drawer trigger |
| CREATE | `client/src/components/clinical/FactCard.tsx` | Category-coloured card; confidence badge; citation button |
| CREATE | `client/src/components/clinical/SourceCitationDrawer.tsx` | Right-slide MUI Drawer; XSS-safe `buildHighlightedText`; char-level `<mark>` |
| CREATE | `client/src/hooks/usePatientView360.ts` | React Query 60s staleTime; `GET /api/v1/patients/{id}/360-view` |
| CREATE | `client/src/hooks/useFactSource.ts` | React Query `enabled:false`; `GET /api/v1/facts/{id}/source` |
| MODIFY | `client/src/theme/healthcare-theme.ts` | Export `FACT_CATEGORY_COLORS` keyed by FactType string |
| MODIFY | `client/src/App.tsx` | Add `/staff/patients` and `/staff/patients/:patientId/360-view` inside `<StaffRouteGuard>` |

---

## External References

- [MUI 5 Tabs — controlled tab panels with `value`/`onChange`](https://mui.com/material-ui/react-tabs/)
- [MUI 5 Drawer — anchor="right" persistent/temporary](https://mui.com/material-ui/react-drawer/)
- [MUI 5 Card + CardContent](https://mui.com/material-ui/react-card/)
- [React Query 4 — `enabled: false` + manual `refetch()`](https://tanstack.com/query/v4/docs/react/guides/disabling-queries)
- [React Router 6 — `useParams` for patientId](https://reactrouter.com/en/main/hooks/use-params)
- [designsystem.md#clinical — healthcare fact category color tokens](../.propel/context/docs/designsystem.md#healthcare-specific-colors)
- [figma_spec.md#SCR-016 — Patient Chart Review screen spec](../.propel/context/docs/figma_spec.md)
- [figma_spec.md#SCR-017 — 360-Degree Patient View screen spec](../.propel/context/docs/figma_spec.md)
- [UXR-303 — healthcare semantic color requirement](../.propel/context/docs/figma_spec.md)
- [FR-012 — 360-degree patient view with de-duplication and source traceability](../.propel/context/docs/spec.md)

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

- [ ] Unit tests pass for `buildHighlightedText`: safe escaping, correct `<mark>` placement, offset-out-of-range guards
- [ ] Integration test: `usePatientView360` returns grouped `ConsolidatedFact` data; tab switching renders correct category facts
- [ ] **[UI Tasks]** Visual comparison against `wireframe-SCR-016-patient-chart-review.html` at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Visual comparison against `wireframe-SCR-017-360-patient-view.html` at 375px, 768px, 1440px; category colours match `FACT_CATEGORY_COLORS`
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment for both screens
- [ ] Source citation drawer opens on citation button click; `<mark>` highlight visible for fact with known `sourceCharOffset`/`sourceCharLength`
- [ ] Empty state "No extracted data available" renders when `data.facts` is empty; pending state "Summary is being assembled…" renders on 404

---

## Implementation Checklist

- [x] Export `FACT_CATEGORY_COLORS` from `healthcare-theme.ts` mapping `Vitals`, `Medications`, `History`, `Diagnoses`, `Procedures` to respective hex tokens from `designsystem.md#clinical` (UXR-303)
- [x] Create `FactCard.tsx`: left `4px` border per `FACT_CATEGORY_COLORS`; blue badge for confidence ≥ 70%, orange for < 70%; citation `IconButton` visible only when `sourceCharOffset != null`
- [x] Create `SourceCitationDrawer.tsx`: right-slide MUI `Drawer` (500px / 100% mobile); XSS-safe `buildHighlightedText` with `Math.min/max` guard; monospace `<pre>` with `<mark>` highlight; Skeleton on `isFetching`
- [x] Create `usePatientView360.ts` + `useFactSource.ts` React Query hooks; staleTime 60s / 300s respectively; `useFactSource` uses `enabled: false`
- [x] Create `PatientChartReviewPage.tsx` (SCR-016): MUI `Table` with 6 columns; Conflict (error) and Pending (warning) `Chip`; `Skeleton` on loading; "No patients pending review" empty state
- [x] Create `PatientView360Page.tsx` (SCR-017): patient identity header; conflict badge that will navigate to SCR-018; 5 MUI `Tabs`; `FactCard` grid per active category; wire `handleCiteClick` → `useFactSource.refetch()` → open `SourceCitationDrawer`
- [x] **[UI Tasks - MANDATORY]** Reference wireframe (`wireframe-SCR-016-*.html`, `wireframe-SCR-017-*.html`) from Design References table during implementation
- [x] **[UI Tasks - MANDATORY]** Validate UI matches wireframe at 375px, 768px, 1440px before marking task complete
