# Task - task_001_fe_same_day_queue_arrival_ui

## Requirement Reference

- **User Story**: US_017 — Same-Day Queue & Arrival Management
- **Story Location**: `.propel/context/tasks/EP-002/us_017/us_017.md`
- **Acceptance Criteria**:
  - AC-1: When staff views the same-day queue, all queued patients are displayed with position, name, appointment time, and status badge (waiting/arrived/in-room/completed).
  - AC-2: When staff reorders queue entries by drag-and-drop, queue positions update in real-time in the UI, and the change is persisted via `PATCH /api/v1/staff/queue/reorder`.
  - AC-3: When staff clicks "Mark Arrived" on a patient, the status transitions to "arrived" immediately (optimistic UI per UXR-404), and the queue row updates.
  - AC-4: When the queue is updated by another staff user, the SignalR real-time push updates the current staff user's queue view without manual refresh.
  - AC-5: Queue loads within 2 seconds at p95 via Redis-cached `GET /api/v1/staff/queue` endpoint (NFR-001).
- **Edge Cases**:
  - Patient leaves before being seen → "Mark Left" secondary button on each row; status badge becomes neutral/gray, row is hidden from active queue view.
  - Redis cache miss → queue briefly shows Loading skeleton while fresh data is fetched; no user-visible blank state.
  - SignalR disconnection → automatic reconnect with exponential backoff; offline toast "Reconnecting to live queue…" while disconnected.
  - Optimistic update rolls back on API error → row reverts to previous status with error toast "Status update failed. Please try again."

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-012-same-day-queue.html`, `.propel/context/wireframes/Hi-Fi/wireframe-SCR-013-patient-arrival-marking.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-012`, `.propel/context/docs/figma_spec.md#SCR-013` |
| **UXR Requirements** | UXR-002 (breadcrumbs: `Home > Staff Dashboard > Same-Day Queue`), UXR-101 (WCAG 2.2 AA), UXR-401 (< 200ms loading feedback), UXR-402 (success/error toast for status changes), UXR-404 (optimistic UI with rollback on `PATCH /status` failure) |
| **Design Tokens** | `designsystem.md#colors` (`primary.500: #2196F3` drag handles/CTAs; `success.main: #4CAF50` arrived badge; `warning.main: #FF9800` waiting badge; `secondary.500: #9C27B0` in-room badge; `neutral` gray for left/completed), `designsystem.md#typography`, `designsystem.md#spacing` (8px grid) |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local file exists at specified path

### CRITICAL: Wireframe Implementation Requirement

- **MUST** open both wireframe files and match layout for:
  - **SCR-012 Same-Day Queue**: draggable Table rows with drag-handle `IconButton` (left column), status `Badge`, appointment time, patient name, "Mark Arrived" `Button` per row.
  - **SCR-013 Patient Arrival Marking**: `Checkbox` column for bulk selection, "Mark selected as arrived" primary `Button`, individual row action buttons, success `Toast`.
- **MUST** implement all required states:
  - **SCR-012**: Default (queue with patients), Loading (Skeleton rows), Empty (no patients today + CTA), Error (MUI Alert + Retry).
  - **SCR-013**: Default (checkboxes visible, bulk button), Loading (button spinner during bulk operation), Error (MUI Alert).
- **MUST** validate implementation against wireframes at breakpoints: 375px, 768px, 1440px.
- Run `/analyze-ux` after implementation to verify pixel-perfect alignment.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| UI Components | Material-UI (MUI) | 5.x |
| State Management | React Query (TanStack Query) | 4.x |
| State Management | Zustand | 4.x |
| Drag-and-Drop | @dnd-kit/sortable | 8.x (free/OSS, MIT) |
| Real-Time | @microsoft/signalr | 8.x (free/OSS) |
| Routing | React Router DOM | 6.x |
| Language | TypeScript | 5.x |
| Build | Vite | 5.x |

> All code and libraries MUST be compatible with versions above. `@dnd-kit/sortable` and `@microsoft/signalr` satisfy NFR-015 (free/OSS).

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

