# Task - task_001_fe_conversational_intake_ui

## Requirement Reference

- **User Story**: US_012 — Conversational AI Intake
- **Story Location**: `.propel/context/tasks/EP-001/us_012/us_012.md`
- **Acceptance Criteria**:
  - AC-1: When the conversational intake screen loads, the AI assistant greets the patient and asks the first intake question with clear, empathetic language.
  - AC-2: When the patient sends a message, the AI responds within 3 seconds (p95 per AIR-Q02). A typing indicator is shown while waiting.
  - AC-3: When the patient clicks "Switch to manual form", the system navigates to SCR-004 with all conversationally-gathered answers pre-populated (via `intake-store.mergeAnswers`).
  - AC-4: When all required information has been gathered, the UI shows a "Submit intake" button and the patient confirms submission.
  - AC-5: When the AI service is unavailable (circuit breaker fires, BE returns `fallbackToManual: true`), the UI shows an error banner "AI assistant is temporarily unavailable" and redirects to `/appointments/intake/manual`.
- **Edge Cases**:
  - Ambiguous/off-topic patient response → AI re-prompts (handled by BE/AI layer); UI simply renders the next AI message as normal.
  - Conversation timeout (5 min inactivity) → `intake-store` retains answers; patient can resume without data loss. UI shows inactivity warning banner at 4 minutes.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-005-conversational-intake.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-005` |
| **UXR Requirements** | UXR-003 (inline guidance for complex workflows — tooltips on "What is this asking?" icon beside each AI question), UXR-403 (booking stepper step 4 active), UXR-101 (keyboard accessible), UXR-102 (44px touch targets) |
| **Design Tokens** | `designsystem.md#colors` (`primary.500: #2196F3` send button, AI Avatar badge), `designsystem.md#typography` (Roboto), `designsystem.md#spacing` |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local file exists at specified path

### CRITICAL: Wireframe Implementation Requirement

- **MUST** open `.propel/context/wireframes/Hi-Fi/wireframe-SCR-005-conversational-intake.html` and match chat layout, message card differentiation (AI vs patient), input area, and "Switch to manual" button placement.
- **MUST** implement all states: Default (greeting), Loading (AI typing indicator), Empty (pre-first-message), Error (AI unavailable — fallback banner), Validation (confirm submission at end).
- **MUST** validate implementation against wireframe at breakpoints: 375px (mobile), 768px (tablet), 1440px (desktop).
- Run `/analyze-ux` after implementation to verify pixel-perfect alignment.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| UI Components | Material-UI (MUI) | 5.x |
| State Management | React Query (TanStack Query) | 4.x |
| State Management | Zustand | 4.x |
| Routing | React Router DOM | 6.x |
| Language | TypeScript | 5.x |
| Build | Vite | 5.x |

> All code and libraries MUST be compatible with versions above.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes (consumes AI chat API) |
| **AIR Requirements** | AIR-002 (conversational intake UX), AIR-Q02 (p95 latency ≤ 3s), AIR-S04 (content safety — filtered responses from BE render as-is), AIR-O02 (circuit breaker fallback surfaced in UI as AC-5) |
| **AI Pattern** | Conversational (guided intake) — renders AI messages; no LLM calls from FE |
| **Prompt Template Path** | N/A (prompt is owned by backend/AI task) |
| **Guardrails Config** | N/A (content filtering enforced by backend before messages reach FE) |
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

Implement the **Conversational Intake** screen (SCR-005) — step 4 of the patient booking flow (FL-001), the AI-assisted alternative to the manual form (SCR-004). This screen presents a chat interface where the AI assistant guides the patient through intake questions via natural conversation.

The UI architecture is a simple **send/receive chat loop**:
1. On mount: call `POST /api/v1/patients/{patientId}/intake/chat` with an initial greeting message to receive the AI's opening message.
2. On send: append user message to local `messages` state, call the chat API, append AI response.
3. When the API returns `isComplete: true` + `structuredAnswers`: call `intake-store.mergeAnswers(structuredAnswers)` and show the Submit button.
4. When the API returns `fallbackToManual: true`: show the error banner and navigate to manual intake.

