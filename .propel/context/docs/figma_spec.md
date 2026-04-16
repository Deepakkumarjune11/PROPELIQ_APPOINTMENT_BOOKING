# Figma Design Specification - Unified Patient Access & Clinical Intelligence Platform

## 1. Figma Specification
**Platform**: Web (Responsive - Mobile 320px+, Tablet 768px+, Desktop 1024px+)

---

## 2. Source References

### Primary Source
| Document | Path | Purpose |
|----------|------|---------|
| Requirements | `.propel/context/docs/spec.md` | Personas, use cases, epics with UI impact flags |
| Epics | `.propel/context/docs/epics.md` | 8 UI-impacting epics (EP-TECH, EP-001, EP-002, EP-003, EP-004, EP-007, EP-009, EP-010) |

### Optional Sources
| Document | Path | Purpose |
|----------|------|---------|
| Wireframes | `.propel/context/wireframes/` | Entity understanding, content structure (NOT AVAILABLE) |
| Design Assets | `.propel/context/Design/` | Visual references from spec.md epics (NOT AVAILABLE) |

### Related Documents
| Document | Path | Purpose |
|----------|------|---------|
| Design System | `.propel/context/docs/designsystem.md` | Tokens, branding, component specifications |
| Models | `.propel/context/docs/models.md` | Architectural diagrams, sequence diagrams for use case flows |

---

## 3. UX Requirements

*Generated based on use cases with UI impact. These requirements apply to screen implementations and are only created when UI impact exists.*

### UXR Requirements Table

| UXR-ID | Category | Requirement | Acceptance Criteria | Screens Affected |
|--------|----------|-------------|---------------------|------------------|
| UXR-001 | Usability | System MUST provide navigation to any feature in max 3 clicks from authenticated dashboard | Click count audit passes for all workflows | All screens |
| UXR-002 | Usability | System MUST display clear navigation hierarchy with breadcrumbs for multi-step workflows | Breadcrumb component visible on all nested screens | SCR-010, SCR-016, SCR-017, SCR-018, SCR-019, SCR-028 |
| UXR-003 | Usability | System MUST provide inline guidance for complex workflows (conversational intake, conflict resolution, code verification) | Help text/tooltips present, user task completion rate > 90% | SCR-004, SCR-005, SCR-009, SCR-017, SCR-018, SCR-019 |
| UXR-101 | Accessibility | System MUST comply with WCAG 2.2 AA standards | WAVE/axe audit passes, color contrast >= 4.5:1 text, >= 3:1 UI | All screens |
| UXR-102 | Accessibility | System MUST support screen reader navigation with semantic HTML and ARIA labels | Screen reader audit passes, all interactive elements announced | All screens |
| UXR-103 | Accessibility | System MUST support full keyboard navigation with visible focus indicators | Tab order audit passes, focus visible on all interactive elements | All screens |
| UXR-104 | Accessibility | System MUST meet minimum color contrast of 4.5:1 for text, 3:1 for UI components | Color contrast audit passes using WebAIM tool | All screens |
| UXR-105 | Accessibility | System MUST display visible focus indicators (2px solid primary.500 outline) on all interactive elements | Focus indicator audit passes | All screens |
| UXR-201 | Responsiveness | System MUST adapt to mobile (320px+), tablet (768px+), desktop (1024px+) breakpoints | Responsive audit passes at all breakpoints | All screens |
| UXR-202 | Responsiveness | System MUST provide touch targets minimum 44x44px on mobile and tablet | Touch target audit passes, no targets < 44x44px | All screens |
| UXR-203 | Responsiveness | System MUST use adaptive navigation: bottom nav for mobile, sidebar for desktop | Navigation pattern appropriate for viewport width | SCR-025 |
| UXR-301 | Visual Design | System MUST use Material-UI design system with healthcare-appropriate color palette | Design token validation passes, all colors from designsystem.md | All screens |
| UXR-302 | Visual Design | System MUST use consistent spacing based on 8px grid system | Spacing audit passes, all spacing multiples of 8px | All screens |
| UXR-303 | Visual Design | System MUST use healthcare-appropriate semantic colors (clinical vitals pink, medications orange, etc.) | Fact category color coding validated | SCR-017, SCR-019 |
| UXR-401 | Interaction | System MUST provide loading feedback within 200ms of user action | User perceives immediate response, <200ms spinner display | All screens |
| UXR-402 | Interaction | System MUST provide success/error feedback for all state-changing actions | Toast notification appears for all mutations (booking, upload, verification) | All screens |
| UXR-403 | Interaction | System MUST display progress indicators for multi-step workflows | Progress stepper visible on booking (3 steps), intake (dynamic), verification (4 steps) | SCR-001-SCR-006, SCR-016-SCR-020 |
| UXR-404 | Interaction | System MUST use optimistic UI updates with rollback on failure | Slot selection immediately updates UI, reverts on conflict error | SCR-002, SCR-013, SCR-014 |
| UXR-501 | Error Handling | System MUST display actionable error messages with recovery paths | Error messages include "Try again" action, alternative path suggestion | All screens |
| UXR-502 | Error Handling | System MUST provide inline validation feedback on form fields | Field validation triggers on blur, error text displays below field | SCR-003, SCR-004, SCR-011, SCR-022, SCR-023, SCR-024, SCR-026, SCR-027 |
| UXR-503 | Error Handling | System MUST handle network errors gracefully with retry options | Network error toast with "Retry" button, offline state indicator | All screens |
| UXR-504 | Error Handling | System MUST warn users 1 minute before session timeout (15-minute inactivity) | Modal warning at 14-minute mark with "Stay Logged In" button | All authenticated screens |

### UXR Derivation Logic
- **Usability UXR**: Derived from UC-XXX success paths that require navigation depth control, orientation, and workflow guidance
- **Accessibility UXR**: Derived from WCAG 2.2 AA standards + designsystem.md constraints (color contrast, focus indicators, touch targets)
- **Responsiveness UXR**: Derived from platform targets (web responsive 320px-1536px+) + breakpoint definitions in designsystem.md
- **Visual Design UXR**: Derived from designsystem.md Material-UI token requirements and healthcare color coding
- **Interaction UXR**: Derived from flow complexity (booking 3-step, verification 4-step) + state transition feedback requirements
- **Error Handling UXR**: Derived from UC-XXX alternative/exception paths (booking errors, upload failures, conflict rejection, session timeout FR-017)

### UXR Numbering Convention
- UXR-001 to UXR-099: Project-wide requirements (usability baseline)
- UXR-1XX: Accessibility requirements (WCAG compliance)
- UXR-2XX: Responsiveness requirements (breakpoint behavior)
- UXR-3XX: Visual design requirements (design system adherence)
- UXR-4XX: Interaction requirements (feedback, optimistic UI)
- UXR-5XX: Error handling requirements (recovery paths)

---

## 4. Personas Summary

*Derived from spec.md - Reference only, do not duplicate full persona details*