Implement two staff-only screens completing the same-day operations workflow (FL-003 continuation from US_016):

**SCR-012 — Same-Day Queue** (P0):
- Fetches today's queue via `GET /api/v1/staff/queue` (React Query, 30s staleTime matching Redis TTL).
- Renders an MUI `Table` with columns: drag-handle `IconButton`, position number, patient name, appointment time, status `Badge`, action buttons.
- Status badge colour mapping: `waiting` → warning (orange), `arrived` → success (green), `in-room` → secondary (purple), `completed` → neutral (gray), `left` → neutral (gray).
- Drag-to-reorder using `@dnd-kit/sortable` — `DndContext` wraps the table body; `SortableContext` with `verticalListSortingStrategy`; each row is a `useSortable` item. On `onDragEnd`, calls `PATCH /api/v1/staff/queue/reorder` with new ordered IDs array.
- Real-time updates via ASP.NET Core SignalR: `HubConnection` subscribes to `QueueUpdated` event → calls `queryClient.invalidateQueries(['queue'])` to re-fetch with updated data.
- Breadcrumb: `Home > Staff Dashboard > Same-Day Queue` (UXR-002).
- States: Default, Loading (Skeleton rows), Empty (no active patients + "Book Walk-In" CTA), Error (Alert + Retry).

**SCR-013 — Patient Arrival Marking** (P0 / inline within SCR-012):
- SCR-013 is rendered as an overlay/extended state within the queue table rather than a separate page.
- Each row has an individual "Mark Arrived" `Button` (calls `PATCH /api/v1/appointments/{id}/status` with `{ status: "arrived" }`).
- **Optimistic UI (UXR-404)**: On click, immediately update the local `appointments` query cache entry to `arrived`; if API fails, revert using React Query `onError` context.
- Bulk arrival: `Checkbox` in each row header enables multi-select mode; "Mark selected as arrived" primary `Button` appears in the table toolbar when ≥ 1 row is selected.
- "Mark Left" secondary `Button` in each row action area → `PATCH /api/v1/appointments/{id}/status` with `{ status: "left" }`; row moves to end of table with grayed badge.
- Success `Toast` on each individual or bulk status change.

---

## Dependent Tasks

