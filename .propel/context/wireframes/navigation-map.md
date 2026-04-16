# Navigation Map - Unified Patient Access & Clinical Intelligence Platform

## Source Reference
| Document | Path | Purpose |
|---------|------|---------|
| Figma Specification | `.propel/context/docs/figma_spec.md` | Prototype flows (FL-001 to FL-007) |

---

## 1. Navigation Overview

**Total Screens**: 28
**Total Flows**: 7 user flows
**Navigation Patterns**: Role-based routing, flow-based sequences, modal overlays
**Wireframe Linking**: All interactive elements wired per FL-XXX flow sequences

---

## 2. Screen-to-Screen Navigation Matrix

### 2.1 Authentication & Entry Points

| From Screen | To Screen | Trigger Element | Condition |
|-------------|-----------|-----------------|-----------|
| (External) | SCR-024 (Login) | Direct URL / Unauthenticated access | Always |
| SCR-024 (Login) | SCR-001 (Availability Search) | #login-btn → Submit | Role = Patient |
| SCR-024 (Login) | SCR-010 (Staff Dashboard) | #login-btn → Submit | Role = Staff |
| SCR-024 (Login) | SCR-021 (User Management) | #login-btn → Submit | Role = Admin |

### 2.2 Patient Portal Navigation (FL-001, FL-002, FL-004)

#### FL-001: Patient Appointment Booking
| From Screen | To Screen | Trigger Element | Notes |
|-------------|-----------|-----------------|-------|
| SCR-001 (Availability Search) | SCR-002 (Slot Selection) | .slot-card → Click | Search results loaded |
| SCR-002 (Slot Selection) | SCR-003 (Patient Details Form) | #select-slot-btn → Click | Slot confirmed |
| SCR-003 (Patient Details Form) | SCR-004 (Manual Intake Form) | #mode-manual-btn → Click | Patient chooses manual intake |
| SCR-003 (Patient Details Form) | SCR-005 (Conversational Intake) | #mode-conversational-btn → Click (default) | Patient chooses AI intake |
| SCR-004/SCR-005 | SCR-006 (Booking Confirmation) | #submit-intake-btn → Click | Intake completed |
| SCR-002 (Slot Selection) | SCR-007 (Booking Error) | Concurrent booking conflict | 409 Conflict API response |
| SCR-007 (Booking Error) | SCR-001 (Availability Search) | #retry-btn → Click | User retries booking |

#### FL-002: Preferred Slot Swap Request
| From Screen | To Screen | Trigger Element | Notes |
|-------------|-----------|-----------------|-------|
| SCR-008 (My Appointments) | SCR-009 (Preferred Slot Selection) | #request-preferred-btn → Click | From appointment card |
| SCR-009 (Preferred Slot Selection) | SCR-008 (My Appointments) | #register-watchlist-btn → Click | Watchlist registered |

#### FL-004: Clinical Document Upload (Patient)
| From Screen | To Screen | Trigger Element | Notes |
|-------------|-----------|-----------------|-------|
| SCR-014 (Document Upload) | SCR-015 (Document List) | File upload complete + #view-documents-btn | Upload success |

#### Patient Utility Navigation
| From Screen | To Screen | Trigger Element | Notes |
|-------------|-----------|-----------------|-------|
| Any Patient Screen | SCR-001 (Availability Search) | #nav-book (Bottom nav) | Mobile primary |
| Any Patient Screen | SCR-008 (My Appointments) | #nav-appointments (Bottom nav) | Mobile primary |
| Any Patient Screen | SCR-015 (Document List) | #nav-documents (Bottom nav) | Mobile primary |
| Any Patient Screen | SCR-026 (User Profile) | #nav-profile (Bottom nav) OR Avatar dropdown → Profile | Mobile/Desktop |
| Any Patient Screen | SCR-027 (Settings) | Avatar dropdown → Settings | Desktop only |
| Any Patient Screen | SCR-024 (Login) | Avatar dropdown → Logout | Logout action |

### 2.3 Staff Portal Navigation (FL-003, FL-004, FL-005, FL-007)