Crucially, **no LLM calls are made from the frontend**. The AI service is entirely encapsulated in the backend (task_003). The frontend is a stateless message renderer that calls a REST endpoint.

The **`intake-store`** (created in US_011/task_001) is the shared state mechanism. When mode-switch is triggered or when `isComplete` is received with structured answers, `mergeAnswers()` is called so the manual form pre-populates correctly (AC-3).

---

## Dependent Tasks

- **task_002_be_conversational_intake_api.md** (US_012) — `POST /api/v1/patients/{patientId}/intake/chat` endpoint must be available (or mocked).
- **task_001_fe_manual_intake_form.md** (US_011) — `intake-store.ts` (shared Zustand store with `mergeAnswers`) must be in place. `/appointments/intake/manual` route must exist for fallback navigation.
- **task_002_be_submit_intake_api.md** (US_011) — `POST /api/v1/patients/{patientId}/intake` endpoint used for final submission after `isComplete`.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| CREATE | `client/src/pages/intake/ConversationalIntakePage.tsx` | SCR-005 root page: chat container, message list, input area, mode-switch button |
| CREATE | `client/src/pages/intake/components/ChatMessageList.tsx` | Scrollable list of `ChatMessage` items with auto-scroll to latest |
| CREATE | `client/src/pages/intake/components/ChatMessageItem.tsx` | Single message card: AI (primary.500 Avatar + name "AI Assistant") vs Patient (secondary Avatar) |
| CREATE | `client/src/pages/intake/components/AiTypingIndicator.tsx` | Three-dot animated MUI `LinearProgress` / skeleton while awaiting AI response |
| CREATE | `client/src/pages/intake/components/ChatInputBar.tsx` | MUI TextField + Send IconButton; Enter to submit; disabled while AI is typing |
| CREATE | `client/src/pages/intake/components/IntakeFallbackBanner.tsx` | MUI Alert severity="warning" with message and "Switch to manual" CTA |
| CREATE | `client/src/api/intakeChat.ts` | API client for `POST /api/v1/patients/{patientId}/intake/chat` |
| CREATE | `client/src/hooks/useIntakeChat.ts` | React Query `useMutation` managing chat send + `isComplete` / `fallbackToManual` handling |
| MODIFY | `client/src/stores/intake-store.ts` | Confirm `mergeAnswers(answers: Record<string, string>)` action exists (should already be present from US_011/task_001) |
| MODIFY | `client/src/App.tsx` | Replace conversational intake placeholder route with `ConversationalIntakePage` component |

---

## Implementation Plan

1. **`intakeChat.ts` API client**:
   ```typescript
   export interface ChatMessage {
     role: 'user' | 'assistant';
     content: string;
     timestamp: Date;
   }

   export interface SendChatMessageRequest {
     message: string;
     conversationHistory: Array<{ role: string; content: string }>;
   }

   export interface SendChatMessageResponse {
     assistantMessage: string;
     isComplete: boolean;
     fallbackToManual: boolean;
     structuredAnswers?: Record<string, string>;  // present when isComplete = true
   }

   export async function sendIntakeChatMessage(
     patientId: string,
     payload: SendChatMessageRequest
   ): Promise<SendChatMessageResponse> { /* axios POST */ }
   ```

2. **`useIntakeChat` hook**:
   - Local `messages: ChatMessage[]` state (initialized with empty array).
   - `isTyping: boolean` state (true while mutation is pending).
   - `isFallback: boolean` state (set true when `fallbackToManual: true` from API).
   - On `mutate(userMessage)`:
     - Append `{ role: 'user', content: userMessage }` to `messages` immediately (optimistic).
     - Set `isTyping = true`.
   - On success:
     - Append `{ role: 'assistant', content: response.assistantMessage }` to `messages`.
     - Set `isTyping = false`.
     - If `response.isComplete`: call `intake-store.mergeAnswers(response.structuredAnswers)`, set `isComplete = true`.
     - If `response.fallbackToManual`: set `isFallback = true` (banner shows, navigate after 2s).

3. **`ChatInputBar` component**:
   - MUI `TextField` (variant="outlined", fullWidth) with MUI `IconButton` `<SendIcon>`.
   - Disabled when `isTyping === true` or `isComplete === true`.
   - Submits on `Enter` (no newlines) or `Send` button click.
   - `inputProps={{ maxLength: 1000 }}`.