| Persona | Role | Primary Goals | Key Screens |
|---------|------|---------------|-------------|
| Patient | Self-service user | Book appointments, complete intake, upload documents, view appointments | SCR-001, SCR-002, SCR-003, SCR-004, SCR-005, SCR-006, SCR-008, SCR-009, SCR-014, SCR-015, SCR-024 |
| Staff | Clinical/operational staff | Manage walk-ins, verify clinical data, resolve conflicts, confirm codes | SCR-010, SCR-011, SCR-012, SCR-013, SCR-014, SCR-015, SCR-016, SCR-017, SCR-018, SCR-019, SCR-020, SCR-024, SCR-028 |
| Admin | System administrator | Manage users and access control | SCR-021, SCR-022, SCR-023, SCR-024, SCR-028 |

---

## 5. Information Architecture

### Site Map
```text
Unified Patient Access & Clinical Intelligence Platform
+-- Authentication
|   +-- SCR-024: Login
|
+-- Patient Portal (Patient Persona)
|   +-- SCR-001: Availability Search
|   +-- SCR-002: Slot Selection
|   +-- SCR-003: Patient Details Form (inline account creation)
|   +-- SCR-004: Manual Intake Form
|   +-- SCR-005: Conversational Intake (AI chat)
|   +-- SCR-006: Booking Confirmation
|   +-- SCR-008: My Appointments
|   +-- SCR-009: Preferred Slot Selection (watchlist)
|   +-- SCR-014: Document Upload
|   +-- SCR-015: Document List
|   +-- SCR-026: User Profile
|   +-- SCR-027: Settings
|
+-- Staff Portal (Staff Persona)
|   +-- SCR-010: Staff Dashboard
|   +-- SCR-011: Walk-In Booking
|   +-- SCR-012: Same-Day Queue Management
|   +-- SCR-013: Patient Arrival Marking
|   +-- SCR-014: Document Upload (staff-initiated)
|   +-- SCR-015: Document List
|   +-- SCR-016: Patient Chart Review (verification queue)
|   +-- SCR-017: 360-Degree Patient View (with source citations)
|   +-- SCR-018: Conflict Resolution
|   +-- SCR-019: Code Verification (ICD-10/CPT suggestions)
|   +-- SCR-020: Verification Complete
|   +-- SCR-028: Operational Metrics Dashboard
|   +-- SCR-026: User Profile
|   +-- SCR-027: Settings
|
+-- Admin Portal (Admin Persona)
    +-- SCR-021: User Management
    +-- SCR-022: Create/Edit User
    +-- SCR-023: Role Assignment
    +-- SCR-028: Operational Metrics Dashboard
    +-- SCR-026: User Profile
    +-- SCR-027: Settings
```

### Navigation Patterns
| Pattern | Type | Platform Behavior |
|---------|------|-------------------|
| Primary Nav | Sidebar (Desktop) / Bottom Nav (Mobile) | Desktop: Persistent sidebar 240px width, collapsible to icons. Mobile: Fixed bottom navigation 56px height |
| Secondary Nav | Tabs / Breadcrumbs | Tabs for category switching (fact categories in 360-view). Breadcrumbs for workflow depth (Staff Dashboard > Patient Chart > 360-View > Conflict Resolution) |
| Utility Nav | User menu (Avatar dropdown) | Top-right avatar with dropdown: Profile, Settings, Logout |

---

## 6. Screen Inventory

*All screens derived from use cases in spec.md*

### Screen List
| Screen ID | Screen Name | Derived From | Personas Covered | Priority | States Required |
|-----------|-------------|--------------|------------------|----------|-----------------|
| SCR-001 | Availability Search | UC-001 | Patient | P0 | Default, Loading, Empty, Error, N/A |
| SCR-002 | Slot Selection | UC-001 | Patient | P0 | Default, Loading, N/A, Error, N/A |
| SCR-003 | Patient Details Form | UC-001 | Patient | P0 | Default, Loading, N/A, Error, Validation |
| SCR-004 | Manual Intake Form | UC-001 | Patient | P0 | Default, Loading, N/A, Error, Validation |
| SCR-005 | Conversational Intake | UC-001 | Patient | P0 | Default, Loading, Empty, Error, Validation |
| SCR-006 | Booking Confirmation | UC-001, UC-002 | Patient | P0 | Default, Loading, N/A, Error, N/A |
| SCR-007 | Booking Error | UC-001 | Patient | P0 | Default, N/A, N/A, Error, N/A |
| SCR-008 | My Appointments | UC-002 | Patient | P0 | Default, Loading, Empty, Error, N/A |
| SCR-009 | Preferred Slot Selection | UC-002 | Patient | P1 | Default, Loading, Empty, Error, N/A |
| SCR-010 | Staff Dashboard | UC-003 | Staff | P0 | Default, Loading, Empty, Error, N/A |
| SCR-011 | Walk-In Booking | UC-003 | Staff | P0 | Default, Loading, N/A, Error, Validation |
| SCR-012 | Same-Day Queue | UC-003 | Staff | P0 | Default, Loading, Empty, Error, N/A |
| SCR-013 | Patient Arrival Marking | UC-003 | Staff | P0 | Default, Loading, N/A, Error, N/A |
| SCR-014 | Document Upload | UC-004 | Patient, Staff | P1 | Default, Loading, N/A, Error, Validation |
| SCR-015 | Document List | UC-004 | Patient, Staff | P1 | Default, Loading, Empty, Error, N/A |
| SCR-016 | Patient Chart Review | UC-005 | Staff | P0 | Default, Loading, Empty, Error, N/A |
| SCR-017 | 360-Degree Patient View | UC-005 | Staff | P0 | Default, Loading, Empty, Error, N/A |
| SCR-018 | Conflict Resolution | UC-005 | Staff | P0 | Default, Loading, N/A, Error, Validation |
| SCR-019 | Code Verification | UC-005 | Staff | P0 | Default, Loading, Empty, Error, N/A |
| SCR-020 | Verification Complete | UC-005 | Staff | P0 | Default, N/A, N/A, N/A, N/A |
| SCR-021 | User Management | UC-006 | Admin | P1 | Default, Loading, Empty, Error, N/A |
| SCR-022 | Create/Edit User | UC-006 | Admin | P1 | Default, Loading, N/A, Error, Validation |
| SCR-023 | Role Assignment | UC-006 | Admin | P1 | Default, Loading, N/A, Error, Validation |
| SCR-024 | Login | All | All | P0 | Default, Loading, N/A, Error, Validation |
| SCR-025 | Header/Navigation | All | All | P0 | Default, N/A, N/A, N/A, N/A |
| SCR-026 | User Profile | All | All | P2 | Default, Loading, N/A, Error, Validation |
| SCR-027 | Settings | All | All | P3 | Default, Loading, N/A, Error, Validation |
| SCR-028 | Operational Metrics Dashboard | FR-018 | Staff, Admin | P2 | Default, Loading, Empty, Error, N/A |

### Priority Legend
- **P0**: Critical path (must-have for MVP) - Authentication, booking flow, walk-in management, clinical verification
- **P1**: Core functionality (high priority) - Preferred slot swap, document upload, user management
- **P2**: Important features (medium priority) - User profile, operational metrics dashboard
- **P3**: Nice-to-have (low priority) - Settings customization