#### FL-003: Staff Walk-In Management
| From Screen | To Screen | Trigger Element | Notes |
|-------------|-----------|-----------------|-------|
| SCR-010 (Staff Dashboard) | SCR-011 (Walk-In Booking) | #nav-walkin (Sidebar) OR #book-walkin-btn | Staff navigation |
| SCR-011 (Walk-In Booking) | SCR-012 (Same-Day Queue) | #submit-booking-btn → Click | Walk-in booked |
| SCR-012 (Same-Day Queue) | SCR-013 (Patient Arrival Marking) | .btn-mark-arrived → Click | Opens arrival confirmation |
| SCR-013 (Patient Arrival Marking) | SCR-012 (Same-Day Queue) | #confirm-arrival-btn → Click | Queue updated |

#### FL-004: Clinical Document Upload (Staff)
| From Screen | To Screen | Trigger Element | Notes |
|-------------|-----------|-----------------|-------|
| SCR-010 (Staff Dashboard) | SCR-014 (Document Upload) | #upload-clinical-docs-btn | Staff-initiated |
| SCR-014 (Document Upload) | SCR-015 (Document List) | File upload complete + #view-documents-btn | Upload success |

#### FL-005: Staff Clinical Verification
| From Screen | To Screen | Trigger Element | Notes |
|-------------|-----------|-----------------|-------|
| SCR-010 (Staff Dashboard) | SCR-016 (Patient Chart Review) | #nav-verify (Sidebar) OR #verify-charts-btn | Verification queue entry |
| SCR-016 (Patient Chart Review) | SCR-017 (360-Degree Patient View) | .btn-select-patient → Click | From verification queue table |
| SCR-017 (360-Degree Patient View) | SCR-018 (Conflict Resolution) | .conflict-badge → Click | If conflicts detected |
| SCR-017/SCR-018 | SCR-019 (Code Verification) | #continue-to-codes-btn → Click | Conflict resolution complete OR no conflicts |
| SCR-019 (Code Verification) | SCR-020 (Verification Complete) | #confirm-codes-btn → Click | All codes confirmed |
| SCR-020 (Verification Complete) | SCR-016 (Patient Chart Review) | #next-patient-btn → Click | Auto-load next patient OR return to queue |

#### FL-005 Sub-flow: Source Citation Drill-Down
| From Screen | To Screen | Trigger Element | Notes |
|-------------|-----------|-----------------|-------|
| SCR-017 (360-Degree Patient View) | Source Citation Drawer (overlay) | .citation-icon-btn → Click | Drawer slides from right |
| SCR-019 (Code Verification) | Source Citation Drawer (overlay) | .evidence-fact-breadcrumb → Click | Shows source segment |

#### FL-007: Operational Metrics Review (Staff)
| From Screen | To Screen | Trigger Element | Notes |
|-------------|-----------|-----------------|-------|
| SCR-010 (Staff Dashboard) | SCR-028 (Operational Metrics Dashboard) | #nav-metrics (Sidebar) | Staff navigation |

#### Staff Utility Navigation
| From Screen | To Screen | Trigger Element | Notes |
|-------------|-----------|-----------------|-------|
| Any Staff Screen | SCR-010 (Staff Dashboard) | #nav-dashboard (Sidebar) OR Bottom nav (mobile) | Primary navigation |
| Any Staff Screen | SCR-011 (Walk-In Booking) | #nav-walkin (Sidebar) | Sidebar nav |
| Any Staff Screen | SCR-012 (Same-Day Queue) | #nav-queue (Sidebar) | Sidebar nav |
| Any Staff Screen | SCR-016 (Patient Chart Review) | #nav-verify (Sidebar) | Sidebar nav |
| Any Staff Screen | SCR-028 (Operational Metrics Dashboard) | #nav-metrics (Sidebar) | Sidebar nav |
| Any Staff Screen | SCR-026 (User Profile) | Avatar dropdown → Profile | Desktop |
| Any Staff Screen | SCR-027 (Settings) | Avatar dropdown → Settings | Desktop |
| Any Staff Screen | SCR-024 (Login) | Avatar dropdown → Logout | Logout action |

### 2.4 Admin Portal Navigation (FL-006, FL-007)

