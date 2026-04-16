# Information Architecture - Unified Patient Access & Clinical Intelligence Platform

## Source Reference
| Document | Path | Purpose |
|----------|------|---------|
| Figma Specification | `.propel/context/docs/figma_spec.md` | Screen inventory, flows, components |

---

## 1. Executive Summary

**Platform**: Web (Responsive - Desktop, Tablet, Mobile)
**Total Screens**: 28 screens
**Primary User Personas**: Patient, Staff, Admin
**Navigation Pattern**: Role-based with persistent sidebar (desktop) / bottom navigation (mobile)

---

## 2. Site Map

```text
PropelIQ Healthcare Platform
│
├── Authentication (All Personas)
│   └── SCR-024: Login
│
├──Patient Portal (Patient Persona)
│   ├── Booking Flow
│   │   ├── SCR-001: Availability Search
│   │   ├── SCR-002: Slot Selection
│   │   ├── SCR-003: Patient Details Form (inline account creation)
│   │   ├── SCR-004: Manual Intake Form
│   │   ├── SCR-005: Conversational Intake (AI chat)
│   │   ├── SCR-006: Booking Confirmation
│   │   └── SCR-007: Booking Error
│   │
│   ├── Appointments Management
│   │   ├── SCR-008: My Appointments
│   │   └── SCR-009: Preferred Slot Selection (watchlist)
│   │
│   ├── Document Management
│   │   ├── SCR-014: Document Upload
│   │   └── SCR-015: Document List
│   │
│   └── Account Management
│       ├── SCR-026: User Profile
│       └── SCR-027: Settings
│
├── Staff Portal (Staff Persona)
│   ├── Operations Dashboard
│   │   ├── SCR-010: Staff Dashboard
│   │   ├── SCR-011: Walk-In Booking
│   │   ├── SCR-012: Same-Day Queue Management
│   │   └── SCR-013: Patient Arrival Marking
│   │
│   ├── Clinical Verification Workflow
│   │   ├── SCR-016: Patient Chart Review (verification queue)
│   │   ├── SCR-017: 360-Degree Patient View (with source citations)
│   │   ├── SCR-018: Conflict Resolution
│   │   ├── SCR-019: Code Verification (ICD-10/CPT suggestions)
│   │   └── SCR-020: Verification Complete
│   │
│   ├── Document Management (shared with Patient)
│   │   ├── SCR-014: Document Upload (staff-initiated)
│   │   └── SCR-015: Document List
│   │
│   ├── Metrics & Reporting
│   │   └── SCR-028: Operational Metrics Dashboard
│   │
│   └── Account Management
│       ├── SCR-026: User Profile
│       └── SCR-027: Settings
│
└── Admin Portal (Admin Persona)
    ├── User Management
    │   ├── SCR-021: User Management
    │   ├── SCR-022: Create/Edit User
    │   └── SCR-023: Role Assignment
    │
    ├── Metrics & Reporting (shared with Staff)
    │   └── SCR-028: Operational Metrics Dashboard
    │
    └── Account Management
        ├── SCR-026: User Profile
        └── SCR-027: Settings
```

---

## 3. Navigation Patterns

### Primary Navigation
| Pattern | Desktop | Tablet | Mobile | Screens Affected |
|---------|---------|--------|--------|------------------|
| Patient Portal | Top header + inline flow steps | Top header + inline flow steps | Bottom navigation (4 tabs) | SCR-001-009, SCR-014-015, SCR-026-027 |
| Staff Portal | Persistent sidebar (240px) | Collapsible sidebar | Bottom navigation (5 tabs) | SCR-010-020, SCR-028 |
| Admin Portal | Persistent sidebar (240px) | Collapsible sidebar | Bottom navigation (3 tabs) | SCR-021-023, SCR-028 |

**Primary Navigation Items:**
- **Patient**: Book, Appointments, Documents, Profile
- **Staff**: Dashboard, Walk-In, Queue, Verify, Metrics
- **Admin**: Users, Metrics, Profile

### Secondary Navigation
| Pattern | Usage | Screens |
|---------|-------|---------|
| Tabs | Category switching within screen (e.g., fact categories in 360-view) | SCR-017 (Vitals, Medications, History, Diagnoses, Procedures), SCR-027 (Account, Notifications, Preferences), SCR-028 (Bookings, AI Performance, Clinical Quality) |
| Breadcrumbs | Workflow depth indication | SCR-017, SCR-018, SCR-019 (Staff Dashboard > Patient Chart > 360-View > Conflict Resolution) |
| Stepper | Multi-step flow progress | SCR-001-006 (Booking flow: 3 steps), SCR-016-020 (Verification flow: 4 steps) |

