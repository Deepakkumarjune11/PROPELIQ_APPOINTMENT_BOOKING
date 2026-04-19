# Task - TASK_003: FE Operation Progress Bar

## Requirement Reference

- **User Story:** us_038 — Loading Feedback, Toasts & Progress Indicators
- **Story Location:** `.propel/context/tasks/EP-009-II/us_038/us_038.md`
- **Acceptance Criteria:**
  - AC-4: Given a long-running operation (file upload, document processing, AI analysis), When in
    progress, Then a progress bar or percentage indicator shows real-time progress with estimated
    time remaining per UXR-403.
- **Edge Cases:**
  - Progress indicator stalls (30 seconds of no progress update) → show "This is taking longer than
    expected" with a Cancel option

> ⚠️ **UXR-403 Definition Discrepancy (flag for BRD revision):**
> US_038 AC-4 references `UXR-403` for "long-running operation progress bar with estimated time
> remaining (file upload, document processing, AI analysis)".
> However, `figma_spec.md` defines `UXR-403` as: "System MUST display progress indicators for
> multi-step workflows — Progress stepper visible on booking (3 steps), intake (dynamic),
> verification (4 steps)." These are fundamentally different: a **step-progress stepper** (booking
> workflow) vs a **real-time progress bar** (file upload / AI processing). This task implements
> the US_038 AC-4 intent (real-time progress bar + estimated time + stall detection). The
> step-progress workflow stepper (figma_spec.md UXR-403) belongs to the booking/verification
> feature stories (EP-001, EP-004). Recommend splitting UXR-403 into two UXRs in a future
> BRD revision.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | All wireframes in `.propel/context/wireframes/Hi-Fi/` (cross-cutting); note: `wireframe-status.md` confirms ProgressBar is a defined component in the wireframe component library |
| **Screen Spec** | `figma_spec.md#SCR-014` (Document Upload — file upload progress), `figma_spec.md#SCR-015` (Document List — processing status), `figma_spec.md#SCR-017` (360-Degree Patient View — AI analysis) |
| **UXR Requirements** | UXR-403 (see discrepancy note above) |
| **Design Tokens** | `designsystem.md#colors` (`--color-primary-500` progress fill, `--color-neutral-200` progress track), `designsystem.md#spacing` (8px grid for component margins), `designsystem.md#transitions` (`--transition-standard` 300ms for progress value transitions) |

> **Wireframe Implementation Requirement:**
> MUST reference the ProgressBar component specification in `wireframe-status.md` and relevant
> screen wireframes for SCR-014 (file upload cards with per-file progress bars) and SCR-015
> (document processing status). Validate component layout at 375px and 1440px.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| Frontend Framework | TypeScript | 5.x |
| UI Library | Material-UI (MUI) | 5.x |
| State Management | Zustand | 4.x |
| Build Tool | Vite | 5.x |
| Backend | .NET / ASP.NET Core | 8.0 |
| Database | PostgreSQL | 15.x |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| Mobile | N/A | N/A |

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

## Task Overview

Create two related components and one hook to cover real-time progress feedback for long-running
operations:

1. **`OperationProgressBar.tsx`** — a compound display component that wraps MUI `LinearProgress`
   with a percentage label and estimated time remaining text. Handles the stall edge case
   (30-second no-update threshold) by showing a warning message and Cancel button.

2. **`useOperationProgress.ts`** — a hook that manages progress state (`progress: 0–100`,
   `estimatedSecondsRemaining`, `isStalled`, `elapsedSeconds`) and stall detection logic.
   Feature pages (document upload SCR-014, processing status SCR-015, AI analysis SCR-017)
   consume this hook and pass the state into `OperationProgressBar`.

These components use only MUI `LinearProgress` + `Typography` + `Button` + `Box` — no additional
npm dependencies.

---

## Dependent Tasks