#### FL-006: Admin User Management
| From Screen | To Screen | Trigger Element | Notes |
|-------------|-----------|-----------------|-------|
| SCR-021 (User Management) | SCR-022 (Create/Edit User) Modal | #create-user-btn → Click OR .edit-user-icon → Click | Modal overlay |
| SCR-022 (Create/Edit User) Modal | SCR-023 (Role Assignment) Modal | #assign-role-btn → Click | Chained modal flow |
| SCR-023 (Role Assignment) Modal | SCR-021 (User Management) | #save-btn → Click | Modal closes, table refreshes |

#### FL-007: Operational Metrics Review (Admin)
| From Screen | To Screen | Trigger Element | Notes |
|-------------|-----------|-----------------|-------|
| SCR-021 (User Management) | SCR-028 (Operational Metrics Dashboard) | #nav-metrics (Sidebar) | Admin navigation |

#### Admin Utility Navigation
| From Screen | To Screen | Trigger Element | Notes |
|-------------|-----------|-----------------|-------|
| Any Admin Screen | SCR-021 (User Management) | #nav-users (Sidebar) OR Bottom nav (mobile) | Primary navigation |
| Any Admin Screen | SCR-028 (Operational Metrics Dashboard) | #nav-metrics (Sidebar) | Sidebar nav |
| Any Admin Screen | SCR-026 (User Profile) | Avatar dropdown → Profile | Desktop |
| Any Admin Screen | SCR-027 (Settings) | Avatar dropdown → Settings | Desktop |
| Any Admin Screen | SCR-024 (Login) | Avatar dropdown → Logout | Logout action |

---

## 3. Modal/Overlay Navigation

### 3.1 Modal Overlays (Do Not Navigate Away from Parent Screen)
| Modal Name | Parent Screen(s) | Trigger | Dismissal Actions |
|------------|------------------|---------|-------------------|
| Login Modal | All protected screens | Unauthenticated access | Login success, Cancel |
| Session Timeout Warning | All authenticated screens | 14-minute inactivity | Stay Logged In, Logout |
| Create/Edit User | SCR-021 (User Management) | Create/Edit button | Save, Cancel |
| Role Assignment | SCR-021, SCR-022 | Assign Role button | Save, Cancel |
| Booking Confirmation PDF Preview | SCR-006 (Booking Confirmation) | View PDF button | Close (X), Overlay click |
| Preferred Slot Calendar | SCR-009 (Preferred Slot Selection) | Select Preferred Slot button | Confirm, Cancel |
| Delete Document Confirmation | SCR-015 (Document List) | Delete icon button | Delete, Cancel |
| Disable User Confirmation | SCR-021 (User Management) | Disable toggle | Confirm, Cancel |

### 3.2 Drawer Overlays (Slide-in Panels)
| Drawer Name | Parent Screen(s) | Trigger | Dismissal Actions |
|-------------|------------------|---------|-------------------|
| Source Citation Drawer | SCR-017, SCR-019 | Citation icon button / Evidence breadcrumb click | Close (X), Overlay click, ESC key |
| Audit Log Drawer | SCR-021 (User Management) | Audit icon button per user row | Close (X), Overlay click |
| Filter Drawer (mobile) | SCR-028 (Operational Metrics Dashboard) | Filter button (mobile only) | Apply, Cancel |

---

## 4. Flow Coverage Report

| Flow ID | Flow Name | Start Screen | End Screen | Total Steps | Screens Covered | Navigation Wired |
|---------|-----------|--------------|------------|-------------|-----------------|------------------|
| FL-001 | Patient Appointment Booking | SCR-024 | SCR-006 | 9 | 7 screens | ✅ Yes |
| FL-002 | Preferred Slot Swap Request | SCR-024 | SCR-008 | 5 | 2 screens | ✅ Yes |
| FL-003 | Staff Walk-In Management | SCR-024 | SCR-012 | 7 | 4 screens | ✅ Yes |
| FL-004 | Clinical Document Upload | SCR-024/SCR-010 | SCR-015 | 6 | 2 screens | ✅ Yes |
| FL-005 | Staff Clinical Verification | SCR-024 | SCR-016 | 9 | 5 screens + 1 drawer | ✅ Yes |
| FL-006 | Admin User Management | SCR-024 | SCR-021 | 7 | 3 screens + 2 modals | ✅ Yes |
| FL-007 | Operational Metrics Review | SCR-024 | SCR-028 | 4 | 1 screen | ✅ Yes |