### Utility Navigation (All Personas)
- Avatar dropdown (top-right): Profile, Settings, Logout
- Notification badge (top-right, Staff/Admin only): Pending verifications, critical conflicts

---

## 4. Entry Points & Primary Paths

### Patient Primary Path (FL-001)
```
Entry: SCR-024 (Login) OR Guest Mode
    ↓
SCR-001 (Availability Search)
    ↓
SCR-002 (Slot Selection)
    ↓
SCR-003 (Patient Details Form - inline account creation)
    ↓
Decision: SCR-004 (Manual Intake) OR SCR-005 (Conversational Intake)
    ↓
SCR-006 (Booking Confirmation)
```

### Staff Primary Path (FL-003 + FL-005)
```
Entry: SCR-024 (Login)
    ↓
SCR-010 (Staff Dashboard)
    ↓ (Walk-In Path)
SCR-011 (Walk-In Booking)
    ↓
SCR-012 (Same-Day Queue)
    ↓
SCR-013 (Patient Arrival Marking)

OR

SEO-010 (Staff Dashboard)
    ↓ (Verification Path)
SCR-016 (Patient Chart Review)
    ↓
SCR-017 (360-Degree Patient View)
    ↓
SCR-018 (Conflict Resolution - if conflicts exist)
    ↓
SCR-019 (Code Verification)
    ↓
SCR-020 (Verification Complete)
```

### Admin Primary Path (FL-006)
```
Entry: SCR-024 (Login)
    ↓
SCR-021 (User Management)
    ↓
Decision: Create OR Edit
    ↓
SCR-022 (Create/Edit User)
    ↓
SCR-023 (Role Assignment)
    ↓
SCR-021 (User Management - updated list)
```

---

## 5. Information Hierarchy

### Content Organization

**Patient Portal**
```
Level 1: Authentication (Login)
Level 2: Dashboard / Booking Entry
Level 3: Booking Flow Steps (Search → Select → Details → Intake → Confirm)
Level 4: Secondary Features (My Appointments, Documents, Profile)
```

**Staff Portal**
```
Level 1: Authentication (Login)
Level 2: Operational Dashboard (Summary Cards + Queue Table)
Level 3: Primary Workflows (Walk-In Management, Clinical Verification)
Level 4: Detail Screens (360-View, Conflict Resolution, Code Verification)
Level 5: Supporting Features (Metrics, Profile)
```

**Admin Portal**
```
Level 1: Authentication (Login)
Level 2: User Management Dashboard
Level 3: CRUD Operations (Create/Edit User, Role Assignment)
Level 4: Supporting Features (Metrics, Profile)
```

---

## 6. Content Types & Data Models

| Content Type | Screens | Key Attributes |
|--------------|---------|----------------|
| Appointments | SCR-001, SCR-002, SCR-006, SCR-008, SCR-009, SCR-010, SCR-011, SCR-012 | Slot datetime, patient name, provider, status, no-show risk, watchlist target |
| Patient Data | SCR-003, SCR-017, SCR-018, SCR-026 | Email, name, DOB, phone, insurance (provider + member ID), demographics |
| Intake Responses | SCR-004, SCR-005 | Question-answer pairs (JSONB), mode (conversational/manual) |
| Clinical Documents | SCR-014, SCR-015 | Filename, upload date, processing status (processing/completed/manual-review), file size |
| Extracted Facts | SCR-017, SCR-019 | Fact type (vitals/medications/history/diagnoses/procedures), value, confidence score, source segment reference |
| Conflicts | SCR-017, SCR-018 | Conflicting values from sources A/B, conflict type, resolution status |
| Code Suggestions | SCR-019 | Code type (ICD-10/CPT), code value, evidence fact IDs, staff review status |
| Users | SCR-021, SCR-022, SCR-023 | Name, email, role (patient/staff/admin), permissions bitfield, active status |
| Operational Metrics | SCR-028 | Booking volumes, no-show outcomes, AI agreement rate, conflicts detected (time-series data) |

---

## 7. User Flows Per Persona

### Patient Flows
| Flow ID | Flow Name | Screens | Trigger |
|---------|-----------|---------|---------|
| FL-001 | Patient Appointment Booking | SCR-024 → SCR-001 → SCR-002 → SCR-003 → SCR-004/SCR-005 → SCR-006 | User clicks "Book Appointment" |
| FL-002 | Preferred Slot Swap Request | SCR-024 → SCR-008 → SCR-009 → SCR-008 | User clicks "Request Preferred Slot" |
| FL-004 | Clinical Document Upload | SCR-024 → SCR-014 → SCR-015 | User navigates to "Upload Documents" |