### Screen-to-Persona Coverage Matrix
| Screen | Patient | Staff | Admin | Notes |
|--------|---------|-------|-------|-------|
| SCR-001 Availability Search | Primary | - | - | Patient self-service entry point |
| SCR-002 Slot Selection | Primary | - | - | Patient booking flow |
| SCR-003 Patient Details Form | Primary | - | - | Inline account creation, insurance capture |
| SCR-004 Manual Intake Form | Primary | - | - | Alternative to conversational intake |
| SCR-005 Conversational Intake | Primary | - | - | AI-assisted intake with mode switch |
| SCR-006 Booking Confirmation | Primary | - | - | Success state with reminders |
| SCR-007 Booking Error | Primary | - | - | Error recovery path |
| SCR-008 My Appointments | Primary | - | - | View booked appointments, watchlist status |
| SCR-009 Preferred Slot Selection | Primary | - | - | Waitlist registration |
| SCR-010 Staff Dashboard | - | Primary | - | Staff operational hub |
| SCR-011 Walk-In Booking | - | Primary | - | Staff-controlled walk-in |
| SCR-012 Same-Day Queue | - | Primary | - | Queue management, drag-to-reorder |
| SCR-013 Patient Arrival Marking | - | Primary | - | Arrival check-in |
| SCR-014 Document Upload | Secondary | Primary | - | Patient pre-visit, staff post-visit uploads |
| SCR-015 Document List | Secondary | Primary | - | Processing status tracking |
| SCR-016 Patient Chart Review | - | Primary | - | Verification queue entry |
| SCR-017 360-Degree Patient View | - | Primary | - | Consolidated facts with source citations |
| SCR-018 Conflict Resolution | - | Primary | - | Mandatory conflict review |
| SCR-019 Code Verification | - | Primary | - | ICD-10/CPT confirmation with evidence |
| SCR-020 Verification Complete | - | Primary | - | Chart finalization |
| SCR-021 User Management | - | - | Primary | Admin user CRUD |
| SCR-022 Create/Edit User | - | - | Primary | User account modal |
| SCR-023 Role Assignment | - | - | Primary | Role and permissions management |
| SCR-024 Login | Primary | Primary | Primary | Authentication for all personas |
| SCR-025 Header/Navigation | Primary | Primary | Primary | Shared navigation shell |
| SCR-026 User Profile | Secondary | Secondary | Secondary | Profile edit for all personas |
| SCR-027 Settings | Secondary | Secondary | Secondary | Preferences for all personas |
| SCR-028 Operational Metrics Dashboard | - | Secondary | Primary | Metrics for staff/admin oversight |

### Modal/Overlay Inventory
| Name | Type | Trigger | Parent Screen(s) | Priority |
|------|------|---------|-----------------|----------|
| Login Modal | Modal | Unauthenticated access | All protected screens | P0 |
| Session Timeout Warning | Dialog | 14-minute inactivity | All authenticated screens | P0 |
| Create/Edit User | Modal | "Create User" / Edit icon click | SCR-021 User Management | P1 |
| Role Assignment | Modal | "Assign Role" button | SCR-021, SCR-022 | P1 |
| Conflict Resolution Dialog | Dialog | Conflict flag click | SCR-017 360-Degree Patient View | P0 |
| Source Citation Drawer | Drawer (right slide) | Fact click in 360-view | SCR-017, SCR-019 | P0 |
| Delete Document Confirmation | Dialog | Delete icon click | SCR-015 Document List | P1 |
| Preferred Slot Calendar | Modal | "Select Preferred Slot" button | SCR-009 Preferred Slot Selection | P1 |
| Booking Confirmation PDF Preview | Modal | "View PDF" button | SCR-006 Booking Confirmation | P1 |

---

## 7. Content & Tone

### Voice & Tone
- **Overall Tone**: Professional, reassuring, clinical trust-first (balancing efficiency with empathy)
- **Error Messages**: Helpful, non-blaming, actionable
  - ❌ Bad: "Error 500. Failed."
  - ✅ Good: "Unable to load appointments. Please check your connection and try again."
- **Empty States**: Encouraging, guiding, with clear CTA
  - ❌ Bad: "No data."
  - ✅ Good: "No appointments yet. Start by searching available slots."
- **Success Messages**: Brief, celebratory, next-action oriented
  - ❌ Bad: "Success."
  - ✅ Good: "Appointment confirmed! Check your email for details."

### Content Guidelines
- **Headings**: Sentence case (e.g., "Patient details" not "Patient Details")
- **CTAs**: Action-oriented, specific verbs (e.g., "Book appointment" not "Submit", "Confirm codes" not "OK")
- **Labels**: Concise, descriptive (e.g., "Insurance member ID" not "ID")
- **Placeholder Text**: Helpful examples (e.g., "name@example.com" for email, "MM/DD/YYYY" for dates), no "Lorem ipsum" in final

---

## 8. Data & Edge Cases

### Data Scenarios
| Scenario | Description | Handling |
|----------|-------------|----------|
| No Data | New user with no appointments, documents, or activity | Empty state with illustration, encouraging CTA (e.g., "Book your first appointment") |
| First Use | New patient onboarding after inline account creation | Welcome tooltip on dashboard, optional onboarding tour |
| Large Data | Staff user with 100+ patients in verification queue | Pagination (25 per page), infinite scroll for document lists, virtualized table rows |
| Slow Connection | >3s load time for availability search or 360-view | Skeleton screens, loading shimmer effect, "Loading..." text with spinner |
| Offline | No network connection | Offline banner at top, cached data displayed (read-only), toast "You're offline. Reconnect to sync changes." |

### Edge Cases
| Case | Screen(s) Affected | Solution |
|------|-------------------|----------|
| Long text | All screens with user-generated content (patient names, conflict descriptions, code evidence) | Truncation with tooltip on hover (desktop) or expand on tap (mobile) |
| Missing image | SCR-026 User Profile (avatar) | Fallback to initials avatar (2-letter, primary.500 background) |
| Form validation | SCR-003, SCR-004, SCR-011, SCR-022 (all forms) | Inline error messages below field, red border, error icon, submit button disabled until valid |
| Session timeout | All authenticated screens | Modal warning at 14-minute mark: "Your session will expire in 1 minute. Stay logged in?" with "Yes" and "Logout" buttons. At 15 minutes, auto-logout to SCR-024 Login with toast "Session expired. Please login again." |
| Concurrent booking | SCR-002 Slot Selection (slot claimed by another user) | Optimistic UI immediately shows selection, but on 409 Conflict API response, revert UI and display toast "Slot no longer available. Please select another." |
| File upload too large | SCR-014 Document Upload | Validation toast "File size must be under 25MB. Please select a smaller file." |
| No-show risk > 70% | SCR-002 Slot Selection | Orange warning badge "High no-show risk detected" with tooltip explaining calculation (time-to-appointment, insurance status) |
| AI extraction confidence < 70% | SCR-015 Document List | Yellow "Manual review required" badge, document flagged in staff verification queue |
| Critical conflict detected | SCR-017 360-Degree Patient View | Red conflict badge with count (e.g., "2 conflicts"), mandatory resolution before verification |
| All codes rejected by staff | SCR-019 Code Verification | Allow "Reject all" with justification text field, track AI-human agreement degradation |

---

## 9. Branding & Visual Direction