**Total Flow Coverage**: 7 of 7 flows (100%)
**Navigation Integrity**: All flows have complete navigation paths wired

---

## 5. Dead-End Screens (Intentional Exit Points)

| Screen | Type | Exit Path |
|--------|------|-----------|
| SCR-006 (Booking Confirmation) | Success terminal | User navigates back to SCR-008 (My Appointments) via bottom nav OR closes browser |
| SCR-007 (Booking Error) | Error terminal | User clicks "Retry" → SCR-001 OR "Select another slot" → SCR-002 |
| SCR-020 (Verification Complete) | Success terminal | User clicks "Next Patient" → SCR-016 OR navigates via sidebar to other screens |

**Note**: These screens are intentional workflow endpoints. They are NOT navigation dead-ends because:
1. Persistent navigation (sidebar/bottom nav) is always present
2. Explicit next-action buttons are provided (Retry, Next Patient)
3. Users can always navigate to other sections via primary navigation

---

## 6. Navigation Validation Checklist

### Screen Coverage
- [ ] All 28 screens appear in at least one navigation path
- [x] All P0 screens (18) have navigation wired
- [x] All P1 screens (6) have navigation wired
- [x] P2/P3 screens defined in map

### Flow Integrity
- [x] All FL-001 to FL-007 flows have complete navigation sequences
- [x] No broken links (missing target screens)
- [x] All interactive elements from figma_spec.md Section 11 (Flows) are wired
- [x] Decision points (e.g., manual vs conversational intake) have both paths wired

### Modal/Overlay Navigation
- [x] All modals have documented triggers and dismissal actions
- [x] All drawers have documented triggers and dismissal actions
- [x] Modals do not navigate away from parent screen (overlay pattern)
- [x] Drawer overlays are wired to correct parent screens

### Accessibility
- [x] All navigation links have proper ARIA labels
- [x] Keyboard navigation supported (Enter, ESC, Tab focus)
- [x] Screen reader announcements for navigation success
- [x] Focus management after navigation (focus moves to target screen heading)

---

## 7. Wireframe Navigation Implementation

### HTML Wireframe Linking Standard
Each wireframe includes navigation map comments:
```html
<!-- Navigation Map
| Element | Action | Target Screen |
|---------|--------|---------------|
| #element-id | click | SCR-XXX (Screen Name) |
-->
```

### Example: SCR-024 (Login) Navigation Map
```html
<!-- Navigation Map
| Element | Action | Target Screen |
|---------|--------|---------------|
| #login-btn | click | SCR-025 (Header/Navigation - role-dependent redirect to patient/staff/admin dashboard) |
| #forgot-password | click | [Password Reset - not in scope for Phase 1] |
-->
```

### Wireframe File Naming Convention
`wireframe-SCR-XXX-{screen-name}.html`

**Examples:**
- `wireframe-SCR-024-login.html`
- `wireframe-SCR-010-staff-dashboard.html`
- `wireframe-SCR-017-360-patient-view.html`

---

## 8. Cross-Reference with Figma Spec

All navigation paths in this map are derived from figma_spec.md Section 11 (Prototype Flows):
- **FL-001 through FL-007**: All flow sequences mapped to screen-to-screen navigation
- **Required Interactions**: All interactive elements from flows are documented as triggers
- **UXR-XXX Constraints**: Navigation depth (UXR-001: max 3 clicks) validated across all paths

**Divergences**: None - All navigation paths match figma_spec.md flow sequences exactly.

---

## 9. Integration with Information Architecture

This navigation map implements the site structure defined in [information-architecture.md](information-architecture.md):
- **Level 1-4 Hierarchy**: All navigation paths respect information hierarchy
- **Primary Paths**: Patient portal, Staff portal, Admin portal paths match site map structure
- **Entry Points**: All entry points from information-architecture.md Section 4 are wired

---

## Version History
| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2024 | Initial navigation map for Phase 1 MVP - 28 screens, 7 flows, complete navigation integrity |