### Staff Flows
| Flow ID | Flow Name | Screens | Trigger |
|---------|-----------|---------|---------|
| FL-003 | Staff Walk-In Management | SCR-024 → SCR-010 → SCR-011 → SCR-012 → SCR-013 → SCR-012 | Staff navigates to walk-in booking |
| FL-004 | Clinical Document Upload (Staff) | SCR-024 → SCR-010 → SCR-014 → SCR-015 | Staff uploads clinical documents post-visit |
| FL-005 | Staff Clinical Verification | SCR-024 → SCR-016 → SCR-017 → SCR-018 → SCR-019 → SCR-020 → SCR-016 | Staff selects patient from verification queue |
| FL-007 | Operational Metrics Review | SCR-024 → SCR-028 | Staff navigates to "Metrics Dashboard" |

### Admin Flows
| Flow ID | Flow Name | Screens | Trigger |
|---------|-----------|---------|---------|
| FL-006 | Admin User Management | SCR-024 → SCR-021 → SCR-022 → SCR-023 → SCR-021 | Admin navigates to "User Management" |
| FL-007 | Operational Metrics Review (Admin) | SCR-024 → SCR-028 | Admin navigates to "Metrics Dashboard" |

---

## 8. Responsive Behavior

### Breakpoints (Material-UI)
| Breakpoint | Width | Layout Adjustments |
|------------|-------|-------------------|
| xs | 0-599px | Bottom navigation, single-column layouts, stacked forms |
| sm | 600-899px | Bottom navigation or collapsible sidebar, 2-column grids |
| md | 900-1199px | Persistent sidebar, multi-column layouts |
| lg | 1200-1535px | Expanded content width, side-by-side detail views |
| xl | 1536px+ | Full-width dashboards, multi-panel layouts |

### Navigation Adaptation
- **Desktop (>= 900px)**: Persistent sidebar (240px) for Staff/Admin, top header for Patient
- **Tablet (600-899px)**: Collapsible sidebar for Staff/Admin, icon-only collapsed state
- **Mobile (<= 600px)**: Bottom navigation (56px height) for all personas, hamburger menu for secondary

### Content Adaptation Examples
| Screen | Desktop | Tablet | Mobile |
|--------|---------|--------|--------|
| SCR-010 (Staff Dashboard) | 4-column summary grid + full table | 2-column grid + table | 1-column stacked + horizontal scroll table |
| SCR-017 (360-Degree Patient View) | Sidebar + tabbed content + source drawer | Tabbed content + modal drawer | Tabbed content full screen + full-screen drawer |
| SCR-019 (Code Verification) | Side-by-side code list + evidence panel | Stacked code list / evidence panel | Full-screen code list, detail modal |

---

## 9. Taxonomies & Categories

### Appointment Categories
- Booking Status: Confirmed, Watchlist, Cancelled, Completed
- No-Show Risk: Low (<30%), Medium (30-70%), High (>70%)
- Queue Status: Waiting, In Room, Completed

### Clinical Data Categories (from Healthcare Color Coding)
- **Vitals** (Pink #E91E63): Blood pressure, heart rate, temperature
- **Medications** (Deep Orange #FF5722): Prescription drugs, dosages
- **History** (Brown #795548): Medical history, family history, allergies
- **Diagnoses** (Deep Purple #673AB7): ICD-10 codes, condition descriptions
- **Procedures** (Teal #009688): CPT codes, surgical history, treatments

### Document Processing Status
- Processing (Yellow badge): Extraction in progress
- Completed (Green badge): Ready for verification
- Manual Review (Orange badge): Confidence < 70%, requires staff review

### User Roles
- Patient: Self-service booking and document upload only
- Staff: Operational + clinical workflows (walk-in, verification, queue management)
- Admin: User management + system governance

---

## 10. Wireframe-to-Figma Traceability

All wireframes follow naming convention: `wireframe-SCR-XXX-{screen-name}.html`

| Screen Priority | Screen Count | Status |
|-----------------|--------------|--------|
| P0 (Critical) | 18 screens | High-fidelity HTML wireframes generated |
| P1 (Core) | 6 screens | High-fidelity HTML wireframes generated |
| P2 (Important) | 3 screens | High-fidelity HTML wireframes generated |
| P3 (Nice-to-have) | 1 screen | High-fidelity HTML wireframes generated |

**Total Wireframes**: 28 high-fidelity HTML wireframes with full Material-UI design token application

---

## Version History
| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2024 | Initial information architecture for Phase 1 MVP - 28 screens, 7 flows, 3 personas |