*See `designsystem.md` for all design tokens (colors, typography, spacing, shadows, etc.)*

### Branding Assets
- **Logo**: [To be provided] - Placeholder: "PropelIQ Healthcare" text logo in primary.500
- **Icon Style**: Material Icons (outlined variant) - bundled with MUI
- **Illustration Style**: Flat, minimal healthcare illustrations for empty states (e.g., calendar icon for empty appointments, document icon for empty document list)
- **Photography Style**: Not applicable for Phase 1 (no hero images or photography planned)

### Healthcare Color Coding
- **Vitals**: Pink (#E91E63) - Blood pressure, heart rate, temperature facts
- **Medications**: Deep Orange (#FF5722) - Prescription drugs, dosages
- **History**: Brown (#795548) - Medical history, family history, allergies
- **Diagnoses**: Deep Purple (#673AB7) - ICD-10 codes, condition descriptions
- **Procedures**: Teal (#009688) - CPT codes, surgical history, treatments

---

## 10. Component Specifications

*Component specifications defined in designsystem.md. Requirements per screen listed below.*

### Component Library Reference
**Source**: `.propel/context/docs/designsystem.md` (Component Specifications section)

### Required Components per Screen

| Screen ID | Screen Name | Components Required | Notes |
|-----------|-------------|---------------------|-------|
| SCR-001 | Availability Search | DatePicker, Card (N), Button, TextField (search), Skeleton | Calendar view with slot cards (grid layout) |
| SCR-002 | Slot Selection | DatePicker, Card (selected slot), Button (2), Badge (risk score) | Selected slot highlight (primary.500 border), no-show risk badge |
| SCR-003 | Patient Details Form | TextField (5), Select (2), Button (2), Alert | Email, name, DOB, phone, insurance provider/member ID fields |
| SCR-004 | Manual Intake Form | TextField (N), Select (N), Checkbox (N), Button (2), ProgressBar | Dynamic form fields based on intake questions, progress stepper |
| SCR-005 | Conversational Intake | Card (chat message), TextField (input), Button (1), IconButton (mode switch), Avatar, Badge | Chat interface with AI/user message differentiation, "Switch to manual" button |
| SCR-006 | Booking Confirmation | Card, Button (3), Link (1), Alert | PDF download, Google Calendar, Outlook Calendar buttons, success alert |
| SCR-007 | Booking Error | Alert, Button (2), Link (1) | Error alert with retry button, "Select another slot" link |
| SCR-008 | My Appointments | Card (N), Badge (status), Button, ListItem | Appointment cards with date/time, provider, status badge (confirmed/watchlist) |
| SCR-009 | Preferred Slot Selection | DatePicker, Card, Button (2), Alert | Unavailable slot picker (disabled slots grayed out), watchlist registration alert |
| SCR-010 | Staff Dashboard | Card (4), Table, Badge, Button, Skeleton | Summary cards (walk-ins today, queue length, verification pending, critical conflicts), same-day queue table |
| SCR-011 | Walk-In Booking | TextField (search), Select, Button (2), Autocomplete, Alert | Patient search autocomplete, inline patient creation form, book button |
| SCR-012 | Same-Day Queue | Table, Badge (status), IconButton (drag handle), Button | Drag-to-reorder rows, status badges (waiting/in-room/completed), real-time updates (WebSocket) |
| SCR-013 | Patient Arrival Marking | Checkbox, Button, Badge, Toast | Bulk check-in checkboxes, "Mark arrived" button, success toast |
| SCR-014 | Document Upload | FileUpload (drag-drop), ProgressBar (N), Button, Card, Alert | Drag-and-drop zone, multiple file upload, progress bars per file, file type validation alert |
| SCR-015 | Document List | Table, Badge (processing status), IconButton (delete), Skeleton | Document metadata table (filename, upload date, status), processing badges (processing/completed/manual-review) |
| SCR-016 | Patient Chart Review | Table, Badge, Button, Card, Skeleton | Verification queue table (patient name, date, pending items count), select button |
| SCR-017 | 360-Degree Patient View | Tabs, Card (fact categories), Badge (confidence), IconButton (citation drill-down), Drawer (source view), Tooltip | Tabbed fact categories (vitals, medications, history, diagnoses, procedures), fact cards with confidence badges, click-to-cite iconbutton |
| SCR-018 | Conflict Resolution | Card (conflict), Radio (resolution options), TextField (justification), Button (2), Alert | Conflict cards with red warning badge, radio buttons (accept source A/B, manual override), justification text field |
| SCR-019 | Code Verification | Card (code suggestion), Badge (ICD-10/CPT), Chip (evidence facts), Button (3), Breadcrumbs (evidence trail), Tooltip | Code cards with evidence breadcrumbs (click to navigate to source), accept/reject/modify buttons |
| SCR-020 | Verification Complete | Alert, Button, Badge | Success alert "Patient chart verified", next patient button, timer badge (chart prep time) |
| SCR-021 | User Management | Table, TextField (search/filter), Button (2), Badge (role), Dropdown (bulk actions) | Sortable, filterable user table (name, email, role, status), create user button |
| SCR-022 | Create/Edit User | Modal, TextField (4), Select (role), Button (2), Alert | Modal form (name, email, role dropdown, permissions), save/cancel buttons |
| SCR-023 | Role Assignment | Modal, Select (role), Checkbox (permissions), Button (2), Tooltip | Role dropdown (patient/staff/admin), permissions bitfield checkboxes, tooltips explaining permissions |
| SCR-024 | Login | TextField (2), Button (2), Link (1), Alert, Checkbox (remember me) | Email/password fields, login button, "Forgot password?" link, remember me checkbox |
| SCR-025 | Header/Navigation | Header, Sidebar (desktop), BottomNav (mobile), Avatar, Badge (notifications), Dropdown (user menu) | Responsive navigation shell, logo, user avatar dropdown (profile/settings/logout) |
| SCR-026 | User Profile | Card, TextField (N), Button (2), Avatar, FileUpload (profile image) | Profile card with avatar, editable fields (name, email, phone), save/cancel buttons |
| SCR-027 | Settings | Tabs, Toggle (N), Select (N), Button (2), Alert | Tabbed settings (account, notifications, preferences), toggles for notification preferences |
| SCR-028 | Operational Metrics Dashboard | Card (4), BarChart, LineChart, PieChart, Gauge, DatePicker (range filter), Tabs, Badge, Skeleton | Metric cards (bookings, no-shows, AI agreement, conflicts), charts with drill-down, date range filter |

### Component Summary
| Category | Components | Variants |
|----------|------------|----------|
| Actions | Button | Primary, Secondary, Tertiary, Ghost × S/M/L × States (default, hover, focus, active, disabled, loading) |
| Actions | IconButton | Primary, Secondary, Default × S/M/L × States |
| Actions | Link | Primary, Secondary × States |
| Actions | FAB | Primary × M/L × States |
| Inputs | TextField | Outlined, Filled × S/M × States (default, focus, error, disabled, filled) |
| Inputs | Select | Outlined, Filled × S/M × States |
| Inputs | Checkbox | Default × S/M × States (unchecked, checked, indeterminate, disabled) |
| Inputs | Radio | Default × S/M × States (unchecked, checked, disabled) |
| Inputs | Toggle | Default × S/M × States (off, on, disabled) |
| Inputs | DatePicker | Outlined × M × States |
| Inputs | FileUpload | Drag-drop zone × M × States (idle, hover, uploading, error, success) |
| Navigation | Header | Desktop, Mobile × Default |
| Navigation | Sidebar | Desktop (persistent, collapsible) × Default |
| Navigation | BottomNav | Mobile × Default |
| Navigation | Tabs | Horizontal, Vertical × Default, Active |
| Navigation | Breadcrumbs | Default |
| Content | Card | Default, Outlined, Interactive × Elevation |
| Content | ListItem | Default, Interactive × States |
| Content | Table | Default, Sortable, Filterable, Paginated |
| Content | Avatar | Image, Initials, Icon × S/M/L/XL × Circle |
| Content | Badge | Primary, Secondary, Success, Warning, Error, Info, Neutral × S/M |
| Content | StatusIndicator | Dot, Label × Success, Warning, Error, Info |
| Content | ProgressBar | Linear, Circular × S/M/L |
| Content | Skeleton | Rectangle, Circle, Text × Animation |
| Feedback | Modal | S/M/L × Elevation 3 |
| Feedback | Drawer | Left, Right, Top, Bottom × Elevation 3 |
| Feedback | Toast | Success, Warning, Error, Info × Bottom-center/Bottom-left |
| Feedback | Alert | Success, Warning, Error, Info × Filled, Outlined |
| Feedback | Dialog | Confirmation, Destructive × Elevation 3 |
| Feedback | Tooltip | Default × Dark background |
| Data Viz | BarChart | Vertical, Horizontal × Default |
| Data Viz | LineChart | Single, Multi-series × Default |
| Data Viz | PieChart | Default, Donut × Default |
| Data Viz | Gauge | Semi-circle, Full-circle × Default |

### Component Constraints
- Use only components from Material-UI (MUI) v5 library
- Custom components must extend MUI components and follow theming system
- All components must support all defined states (Default, Hover, Focus, Active, Disabled, Loading)
- Follow Figma naming convention: `C/<Category>/<Name>` (e.g., `C/Actions/Button`, `C/Inputs/TextField`)
- Ensure WCAG 2.2 AA compliance for all components (color contrast, focus indicators, touch targets)

---

## 11. Prototype Flows

*Flows derived from use cases in spec.md. Each flow notes which personas it covers.*

### Flow: FL-001 - Patient Appointment Booking
**Flow ID**: FL-001
**Derived From**: UC-001
**Personas Covered**: Patient
**Description**: Complete patient self-service booking flow from slot search to confirmation

#### Flow Sequence
```text
1. Entry: SCR-024 Login / Default (OR Guest Mode)
   - Trigger: User clicks "Book Appointment" from landing page
   |
   v
2. Step: SCR-001 Availability Search / Default
   - Action: User searches available slots by date/provider
   |
   v
3. Step: SCR-001 Availability Search / Loading
   - Action: System fetches available slots (Redis cache with 60s TTL)
   |
   v
4. Step: SCR-002 Slot Selection / Default
   - Action: User selects preferred slot from calendar view
   |
   v
5. Step: SCR-003 Patient Details Form / Default
   - Action: User enters email, name, DOB, phone, insurance (inline account creation)
   |
   v
6. Step: SCR-003 Patient Details Form / Validation
   - Action: System validates insurance soft validation (name + member ID)
   |
   v
7. Decision Point (Intake Mode):
   - Manual → SCR-004 Manual Intake Form / Default
   - Conversational → SCR-005 Conversational Intake / Default
   |
   v
8. Step: SCR-006 Booking Confirmation / Loading
   - Action: System creates appointment, generates PDF, sends reminders
   |
   v
9. Exit: SCR-006 Booking Confirmation / Default
   - Success feedback with PDF download, calendar sync options
```

#### Required Interactions
- Calendar date picker for slot search
- Slot selection click/tap with immediate visual feedback
- Form field validation inline (real-time for email format, on-blur for required fields)
- Mode switch toggle button (manual ↔ conversational) preserving entered answers
- PDF download button (opens in new tab or downloads file)
- Calendar sync buttons (Google Calendar OAuth, Outlook Calendar Microsoft Graph)

---

### Flow: FL-002 - Preferred Slot Swap Request
**Flow ID**: FL-002
**Derived From**: UC-002
**Personas Covered**: Patient
**Description**: Patient requests automatic swap to preferred unavailable slot

#### Flow Sequence
```text
1. Entry: SCR-024 Login / Default
   - Trigger: Authenticated patient user
   |
   v
2. Step: SCR-008 My Appointments / Default
   - Action: User views booked appointments
   |
   v
3. Step: SCR-009 Preferred Slot Selection / Default
   - Action: User selects one preferred unavailable slot from calendar
   |
   v
4. Step: SCR-009 Preferred Slot Selection / Loading
   - Action: System registers watchlist entry
   |
   v
5. Exit: SCR-008 My Appointments / Default
   - Status badge shows "Watchlist: [Preferred Date/Time]"
```

#### Required Interactions
- Appointment card click to expand details
- "Request Preferred Slot" button (only appears if appointment confirmed)
- Calendar date picker for preferred slot (disables available slots, highlights unavailable)
- Watchlist status indicator badge with tooltip explaining automatic swap process

---

### Flow: FL-003 - Staff Walk-In Management
**Flow ID**: FL-003
**Derived From**: UC-003
**Personas Covered**: Staff
**Description**: Staff books walk-in, manages queue, marks arrival

#### Flow Sequence
```text
1. Entry: SCR-024 Login / Default
   - Trigger: Staff user authenticated
   |
   v
2. Step: SCR-010 Staff Dashboard / Default
   - Action: Staff views same-day operations overview
   |
   v
3. Step: SCR-011 Walk-In Booking / Default
   - Action: Staff searches existing patient OR creates minimal profile
   |
   v
4. Step: SCR-011 Walk-In Booking / Validation
   - Action: System validates patient data, books same-day slot or queue
   |
   v
5. Step: SCR-012 Same-Day Queue / Default
   - Action: Queue auto-updates with new walk-in patient (Redis cached, 30s TTL)
   |
   v
6. Step: SCR-013 Patient Arrival Marking / Default
   - Action: Staff clicks "Mark Arrived" checkbox when patient present
   |
   v
7. Exit: SCR-012 Same-Day Queue / Default
   - Patient status updated, queue position recalculated
```

#### Required Interactions
- Patient search autocomplete (debounced, min 2 characters)
- Create patient inline form (expand on "Create new patient" button)
- Drag-to-reorder queue rows (touch/mouse drag handles)
- Bulk arrival check-in (checkbox column + "Mark selected as arrived" button)
- Real-time queue updates (WebSocket connection for live staff collaboration)

---

### Flow: FL-004 - Clinical Document Upload
**Flow ID**: FL-004
**Derived From**: UC-004
**Personas Covered**: Patient, Staff
**Description**: Upload and track processing status of clinical documents

#### Flow Sequence
```text
1. Entry: SCR-024 Login / Default (Patient) OR SCR-010 Staff Dashboard (Staff)
   - Trigger: User navigates to "Upload Documents" (patient nav) or "Upload Clinical Documents" (staff dashboard)
   |
   v
2. Step: SCR-014 Document Upload / Default
   - Action: User drags files or clicks file picker (PDF format only, max 25MB)
   |
   v
3. Step: SCR-014 Document Upload / Loading
   - Action: System uploads file, virus scans (optional), creates ClinicalDocument record
   |
   v
4. Step: SCR-014 Document Upload / Validation (if file invalid)
   - Action: System shows error "Only PDF files under 25MB accepted"
   |
   v
5. Step: SCR-015 Document List / Loading
   - Action: System queues extraction job (Hangfire background processing)
   |
   v
6. Exit: SCR-015 Document List / Default
   - Documents listed with processing status badges ("Processing" yellow, "Completed" green, "Manual Review" orange)
```

#### Required Interactions
- Drag-and-drop file zone (highlight border on drag-over)
- Multiple file selection (file input allows multiple, upload progress bars stacked)
- Upload progress bars per file (linear progress with percentage)
- Processing status polling (poll every 5 seconds for status updates)
- Delete document action (IconButton with confirmation dialog)

---

### Flow: FL-005 - Staff Clinical Verification
**Flow ID**: FL-005
**Derived From**: UC-005
**Personas Covered**: Staff
**Description**: Verify extracted data, resolve conflicts, confirm codes (2-minute target)

#### Flow Sequence
```text
1. Entry: SCR-024 Login / Default
   - Trigger: Staff navigates to patient chart review queue
   |
   v
2. Step: SCR-016 Patient Chart Review / Default
   - Action: Staff selects patient from verification queue (sorted by priority: critical conflicts first)
   |
   v
3. Step: SCR-017 360-Degree Patient View / Default
   - Action: Staff reviews consolidated facts by category (vitals, medications, history, diagnoses, procedures)
   |
   v
4. Step: SCR-017 360-Degree Patient View / Default (source citation drill-down)
   - Action: Staff clicks extracted fact IconButton → Drawer slides from right with source document segment highlighted
   |
   v
5. Step: SCR-018 Conflict Resolution / Default
   - Action: System highlights conflicts (red badge), staff resolves each (radio: accept source A, accept source B, manual override)
   |
   v
6. Step: SCR-019 Code Verification / Default
   - Action: Staff reviews ICD-10/CPT suggestions with evidence fact breadcrumbs (click to navigate to source)
   |
   v
7. Step: SCR-019 Code Verification / Validation (if staff modifies codes)
   - Action: Staff clicks "Modify" → edits code → enters justification text → saves
   |
   v
8. Step: SCR-020 Verification Complete / Default
   - Action: System marks PatientView360.verification_status = 'verified', displays timer badge (e.g., "1:47 chart prep time"), audit logs
   |
   v
9. Exit: SCR-016 Patient Chart Review / Default
   - Patient removed from verification queue, next patient auto-loaded (or empty state if queue clear)
```

#### Required Interactions
- Tabbed fact categories (Vitals, Medications, History, Diagnoses, Procedures) with color-coded indicators
- Click-to-highlight source citation (IconButton on each fact → opens Drawer with PDF viewer scrolled to segment)
- Conflict resolution radio buttons (accept A, accept B, override) with required justification text field for override
- Code accept/reject/modify actions (buttons with state transitions)
- Timer display for 2-minute target (visible countdown, turns orange if >2 minutes)
- Keyboard shortcuts for rapid review (→ next fact, ← previous fact, Enter to accept, Esc to open citation)

---

### Flow: FL-006 - Admin User Management
**Flow ID**: FL-006
**Derived From**: UC-006
**Personas Covered**: Admin
**Description**: Create, update, disable users; assign roles

#### Flow Sequence
```text
1. Entry: SCR-024 Login / Default
   - Trigger: Admin user authenticated
   |
   v
2. Step: SCR-021 User Management / Default
   - Action: Admin views user list table (sortable by name/email/role/status, filterable by role/status)
   |
   v
3. Decision Point:
   - Create User → SCR-022 Create/Edit User / Default (empty form)
   - Edit Existing → SCR-022 Create/Edit User / Default (pre-populated form)
   - Disable User → SCR-021 User Management (inline toggle action, confirmation dialog)
   |
   v
4. Step: SCR-022 Create/Edit User / Validation
   - Action: System validates email format, role combinations (reject patient+staff), required fields
   |
   v
5. Step: SCR-023 Role Assignment / Default
   - Action: Admin selects role (patient/staff/admin dropdown), configures permissions bitfield (checkboxes)
   |
   v
6. Step: SCR-023 Role Assignment / Validation
   - Action: System enforces privilege escalation checks (admin cannot remove own admin role)
   |
   v
7. Exit: SCR-021 User Management / Default
   - User list updated, active sessions invalidated if role downgraded (force re-authentication)
```

#### Required Interactions
- User search/filter (text input filters name/email, dropdown filters role/status)
- Sortable table columns (click column header to sort ascending/descending)
- Create user modal (opens on "Create User" button, modal with form)
- Role dropdown multi-select (single role enforced, validation prevents invalid combinations)
- Permissions bitfield checkboxes (e.g., "Can book walk-ins", "Can verify charts", "Can manage users")
- Disable account toggle (inline IconButton with confirmation dialog "Disable user [name]? They will be logged out immediately.")
- Audit log sidebar (IconButton per user row opens drawer with audit entries for that user)

---

### Flow: FL-007 - Operational Metrics Review
**Flow ID**: FL-007
**Derived From**: FR-018 (Operational Metrics)
**Personas Covered**: Staff, Admin
**Description**: View platform performance metrics and AI agreement rates

#### Flow Sequence
```text
1. Entry: SCR-024 Login / Default
   - Trigger: Staff/admin navigates to "Metrics Dashboard" (sidebar navigation)
   |
   v
2. Step: SCR-028 Operational Metrics Dashboard / Loading
   - Action: System aggregates metrics from database (booking volumes, no-show outcomes, AI agreement rate, conflict counts)
   |
   v
3. Step: SCR-028 Operational Metrics Dashboard / Default
   - Action: User views summary cards + charts (booking trends line chart, no-show risk pie chart, AI agreement gauge, conflicts bar chart)
   |
   v
4. Exit: SCR-028 Operational Metrics Dashboard / Default
   - Drill-down: Click chart element → filters detail view, click "Export CSV" → downloads metrics report
```

#### Required Interactions
- Date range filter (DatePicker for start/end dates, preset buttons: "Last 7 days", "Last 30 days", "This month")
- Metric category tabs (Bookings, AI Performance, Clinical Quality)
- Chart drill-down click (click bar/pie slice → opens modal with detail table)
- Export CSV button (downloads CSV file with current filter/date range applied)
- Refresh button (manually refresh metrics without page reload)

---

## 12. Export Requirements

### JPG Export Settings
| Setting | Value |
|---------|-------|
| Format | JPG |
| Quality | High (85%) |
| Scale - Mobile | 2x (390x844 logical → 780x1688 export for iPhone 12 Pro) |
| Scale - Web | 2x (1440x900 logical → 2880x1800 export for desktop) |
| Color Profile | sRGB |

### Export Naming Convention
`PropelIQ-Healthcare__<Platform>__<ScreenName>__<State>__v1.jpg`

**Examples:**
- `PropelIQ-Healthcare__Mobile__AvailabilitySearch__Default__v1.jpg`
- `PropelIQ-Healthcare__Web__StaffDashboard__Loading__v1.jpg`
- `PropelIQ-Healthcare__Tablet__360PatientView__Default__v1.jpg`

### Export Manifest (Sample - Full manifest TBD)
| Screen | State | Platform | Filename |
|--------|-------|----------|----------|
| SCR-001 | Default | Mobile | PropelIQ-Healthcare__Mobile__AvailabilitySearch__Default__v1.jpg |
| SCR-001 | Loading | Mobile | PropelIQ-Healthcare__Mobile__AvailabilitySearch__Loading__v1.jpg |
| SCR-001 | Empty | Mobile | PropelIQ-Healthcare__Mobile__AvailabilitySearch__Empty__v1.jpg |
| SCR-001 | Error | Mobile | PropelIQ-Healthcare__Mobile__AvailabilitySearch__Error__v1.jpg |
| SCR-001 | Default | Desktop | PropelIQ-Healthcare__Web__AvailabilitySearch__Default__v1.jpg |
| SCR-024 | Default | Mobile | PropelIQ-Healthcare__Mobile__Login__Default__v1.jpg |
| SCR-024 | Error | Mobile | PropelIQ-Healthcare__Mobile__Login__Error__v1.jpg |
| SCR-024 | Default | Desktop | PropelIQ-Healthcare__Web__Login__Default__v1.jpg |

### Total Export Count (Estimated)
- **Screens**: 28 screens
- **States per screen (average)**: 3.5 states (not all screens have all 5 states)
- **Platforms**: Mobile (primary) + Desktop (secondary) = 2x
- **Total JPGs**: 28 × 3.5 × 2 ≈ **196 JPG exports**

---

## 13. Figma File Structure

### Page Organization
```text
PropelIQ Healthcare Figma File
+-- 00_Cover
|   +-- Project info (title, version 1.0, stakeholders: Product, Design, Engineering)
|   +-- Changelog (version history)
+-- 01_Foundations
|   +-- Color tokens (Primary, Secondary, Semantic, Neutral, Healthcare-specific)
|   +-- Color tokens - Light mode (default)
|   +-- Color tokens - Dark mode (future phase)
|   +-- Typography scale (H1-H6, Body, Button, Caption, Overline)
|   +-- Spacing scale (8px grid, spacing(1) to spacing(10))
|   +-- Radius tokens (none, small, medium, large, full)
|   +-- Elevation/shadows (0-5 levels)
|   +-- Grid definitions (12-column responsive grid, breakpoints)
+-- 02_Components
|   +-- C/Actions/Button (Primary, Secondary, Tertiary, Ghost × S/M/L × States)
|   +-- C/Actions/IconButton (Primary, Secondary, Default × S/M/L × States)
|   +-- C/Actions/Link (Primary, Secondary × States)
|   +-- C/Actions/FAB (Primary × M/L × States)
|   +-- C/Inputs/TextField (Outlined, Filled × S/M × States)
|   +-- C/Inputs/Select (Outlined, Filled × S/M × States)
|   +-- C/Inputs/Checkbox (Default × S/M × States)
|   +-- C/Inputs/Radio (Default × S/M × States)
|   +-- C/Inputs/Toggle (Default × S/M × States)
|   +-- C/Inputs/DatePicker (Outlined × M × States)
|   +-- C/Inputs/FileUpload (Drag-drop × M × States)
|   +-- C/Navigation/Header (Desktop, Mobile)
|   +-- C/Navigation/Sidebar (Desktop persistent, Desktop collapsible)
|   +-- C/Navigation/BottomNav (Mobile)
|   +-- C/Navigation/Tabs (Horizontal, Vertical)
|   +-- C/Navigation/Breadcrumbs
|   +-- C/Content/Card (Default, Outlined, Interactive)
|   +-- C/Content/ListItem (Default, Interactive)
|   +-- C/Content/Table (Default, Sortable, Filterable, Paginated)
|   +-- C/Content/Avatar (Image, Initials, Icon × S/M/L/XL)
|   +-- C/Content/Badge (Primary, Secondary, Success, Warning, Error, Info, Neutral)
|   +-- C/Content/StatusIndicator (Dot, Label)
|   +-- C/Content/ProgressBar (Linear, Circular)
|   +-- C/Content/Skeleton (Rectangle, Circle, Text)
|   +-- C/Feedback/Modal (S, M, L)
|   +-- C/Feedback/Drawer (Left, Right, Top, Bottom)
|   +-- C/Feedback/Toast (Success, Warning, Error, Info)
|   +-- C/Feedback/Alert (Success, Warning, Error, Info × Filled, Outlined)
|   +-- C/Feedback/Dialog (Confirmation, Destructive)
|   +-- C/Feedback/Tooltip
|   +-- C/DataViz/BarChart (Vertical, Horizontal)
|   +-- C/DataViz/LineChart (Single, Multi-series)
|   +-- C/DataViz/PieChart (Default, Donut)
|   +-- C/DataViz/Gauge (Semi-circle, Full-circle)
+-- 03_Patterns
|   +-- Auth form pattern (Login, Forgot Password)
|   +-- Search + filter pattern (User Management, Patient Chart Review)
|   +-- Detail page pattern (360-Degree Patient View)
|   +-- Error/Empty/Loading state patterns (Reusable state templates)
|   +-- Multi-step form pattern (Booking flow, Intake flow)
+-- 04_Screens-Patient
|   +-- SCR-001_AvailabilitySearch/Default
|   +-- SCR-001_AvailabilitySearch/Loading
|   +-- SCR-001_AvailabilitySearch/Empty
|   +-- SCR-001_AvailabilitySearch/Error
|   +-- SCR-002_SlotSelection/Default
|   +-- SCR-002_SlotSelection/Loading
|   +-- SCR-002_SlotSelection/Error
|   +-- SCR-003_PatientDetailsForm/Default
|   +-- SCR-003_PatientDetailsForm/Loading
|   +-- SCR-003_PatientDetailsForm/Error
|   +-- SCR-003_PatientDetailsForm/Validation
|   +-- SCR-004_ManualIntakeForm/Default
|   +-- SCR-004_ManualIntakeForm/Loading
|   +-- SCR-004_ManualIntakeForm/Error
|   +-- SCR-004_ManualIntakeForm/Validation
|   +-- SCR-005_ConversationalIntake/Default
|   +-- SCR-005_ConversationalIntake/Loading
|   +-- SCR-005_ConversationalIntake/Empty
|   +-- SCR-005_ConversationalIntake/Error
|   +-- SCR-005_ConversationalIntake/Validation
|   +-- SCR-006_BookingConfirmation/Default
|   +-- SCR-006_BookingConfirmation/Loading
|   +-- SCR-006_BookingConfirmation/Error
|   +-- SCR-007_BookingError/Error
|   +-- SCR-008_MyAppointments/Default
|   +-- SCR-008_MyAppointments/Loading
|   +-- SCR-008_MyAppointments/Empty
|   +-- SCR-008_MyAppointments/Error
|   +-- SCR-009_PreferredSlotSelection/Default
|   +-- SCR-009_PreferredSlotSelection/Loading
|   +-- SCR-009_PreferredSlotSelection/Empty
|   +-- SCR-009_PreferredSlotSelection/Error
|   +-- SCR-014_DocumentUpload/Default
|   +-- SCR-014_DocumentUpload/Loading
|   +-- SCR-014_DocumentUpload/Error
|   +-- SCR-014_DocumentUpload/Validation
|   +-- SCR-015_DocumentList/Default
|   +-- SCR-015_DocumentList/Loading
|   +-- SCR-015_DocumentList/Empty
|   +-- SCR-015_DocumentList/Error
+-- 05_Screens-Staff
|   +-- SCR-010_StaffDashboard/Default
|   +-- SCR-010_StaffDashboard/Loading
|   +-- SCR-010_StaffDashboard/Empty
|   +-- SCR-010_StaffDashboard/Error
|   +-- SCR-011_WalkInBooking/Default
|   +-- SCR-011_WalkInBooking/Loading
|   +-- SCR-011_WalkInBooking/Error
|   +-- SCR-011_WalkInBooking/Validation
|   +-- SCR-012_SameDayQueue/Default
|   +-- SCR-012_SameDayQueue/Loading
|   +-- SCR-012_SameDayQueue/Empty
|   +-- SCR-012_SameDayQueue/Error
|   +-- SCR-013_PatientArrivalMarking/Default
|   +-- SCR-013_PatientArrivalMarking/Loading
|   +-- SCR-013_PatientArrivalMarking/Error
|   +-- SCR-016_PatientChartReview/Default
|   +-- SCR-016_PatientChartReview/Loading
|   +-- SCR-016_PatientChartReview/Empty
|   +-- SCR-016_PatientChartReview/Error
|   +-- SCR-017_360PatientView/Default
|   +-- SCR-017_360PatientView/Loading
|   +-- SCR-017_360PatientView/Empty
|   +-- SCR-017_360PatientView/Error
|   +-- SCR-018_ConflictResolution/Default
|   +-- SCR-018_ConflictResolution/Loading
|   +-- SCR-018_ConflictResolution/Error
|   +-- SCR-018_ConflictResolution/Validation
|   +-- SCR-019_CodeVerification/Default
|   +-- SCR-019_CodeVerification/Loading
|   +-- SCR-019_CodeVerification/Empty
|   +-- SCR-019_CodeVerification/Error
|   +-- SCR-020_VerificationComplete/Default
|   +-- SCR-028_OperationalMetrics/Default
|   +-- SCR-028_OperationalMetrics/Loading
|   +-- SCR-028_OperationalMetrics/Empty
|   +-- SCR-028_OperationalMetrics/Error
+-- 06_Screens-Admin
|   +-- SCR-021_UserManagement/Default
|   +-- SCR-021_UserManagement/Loading
|   +-- SCR-021_UserManagement/Empty
|   +-- SCR-021_UserManagement/Error
|   +-- SCR-022_CreateEditUser/Default
|   +-- SCR-022_CreateEditUser/Loading
|   +-- SCR-022_CreateEditUser/Error
|   +-- SCR-022_CreateEditUser/Validation
|   +-- SCR-023_RoleAssignment/Default
|   +-- SCR-023_RoleAssignment/Loading
|   +-- SCR-023_RoleAssignment/Error
|   +-- SCR-023_RoleAssignment/Validation
|   +-- SCR-028_OperationalMetrics/Default (shared with Staff)
+-- 07_Screens-Shared
|   +-- SCR-024_Login/Default
|   +-- SCR-024_Login/Loading
|   +-- SCR-024_Login/Error
|   +-- SCR-024_Login/Validation
|   +-- SCR-025_HeaderNavigation/Default
|   +-- SCR-026_UserProfile/Default
|   +-- SCR-026_UserProfile/Loading
|   +-- SCR-026_UserProfile/Error
|   +-- SCR-026_UserProfile/Validation
|   +-- SCR-027_Settings/Default
|   +-- SCR-027_Settings/Loading
|   +-- SCR-027_Settings/Error
|   +-- SCR-027_Settings/Validation
+-- 08_Prototype-Flows
|   +-- FL-001: Patient Booking Flow (wired frames)
|   +-- FL-002: Preferred Slot Swap (wired frames)
|   +-- FL-003: Staff Walk-In Management (wired frames)
|   +-- FL-004: Clinical Document Upload (wired frames)
|   +-- FL-005: Staff Clinical Verification (wired frames)
|   +-- FL-006: Admin User Management (wired frames)
|   +-- FL-007: Operational Metrics Review (wired frames)
+-- 09_Handoff
    +-- Design-to-Dev Specs (token usage rules, component implementation notes)
    +-- Responsive Specifications (breakpoint behaviors, mobile/desktop differences)
    +-- Edge Cases Documentation (long text, missing data, error states)
    +-- Accessibility Notes (WCAG 2.2 AA validation checklist, focus order)
    +-- Animation Specifications (transition durations, easing functions)
```

---

## 14. Quality Checklist

### Pre-Export Validation
- [ ] All P0/P1 screens have required states (Default + applicable Loading/Empty/Error/Validation)
- [ ] All components use design tokens from designsystem.md (no hard-coded hex colors, no arbitrary spacing values)
- [ ] Color contrast meets WCAG AA (>= 4.5:1 text, >= 3:1 UI components) - validated with WebAIM Contrast Checker
- [ ] Focus states defined for all interactive elements (2px solid primary.500 outline, 2px offset)
- [ ] Touch targets >= 44x44px on mobile/tablet screens (buttons, IconButtons, checkboxes, radio, table row actions)
- [ ] Prototype flows wired and functional (FL-001 through FL-007 all clickable in Figma prototype mode)
- [ ] Naming conventions followed (screens: SCR-XXX_ScreenName/State, components: C/Category/Name, flows: FL-XXX)
- [ ] Export manifest complete for P0 screens (Login, Booking flow, Staff Dashboard, 360-View, Code Verification)

### Post-Generation
- [ ] designsystem.md updated with Figma component references (sync Figma component IDs with designsystem.md specs)
- [ ] Export manifest generated (CSV list of all JPG exports with file paths)
- [ ] JPG files named correctly according to convention (PropelIQ-Healthcare__Platform__ScreenName__State__v1.jpg)
- [ ] Handoff documentation complete (09_Handoff page in Figma with dev specs, accessibility notes, edge cases)
- [ ] UXR-XXX requirements mapped to screens (all 22 UXR requirements validated across 28 screens)
- [ ] Use case coverage validated (all 6 UC-XXX covered by flows, no orphan screens)
- [ ] Persona coverage validated (Patient has 11 primary screens, Staff has 13 primary screens, Admin has 4 primary screens)

---

## Version History
| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2024 | Initial Figma specification for Phase 1 MVP - 28 screens, 7 flows, 22 UXR requirements |

---

**End of Figma Design Specification**