- `EP-009-II/us_038/task_002_fe_toast_notification_system.md` — `useToast` hook should be
  available so `OperationProgressBar` can call `showError(message)` when a Cancel is triggered
  (optional integration — Cancel just calls the `onCancel` callback prop; toast is the
  caller's responsibility).

---

## Impacted Components

| Component | Module | Action |
|-----------|--------|--------|
| `OperationProgressBar.tsx` | `client/src/components/feedback/` | CREATE |
| `useOperationProgress.ts` | `client/src/hooks/` | CREATE |

---

## Implementation Plan

1. **Define `OperationProgressBar` props interface**:
   ```ts
   interface OperationProgressBarProps {
     progress: number;                  // 0-100 (integer)
     estimatedSecondsRemaining?: number; // undefined = not yet calculated
     isStalled?: boolean;               // true = show stall warning
     label?: string;                    // operation description e.g. "Uploading document..."
     onCancel?: () => void;             // optional cancel handler; if absent, no Cancel button
   }
   ```

2. **`OperationProgressBar` render logic**:
   - Normal state (not stalled): render MUI `LinearProgress variant="determinate" value={progress}`;
     below it, a `Box` with `display: flex, justifyContent: 'space-between'`:
     - Left: `<Typography variant="caption">{label ?? 'Processing...'}</Typography>`
     - Right: `<Typography variant="caption">{progress}%{estimatedSecondsRemaining !== undefined ? ` · ~${formatTimeRemaining(estimatedSecondsRemaining)}` : ''}</Typography>`
   - Stalled state (`isStalled === true`): replace the right label with
     `<Typography variant="caption" color="warning.main">This is taking longer than expected</Typography>`;
     if `onCancel` is provided, add a `<Button variant="text" size="small" color="warning" onClick={onCancel}>Cancel</Button>`.
   - **ARIA**: `LinearProgress` already has `role="progressbar"`, `aria-valuenow={progress}`,
     `aria-valuemin={0}`, `aria-valuemax={100}` built-in (MUI). Add `aria-label={label ?? 'Operation progress'}`.
   - `progress` input guard: clamp to `Math.max(0, Math.min(100, progress))` before passing to
     MUI to prevent invalid progressbar states from upstream data races.

3. **`formatTimeRemaining` utility** (file-private to `OperationProgressBar.tsx`):
   ```ts
   function formatTimeRemaining(seconds: number): string {
     if (seconds < 60) return `${seconds}s`;
     const m = Math.floor(seconds / 60);
     const s = seconds % 60;
     return s === 0 ? `${m}m` : `${m}m ${s}s`;
   }
   ```
   This is intentionally a simple, non-localised formatter (Phase 1 scope — English only).

4. **Create `useOperationProgress.ts`** — hook that drives `OperationProgressBar`:
   - State: `progress: number` (0–100), `startedAt: number | null` (epoch ms),
     `lastProgressAt: number | null`, `isStalled: boolean`.
   - `updateProgress(newProgress: number)`: sets `progress`; updates `lastProgressAt = Date.now()`;
     sets `isStalled = false`.
   - `resetProgress()`: resets all state to initial values.
   - Stall detection: `useEffect` with `setInterval(checkStall, 5000)` (poll every 5s):
     - If `progress > 0 && progress < 100 && Date.now() - lastProgressAt > 30_000`:
       set `isStalled = true`.
     - Clears interval on unmount and when `progress >= 100`.
   - Estimated time calculation: computed value (no state):
     ```ts
     const elapsedMs = Date.now() - (startedAt ?? Date.now());
     const rate = progress / elapsedMs;           // % per ms
     const estimatedSecondsRemaining = rate > 0
       ? Math.round(((100 - progress) / rate) / 1000)
       : undefined;
     ```
     Only compute when `progress > 5` (first 5% too noisy for accurate estimation).
   - Returns: `{ progress, isStalled, estimatedSecondsRemaining, updateProgress, resetProgress }`.

5. **Stall constant**: define `STALL_THRESHOLD_MS = 30_000` as a named constant at module scope
   (no magic numbers — per `rules/code-anti-patterns.md`).

6. **Poll interval constant**: define `STALL_POLL_INTERVAL_MS = 5_000` at module scope. The poll
   does not use `setTimeout` chains (avoids drift); `setInterval` with a 5s window is precise
   enough given the 30s threshold.

7. **Integration guidance in component JSDoc** — add JSDoc to `OperationProgressBar.tsx` explaining
   the three intended usage contexts:
   - **File upload (SCR-014)**: `progress` driven by XHR `onprogress` event `(loaded/total * 100)`;
     `onCancel` calls `xhr.abort()`.
   - **Document processing (SCR-015)**: `progress` driven by polling
     `GET /api/v1/documents/{id}/status` (polling interval determined by feature task EP-003);
     `onCancel` is optional (processing is server-side and may not support cancellation).
   - **AI analysis (SCR-017)**: `progress` driven by server-sent events (SSE) or WebSocket progress
     messages from the AI gateway pipeline (EP-007); `onCancel` calls an abort endpoint.

---

## Current Project State

```
client/src/
├── components/
│   └── feedback/
│       ├── ToastProvider.tsx     (from task_002 — EXISTS when this task starts)
│       └── OperationProgressBar.tsx  ← CREATE
└── hooks/
    ├── useToast.ts               (from task_002 — EXISTS when this task starts)
    └── useOperationProgress.ts   ← CREATE
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/components/feedback/OperationProgressBar.tsx` | MUI LinearProgress + percentage label + estimated time remaining + stall warning + Cancel button; ARIA progressbar; input clamp guard |
| CREATE | `client/src/hooks/useOperationProgress.ts` | Progress state management: updateProgress, resetProgress, stall detection (30s threshold), estimated time remaining calculation; STALL_THRESHOLD_MS and STALL_POLL_INTERVAL_MS named constants |

---

## External References

- [MUI LinearProgress API — determinate variant, aria-valuenow, aria-label (MUI v5)](https://mui.com/material-ui/react-progress/#linear-determinate)
- [MUI LinearProgress — ARIA accessibility built-in (MUI v5)](https://mui.com/material-ui/react-progress/#accessibility)
- [MDN — setInterval for periodic stall detection](https://developer.mozilla.org/en-US/docs/Web/API/setInterval)
- [MDN — XHR ProgressEvent (upload.onprogress)](https://developer.mozilla.org/en-US/docs/Web/API/XMLHttpRequest/progress_event)
- [MDN — Server-Sent Events (EventSource) for streaming progress](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events/Using_server-sent_events)
- [WCAG 4.1.3 Status Messages — progressbar role for live progress feedback](https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html)
- [WCAG 2.2 — Understanding Success Criterion 3.2.4 Consistent Identification](https://www.w3.org/WAI/WCAG22/Understanding/consistent-identification.html)

---

## Build Commands

```bash
cd client
npm run dev     # Verify LinearProgress value transitions at 300ms, stall warning at 30s
npm run build   # Confirm no TypeScript errors (generic hook return types)
npm run lint    # Confirm no a11y warnings on LinearProgress element
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass
- [ ] Integration tests pass (if applicable)
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px and 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [ ] `OperationProgressBar` renders MUI `LinearProgress` with `variant="determinate"` and correct `value`
- [ ] Percentage label and estimated time remaining display correctly at `progress=0`, `progress=50`, `progress=99`, `progress=100`
- [ ] Input clamp: passing `progress=150` renders `LinearProgress value={100}` (no overflow)
- [ ] `formatTimeRemaining(45)` returns `"45s"`, `formatTimeRemaining(90)` returns `"1m 30s"`, `formatTimeRemaining(120)` returns `"2m"`
- [ ] Stall state activates after `STALL_THRESHOLD_MS` (30,000ms) of no `updateProgress` call — verified by mocking `Date.now` in test or manual dev test with reduced threshold
- [ ] Stall warning text "This is taking longer than expected" renders in `warning.main` colour
- [ ] Cancel button renders only when `onCancel` prop is provided (not rendered when absent)
- [ ] `resetProgress()` clears stall state, progress, and estimated time (component returns to initial state)
- [ ] `aria-label` on `LinearProgress` equals `label` prop or `'Operation progress'` default
- [ ] `setInterval` is cleared in `useEffect` cleanup (no memory leak — verify with React strict mode double-invocation)

---

## Implementation Checklist

- [ ] **1.** Create `client/src/components/feedback/OperationProgressBar.tsx`: define `OperationProgressBarProps` interface; render MUI `LinearProgress variant="determinate" value={clampedProgress}`; add bottom row with operation label (left) and `{progress}% · ~{estimatedTime}` (right) using `Typography variant="caption"`; add ARIA `aria-label` prop passthrough
- [ ] **2.** Add stall state branch in `OperationProgressBar`: when `isStalled=true`, replace right label with warning-coloured `Typography` "This is taking longer than expected"; conditionally render `<Button variant="text" size="small" color="warning" onClick={onCancel}>Cancel</Button>` only if `onCancel` is defined
- [ ] **3.** Add file-private `formatTimeRemaining(seconds: number): string` utility in same file as `OperationProgressBar`: `< 60s` → `${s}s`; `>= 60s` → `${m}m ${s}s` (suppress `0s` suffix)
- [ ] **4.** Add `Math.max(0, Math.min(100, progress))` clamp in `OperationProgressBar` before passing to `LinearProgress value` prop
- [ ] **5.** Create `client/src/hooks/useOperationProgress.ts`: define `STALL_THRESHOLD_MS = 30_000` and `STALL_POLL_INTERVAL_MS = 5_000` as named module-scope constants; implement `updateProgress`, `resetProgress` actions; `useEffect` with `setInterval` stall detector; computed `estimatedSecondsRemaining` (only when `progress > 5`)
- [ ] **6.** Add JSDoc to `OperationProgressBar.tsx` with three usage contexts: XHR file upload (SCR-014), document processing polling (SCR-015), AI analysis SSE/WebSocket (SCR-017); this guides EP-001/003/004 feature teams
- [ ] **7.** Manual stall test: in dev server, call `updateProgress(10)` once then wait 30s (or temporarily set `STALL_THRESHOLD_MS = 3_000` for dev testing); confirm stall warning and Cancel button appear; call `resetProgress()` and confirm they disappear
- [ ] **[UI Tasks - MANDATORY]** Reference ProgressBar component specification from `wireframe-status.md` and SCR-014/SCR-015 wireframe files during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate progress bar layout matches wireframe ProgressBar component dimensions before marking task complete