- **task_002_be_queue_management_api.md** (US_017) — `GET /api/v1/staff/queue`, `PATCH /api/v1/staff/queue/reorder`, `PATCH /api/v1/appointments/{id}/status`, and `QueueHub` SignalR endpoint must be available.
- **task_001_fe_staff_dashboard_walkin_booking_ui.md** (US_016) — `StaffRouteGuard` and staff layout patterns established; reuse for queue screen.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/staff/queue/SameDayQueuePage.tsx` | SCR-012 + SCR-013 — sortable table, status badges, optimistic arrival marking |
| CREATE | `client/src/pages/staff/queue/QueueRow.tsx` | Reusable draggable table row (useSortable, status badge, action buttons) |
| CREATE | `client/src/hooks/useSameDayQueue.ts` | React Query hook for `GET /api/v1/staff/queue` with 30s staleTime |
| CREATE | `client/src/hooks/useUpdateAppointmentStatus.ts` | Optimistic `useMutation` for `PATCH /api/v1/appointments/{id}/status` |
| CREATE | `client/src/hooks/useQueueSignalR.ts` | SignalR `HubConnection` hook — connects to `/hubs/queue`, handles `QueueUpdated` broadcast |
| MODIFY | `client/src/api/staff.ts` | Add `reorderQueue(orderedIds)` and `updateAppointmentStatus(id, status)` typed functions |
| MODIFY | `client/src/App.tsx` | Add `/staff/queue` route inside `<StaffRouteGuard>` |

---

## Implementation Plan

1. **Install dependencies**:
   ```bash
   npm install @dnd-kit/core @dnd-kit/sortable @dnd-kit/utilities @microsoft/signalr
   ```

2. **`staff.ts` additions**:
   ```typescript
   export interface QueueEntry {
     appointmentId: string;
     queuePosition: number;
     patientName: string;
     appointmentTime: string;    // ISO 8601
     status: 'waiting' | 'arrived' | 'in-room' | 'completed' | 'left';
     visitType: string;
   }

   export async function getSameDayQueue(): Promise<QueueEntry[]>
   // GET /api/v1/staff/queue

   export async function reorderQueue(orderedIds: string[]): Promise<void>
   // PATCH /api/v1/staff/queue/reorder  Body: { orderedAppointmentIds: string[] }

   export async function updateAppointmentStatus(
     appointmentId: string,
     status: 'arrived' | 'in-room' | 'left'
   ): Promise<void>
   // PATCH /api/v1/appointments/{appointmentId}/status  Body: { status }
   ```

3. **`useQueueSignalR.ts`** — real-time connection with reconnect:
   ```typescript
   export function useQueueSignalR() {
     const queryClient = useQueryClient();
     useEffect(() => {
       const connection = new HubConnectionBuilder()
         .withUrl('/hubs/queue', { accessTokenFactory: () => getAuthToken() })
         .withAutomaticReconnect([0, 2000, 5000, 10000])   // backoff intervals ms
         .build();

       connection.on('QueueUpdated', () => {
         queryClient.invalidateQueries({ queryKey: ['queue'] });
       });

       connection.onreconnecting(() => showToast('info', 'Reconnecting to live queue…'));
       connection.onreconnected(() => showToast('success', 'Live queue reconnected.'));

       connection.start().catch(console.error);
       return () => { connection.stop(); };
     }, [queryClient]);
   }
   ```

4. **`useUpdateAppointmentStatus.ts`** — optimistic mutation:
   ```typescript
   export function useUpdateAppointmentStatus() {
     const queryClient = useQueryClient();
     return useMutation({
       mutationFn: ({ appointmentId, status }) =>
         updateAppointmentStatus(appointmentId, status),
       onMutate: async ({ appointmentId, status }) => {
         await queryClient.cancelQueries({ queryKey: ['queue'] });
         const previous = queryClient.getQueryData<QueueEntry[]>(['queue']);
         // Optimistic update — replace entry status in cache
         queryClient.setQueryData<QueueEntry[]>(['queue'], (old) =>
           old?.map(e => e.appointmentId === appointmentId ? { ...e, status } : e) ?? []
         );
         return { previous };
       },
       onError: (_err, _vars, context) => {
         queryClient.setQueryData(['queue'], context?.previous);  // rollback
         showToast('error', 'Status update failed. Please try again.');
       },
       onSuccess: () => showToast('success', 'Patient status updated.'),
     });
   }
   ```

5. **`QueueRow.tsx`** — `useSortable` drag handle + action buttons:
   ```tsx
   const { attributes, listeners, setNodeRef, transform, transition } = useSortable({ id: entry.appointmentId });
   // Drag handle: <IconButton {...listeners} {...attributes}><DragIndicatorIcon/></IconButton>
   // Status badge: <Chip label={entry.status} color={statusColorMap[entry.status]} size="small"/>
   // Mark Arrived: <Button onClick={() => mutate({ appointmentId: entry.appointmentId, status: 'arrived' })}>Mark Arrived</Button>
   // Mark Left: <Button variant="text" color="warning" onClick={...}>Mark Left</Button>
   ```

6. **`SameDayQueuePage.tsx`** — drag context + bulk selection:
   ```tsx
   // DndContext onDragEnd -> reorder local array -> call reorderQueue(newOrderedIds)
   // Bulk select: Checkbox in header toggles selectAll; selected state in useState
   // "Mark selected as arrived" Button calls mutate for each selected ID sequentially
   // Call useQueueSignalR() at top of component to establish live connection
   ```

---

## Current Project State

```
client/src/
  pages/
    staff/
      dashboard/           ← us_016/task_001
      walk-in/             ← us_016/task_001
      queue/               ← THIS TASK (SCR-012 + SCR-013)
  api/
    staff.ts               ← us_016/task_001 (extend with queue/status functions)
  hooks/
    usePatientSearch.ts    ← us_016/task_001
    useBookWalkIn.ts       ← us_016/task_001
    useSameDayQueue.ts     ← THIS TASK (create)
    useUpdateAppointmentStatus.ts ← THIS TASK (create)
    useQueueSignalR.ts     ← THIS TASK (create)
  components/
    guards/
      StaffRouteGuard.tsx  ← us_016/task_001
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/staff/queue/SameDayQueuePage.tsx` | SCR-012 + SCR-013 with DndContext, SignalR, bulk selection, all states |
| CREATE | `client/src/pages/staff/queue/QueueRow.tsx` | Draggable row with useSortable, status badge, action buttons |
| CREATE | `client/src/hooks/useSameDayQueue.ts` | React Query hook, 30s staleTime |
| CREATE | `client/src/hooks/useUpdateAppointmentStatus.ts` | Optimistic mutation with rollback |
| CREATE | `client/src/hooks/useQueueSignalR.ts` | SignalR hub connection with auto-reconnect |
| MODIFY | `client/src/api/staff.ts` | Add `QueueEntry`, `getSameDayQueue`, `reorderQueue`, `updateAppointmentStatus` |
| MODIFY | `client/src/App.tsx` | Add `/staff/queue` route inside `<StaffRouteGuard>` |

---

## External References

- [@dnd-kit/sortable — sortable list implementation](https://docs.dndkit.com/presets/sortable)
- [@dnd-kit/sortable — `useSortable` hook](https://docs.dndkit.com/presets/sortable/usesortable)
- [@microsoft/signalr — React integration (auto-reconnect)](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-8.0)
- [React Query — optimistic updates with `onMutate`/`onError`/`context`](https://tanstack.com/query/v4/docs/react/guides/optimistic-updates)
- [MUI Table — sortable rows pattern](https://mui.com/material-ui/react-table/)
- [UXR-404 — optimistic UI requirement](../.propel/context/docs/figma_spec.md)
- [NFR-001 — 2s p95 response target](../.propel/context/docs/design.md#NFR-001)

---

## Build Commands

```bash
cd client
npm install @dnd-kit/core @dnd-kit/sortable @dnd-kit/utilities @microsoft/signalr
npm run dev
npm run type-check
npm run build
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass (`useUpdateAppointmentStatus` optimistic update + rollback on error)
- [ ] Drag-and-drop reorders rows visually and calls `reorderQueue` with new IDs array
- [ ] SignalR `QueueUpdated` event triggers React Query cache invalidation
- [ ] SignalR disconnection shows "Reconnecting…" toast; reconnection shows "reconnected" toast
- [ ] "Mark Arrived" immediately updates status badge (optimistic); reverts on API 4xx/5xx
- [ ] Bulk "Mark selected as arrived" processes all selected rows with per-row success toasts
- [ ] **[UI Tasks]** Visual comparison against wireframes at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment

---

## Implementation Checklist

- [x] Install `@dnd-kit/core`, `@dnd-kit/sortable`, `@dnd-kit/utilities`, `@microsoft/signalr` packages
- [x] Extend `client/src/api/staff.ts` with `QueueEntry` type, `getSameDayQueue`, `reorderQueue`, `updateAppointmentStatus`
- [x] Create `useSameDayQueue.ts` React Query hook with 30s staleTime
- [x] Create `useUpdateAppointmentStatus.ts` optimistic mutation with `onMutate`/`onError` rollback
- [x] Create `useQueueSignalR.ts` with `withAutomaticReconnect` and `QueueUpdated` → `invalidateQueries`
- [x] Create `QueueRow.tsx` with `useSortable`, status `Chip`, "Mark Arrived" and "Mark Left" buttons
- [x] Create `SameDayQueuePage.tsx` with `DndContext`, `SortableContext`, bulk checkbox selection, all 4 SCR-012 states, breadcrumb
- [x] Modify `App.tsx` to add `/staff/queue` route inside `<StaffRouteGuard>`
- [x] **[UI Tasks - MANDATORY]** Reference wireframes from Design References table during implementation
- [x] **[UI Tasks - MANDATORY]** Validate UI matches wireframes before marking task complete