4. **`ChatMessageItem` component**:
   - AI messages: `Box` flexRow, MUI `Avatar` (primary.500 background, "AI" initials), `Card` with AI text content.
   - Patient messages: `Box` flexRow-reverse, `Avatar` (secondary, patient initials), `Card` (right-aligned).
   - Timestamp rendered as `Typography` variant="caption" below each card.

5. **`AiTypingIndicator` component**:
   - Three animated `CircularProgress` dots (size=8, staggered CSS animation) inside a Card container matching AI message style.
   - Conditionally rendered when `isTyping === true`.

6. **`IntakeFallbackBanner` component**:
   - MUI `Alert` severity="warning": "AI assistant is temporarily unavailable. Switching you to the manual form..."
   - Auto-navigates to `/appointments/intake/manual` after 2000ms.

7. **`ConversationalIntakePage` assembly**:
   - Guard: if `booking-store.patientDetails` is null, redirect to `/appointments/search`.
   - On mount: fire initial `sendIntakeChatMessage` with empty user message (`""`) to trigger the AI greeting (AC-1). The BE returns the opening greeting as `assistantMessage`.
   - Render booking stepper (step 4 active).
   - "Switch to manual" `Button` (variant="outlined", `SwapHorizIcon`) top-right: calls `intake-store.setMode('manual')` + `navigate('/appointments/intake/manual')`. Does NOT clear answers (FR-003 answer preservation).
   - `ChatMessageList` + `AiTypingIndicator` (when `isTyping`).
   - `ChatInputBar` (disabled when `isTyping` or `isComplete`).
   - If `isComplete`: MUI `Alert` severity="success" "All required information collected!" + MUI `Button` "Submit intake" → calls `useSubmitIntake` mutation (answers from `intake-store`, mode="conversational").
   - If `isFallback`: `IntakeFallbackBanner`.
   - Inactivity warning: `useEffect` with `setTimeout` 4 minutes → shows MUI `Snackbar` "Still there? Your progress is saved."

---

## Current Project State

```
client/
  src/
    App.tsx                                ← MODIFY: replace placeholder with ConversationalIntakePage
    pages/
      intake/
        ManualIntakeFormPage.tsx           ← Created in us_011/task_001
        components/
          IntakeProgressBar.tsx            ← Created in us_011/task_001
          ModeSwitchButton.tsx             ← Created in us_011/task_001
          IntakeQuestionField.tsx          ← Created in us_011/task_001
    stores/
      intake-store.ts                      ← Created in us_011/task_001 (mergeAnswers present)
    hooks/
      useSubmitIntake.ts                   ← Created in us_011/task_001
    api/
      intake.ts                            ← Created in us_011/task_001
```

> `pages/intake/ConversationalIntakePage.tsx`, `api/intakeChat.ts`, `hooks/useIntakeChat.ts`, and chat sub-components do not exist yet.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/api/intakeChat.ts` | Typed API client for the intake chat endpoint with `SendChatMessageRequest/Response` types |
| CREATE | `client/src/hooks/useIntakeChat.ts` | Manages `messages[]`, `isTyping`, `isComplete`, `isFallback` state; calls chat API; handles `mergeAnswers` on completion |
| CREATE | `client/src/pages/intake/ConversationalIntakePage.tsx` | SCR-005 page: stepper, mode-switch, ChatMessageList, AiTypingIndicator, ChatInputBar, submit/fallback logic |
| CREATE | `client/src/pages/intake/components/ChatMessageList.tsx` | Scrollable list with auto-scroll via `useRef` + `useEffect` |
| CREATE | `client/src/pages/intake/components/ChatMessageItem.tsx` | AI vs patient message card differentiation with MUI Avatar + Card |
| CREATE | `client/src/pages/intake/components/AiTypingIndicator.tsx` | Three-dot typing animation, shown while `isTyping` |
| CREATE | `client/src/pages/intake/components/ChatInputBar.tsx` | MUI TextField + Send button; Enter to submit; disabled when typing/complete |
| CREATE | `client/src/pages/intake/components/IntakeFallbackBanner.tsx` | Warning alert + auto-navigate to manual form after 2s |
| MODIFY | `client/src/App.tsx` | Replace `/appointments/intake/conversational` stub with `ConversationalIntakePage` |

---

## External References

- [MUI Card — chat message containers](https://mui.com/material-ui/react-card/)
- [MUI Avatar — AI/patient differentiation](https://mui.com/material-ui/react-avatar/)
- [MUI TextField — chat input](https://mui.com/material-ui/react-text-field/)
- [MUI Alert + Snackbar — fallback and inactivity banners](https://mui.com/material-ui/react-alert/)
- [TanStack React Query v4 — useMutation with optimistic updates](https://tanstack.com/query/v4/docs/framework/react/guides/optimistic-updates)
- [React useRef — auto-scroll to bottom of message list](https://react.dev/reference/react/useRef)

---

## Build Commands

```bash
# From client/
npm install
npx tsc --noEmit
npm run dev
npm run build
```

---

## Implementation Validation Strategy

- [ ] On mount, initial empty-message API call fires and AI greeting appears in `ChatMessageList` (AC-1)
- [ ] Send a message → AI typing indicator shows → AI response appears within 3s (AC-2 / AIR-Q02)
- [ ] "Switch to manual" button navigates to `/appointments/intake/manual` without clearing `intake-store.answers`
- [ ] When API returns `isComplete: true` + `structuredAnswers`, `mergeAnswers` called and Submit button appears (AC-4)
- [ ] When API returns `fallbackToManual: true`, `IntakeFallbackBanner` shows and auto-redirects to manual form (AC-5)
- [ ] `ChatInputBar` is disabled while AI is typing; re-enabled after response received
- [ ] `ChatMessageList` auto-scrolls to latest message after each append
- [ ] 4-minute inactivity `Snackbar` fires (test with short setTimeout override in dev)
- [ ] `booking-store.patientDetails` null guard redirects to `/appointments/search`
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment

---

## Implementation Checklist

- [x] Create `client/src/api/intakeChat.ts` — `ChatMessage`, `SendChatMessageRequest`, `SendChatMessageResponse` types; `sendIntakeChatMessage(patientId, payload)` function
- [x] Create `client/src/hooks/useIntakeChat.ts` — `messages`, `isTyping`, `isComplete`, `isFallback`, `showInactivityWarning` state; `useMutation` send; handle `mergeAnswers` on completion; handle `fallbackToManual` with 2s timed navigation; 4-min inactivity timer
- [x] Add `mergeAnswers(incoming: Record<string, string>)` to `client/src/stores/intake-store.ts` (shallow-merge over existing answers — AC-3)
- [x] Create `ChatMessageItem.tsx` — AI (primary.500 Avatar, left-aligned card, rounded `0 8px 8px 8px`) vs patient (secondary Avatar, right-aligned card, `8px 0 8px 8px`) with timestamp
- [x] Create `ChatMessageList.tsx` — scrollable container with `role="log"` + `aria-live="polite"`, `useRef` + `useEffect` auto-scroll to bottom; renders `AiTypingIndicator` when `isTyping`
- [x] Create `AiTypingIndicator.tsx` — three-dot CSS bounce animation in an AI-style card with `role="status"` aria label
- [x] Create `ChatInputBar.tsx` — MUI TextField (pill-shaped border-radius 20px) + Send `IconButton` 44px; Enter handler; disabled state when `isTyping || isComplete`
- [x] Create `IntakeFallbackBanner.tsx` — MUI Alert severity="warning" with "Switch now" action button; navigate handled by parent hook (AC-5)
- [x] Create `ConversationalIntakePage.tsx` — guard, stepper (step 4), mode-switch button (preserves answers AC-3), ChatMessageList, ChatInputBar, submit/fallback logic; fire greeting on mount (AC-1)
- [x] `App.tsx` already imports `ConversationalIntakePage` — no route changes needed (stub file was replaced)
- [x] TypeScript type-check: `npx tsc --noEmit` → 0 errors
- [ ] **[UI Tasks - MANDATORY]** Reference wireframe from Design References table during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate UI matches wireframe at 375px, 768px, 1440px before marking task complete
