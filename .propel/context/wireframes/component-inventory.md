# Component Inventory - Unified Patient Access & Clinical Intelligence Platform

## Source Reference
| Document | Path | Purpose |
|----------|------|---------|
| Figma Specification | `.propel/context/docs/figma_spec.md` | Component requirements per screen |
| Design System | `.propel/context/docs/designsystem.md` | Component specifications and variants |

---

## 1. Component Overview

**Total Unique Components**: 31 component types across 7 categories
**Design System**: Material-UI (MUI) v5
**Component States**: Default, Hover, Focus, Active, Disabled, Loading (where applicable)

---

## 2. Component Catalog

### 2.1 Actions (5 components)

#### Button
- **Type**: Primary action trigger
- **Variants**: Primary, Secondary, Tertiary, Ghost
- **Sizes**: Small (32px), Medium (40px), Large (48px)
- **States**: Default, Hover, Focus, Active, Disabled, Loading
- **Screens Used**: All screens (28/28)
- **Implementation Status**: ✅ Defined in designsystem.md
- **Wireframe Examples**:
  - SCR-024: Login button (Primary, Medium)
  - SCR-010: Mark Arrived buttons (Text, Small)
  - SCR-006: PDF Download, Calendar Sync buttons (Secondary, Medium)

#### IconButton
- **Type**: Icon-only action trigger
- **Variants**: Primary, Secondary, Default
- **Sizes**: Small (24px), Medium (40px), Large (48px)
- **States**: Default, Hover, Focus, Active, Disabled
- **Screens Used**: SCR-012, SCR-015, SCR-017, SCR-019, SCR-021 (5/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-017: Citation drill-down icon button
  - SCR-015: Delete document icon button
  - SCR-012: Drag handle icon button

#### Link
- **Type**: Text-based navigation
- **Variants**: Primary, Secondary  
- **States**: Default, Hover, Focus, Active
- **Screens Used**: SCR-024, SCR-006, SCR-007 (3/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-024: "Forgot password?" link
  - SCR-006: Calendar event links
  - SCR-007: "Select another slot" link

#### FAB (Floating Action Button)
- **Type**: Prominent primary action
- **Variants**: Primary
- **Sizes**: Medium (56px), Large (72px)
- **States**: Default, Hover, Focus, Active
- **Screens Used**: Not used in Phase 1 (mobile-first alternative: Bottom navigation)
- **Implementation Status**: ✅ Defined (reserved for future)

### 2.2 Inputs (7 components)

#### TextField
- **Type**: Text input field
- **Variants**: Outlined, Filled
- **Sizes**: Small (40px), Medium (56px)
- **States**: Default, Focus, Error, Disabled, Filled
- **Screens Used**: SCR-003, SCR-004, SCR-005 (chat input), SCR-011, SCR-022, SCR-024, SCR-026, SCR-027 (8/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-024: Email, Password fields (Outlined, Medium)
  - SCR-003: Name, DOB, Phone fields (Outlined, Medium)
  - SCR-005: Conversational intake chat input (Outlined, Medium)

#### Select
- **Type**: Dropdown selection
- **Variants**: Outlined, Filled
- **Sizes**: Small, Medium
- **States**: Default, Focus, Error, Disabled, Filled
- **Screens Used**: SCR-003, SCR-004, SCR-011, SCR-022, SCR-023, SCR-027 (6/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-003: Insurance provider dropdown
  - SCR-022: Role selection dropdown
  - SCR-027: Settings preferences dropdowns

#### Checkbox
- **Type**: Multiple selection input
- **Variants**: Default
- **Sizes**: Small (18px), Medium (20px)
- **States**: Unchecked, Checked, Indeterminate, Disabled
- **Screens Used**: SCR-004, SCR-013, SCR-023, SCR-024 (4/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-024: "Remember me" checkbox
  - SCR-013: Bulk arrival check-in checkboxes
  - SCR-023: Permissions bitfield checkboxes

#### Radio
- **Type**: Single selection input
- **Variants**: Default
- **Sizes**: Small (18px), Medium (20px)
- **States**: Unchecked, Checked, Disabled
- **Screens Used**: SCR-004, SCR-018 (2/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-018: Conflict resolution options (Accept source A, Accept source B, Manual override)

#### Toggle (Switch)
- **Type**: Boolean state toggler
- **Variants**: Default
- **Sizes**: Small, Medium
- **States**: Off, On, Disabled
- **Screens Used**: SCR-027 (1/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-027: Notification preferences toggles

#### DatePicker
- **Type**: Date selection calendar
- **Variants**: Outlined
- **Size**: Medium
- **States**: Default, Focus, Error, Disabled
- **Screens Used**: SCR-001, SCR-002, SCR-009, SCR-028 (4/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-001: Availability search date picker
  - SCR-009: Preferred slot calendar picker
  - SCR-028: Date range filter picker

#### FileUpload
- **Type**: Drag-and-drop file selector
- **Variants**: Drag-drop zone
- **Size**: Medium
- **States**: Idle, Hover (drag-over), Uploading, Error, Success
- **Screens Used**: SCR-014, SCR-026 (2/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-014: Clinical document upload drag-drop zone
  - SCR-026: Profile image upload

### 2.3 Navigation (5 components)

#### Header
- **Type**: Top navigation bar
- **Variants**: Desktop, Mobile
- **States**: Default
- **Screens Used**: SCR-025 (all screens via persistent header) (28/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-025: Logo, user avatar, utility navigation

#### Sidebar
- **Type**: Side navigation panel
- **Variants**: Desktop (persistent), Desktop (collapsible)
- **States**: Default, Collapsed
- **Screens Used**: Staff portal (13 screens), Admin portal (4 screens) (17/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-010: Staff sidebar with Dashboard, Walk-In, Queue, Verify, Metrics nav items

#### BottomNav
- **Type**: Mobile bottom navigation
- **Variants**: Mobile only
- **States**: Default, Active
- **Screens Used**: All screens on mobile viewport (28/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - Patient: Book, Appointments, Documents, Profile (4 tabs)
  - Staff: Dashboard, Walk-In, Queue, Verify, Metrics (5 tabs)
  - Admin: Users, Metrics, Profile (3 tabs)

#### Tabs
- **Type**: Category/section switcher
- **Variants**: Horizontal, Vertical
- **States**: Default, Active
- **Screens Used**: SCR-017, SCR-027, SCR-028 (3/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-017: Fact categories (Vitals, Medications, History, Diagnoses, Procedures)
  - SCR-027: Settings categories (Account, Notifications, Preferences)
  - SCR-028: Metric categories (Bookings, AI Performance, Clinical Quality)

#### Breadcrumbs
- **Type**: Hierarchical navigation path
- **Variants**: Default
- **States**: Default, Hover, Focus
- **Screens Used**: SCR-017, SCR-018, SCR-019 (3/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-017: Staff Dashboard > Patient Chart > 360-View
  - SCR-018: Staff Dashboard > Patient Chart > 360-View > Conflict Resolution

### 2.4 Content (9 components)

#### Card
- **Type**: Grouped content container
- **Variants**: Default, Outlined, Interactive
- **Elevation**: 0 (Outlined), 1 (Default), 2 (Interactive hover)
- **States**: Default, Hover (interactive only)
- **Screens Used**: SCR-001, SCR-002, SCR-005 (chat messages), SCR-006, SCR-009, SCR-010 (summary cards), SCR-016, SCR-017, SCR-018, SCR-019, SCR-026 (11/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-010: Summary cards (Walk-Ins Today, Queue Length, Verification Pending, Critical Conflicts)
  - SCR-001: Appointment slot cards (grid layout)
  - SCR-017: Fact cards with confidence badges

#### ListItem
- **Type**: Single item in vertical list
- **Variants**: Default, Interactive
- **States**: Default, Hover (interactive)
- **Screens Used**: SCR-008, SCR-025 (sidebar nav) (2/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-008: Appointment list items
  - SCR-025: Sidebar navigation list items

#### Table
- **Type**: Tabular data display
- **Variants**: Default, Sortable, Filterable, Paginated
- **States**: Row hover
- **Screens Used**: SCR-010, SCR-012, SCR-015, SCR-016, SCR-021, SCR-028 (6/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-010: Same-day queue table
  - SCR-021: User management table (sortable, filterable)
  - SCR-015: Document metadata table

#### Avatar
- **Type**: User profile image/initials
- **Variants**: Image, Initials, Icon
- **Sizes**: Small (32px), Medium (40px), Large (64px), XLarge (96px)
- **Shape**: Circle
- **Screens Used**: SCR-005, SCR-025, SCR-026 (3/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-025: User avatar in header (Medium, Initials)
  - SCR-005: AI/User avatars in chat (Small, Icon)
  - SCR-026: Profile avatar (XLarge, Image/Initials)

#### Badge
- **Type**: Status/count indicator
- **Variants**: Primary, Secondary, Success, Warning, Error, Info, Neutral
- **Sizes**: Small (16px height), Medium (20px height)
- **Screens Used**: SCR-002, SCR-008, SCR-010, SCR-012, SCR-013, SCR-015, SCR-016, SCR-017, SCR-019, SCR-020, SCR-021, SCR-025 (12/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-002: No-show risk badge (Orange warning)
  - SCR-010: Summary card badges (4 waiting, 5 charts, 2 urgent)
  - SCR-015: Processing status badges (Processing yellow, Completed green, Manual Review orange)

#### StatusIndicator
- **Type**: Visual status marker
- **Variants**: Dot, Label
- **States**: Success, Warning, Error, Info
- **Screens Used**: SCR-012, SCR-015 (2/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-012: Queue status dots (Waiting, In Room, Completed)

#### ProgressBar
- **Type**: Task/loading progress indicator
- **Variants**: Linear, Circular
- **Sizes**: Small (24px), Medium (40px), Large (64px) - circular only
- **Screens Used**: SCR-004 (linear stepper), SCR-014 (upload progress), SCR-005 (conversation loading) (3/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-004: Manual intake form progress stepper (linear)
  - SCR-014: Document upload progress bars (linear, per file)
  - Button loading states: Circular spinner (Small, 16px)

#### Skeleton
- **Type**: Content loading placeholder
- **Variants**: Rectangle, Circle, Text
- **Animation**: Pulse (1.5s ease-in-out infinite)
- **Screens Used**: All screens (loading states) (28/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-010: Summary card skeleton (loading state)
  - SCR-001: Slot card skeleton (loading state)
  - Table row skeletons

### 2.5 Feedback (6 components)

#### Modal
- **Type**: Overlay dialog
- **Sizes**: Small (600px), Medium (900px), Large (1200px)
- **Elevation**: 3
- **States**: Default, Opening (animation), Closing (animation)
- **Screens Used**: SCR-006 (PDF preview), SCR-009 (Preferred slot calendar), SCR-022, SCR-024 (session timeout) (4/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-022: Create/Edit User modal (Medium)
  - SCR-024: Session Timeout Warning dialog (Small)
  - SCR-006: PDF Confirmation Preview modal (Medium)

#### Drawer
- **Type**: Slide-in panel
- **Variants**: Left, Right, Top, Bottom
- **Elevation**: 3
- **States**: Default, Opening, Closing
- **Screens Used**: SCR-017 (source citation), SCR-019, SCR-021 (audit log) (3/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-017: Source Citation Drawer (Right, slides from right with PDF viewer)
  - SCR-021: Audit Log sidebar (Right)

#### Toast (Snackbar)
- **Type**: Temporary notification
- **Variants**: Success, Warning, Error, Info
- **Position**: Bottom-center (mobile), Bottom-left (desktop)
- **Duration**: 6000ms auto-hide
- **Screens Used**: All screens (state change notifications) (28/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-006: "Appointment confirmed! Check your email for details."
  - SCR-013: "Patient marked as arrived."
  - SCR-019: "Codes confirmed successfully."

#### Alert
- **Type**: Inline notification/message
- **Variants**: Success, Warning, Error, Info
- **Styles**: Filled, Outlined
- **Screens Used**: SCR-003, SCR-004, SCR-006, SCR-007, SCR-009, SCR-011, SCR-014, SCR-018, SCR-022, SCR-024, SCR-027 (11/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-024: Error alert "Invalid email or password"
  - SCR-006: Success alert "Booking confirmed"
  - SCR-009: Info alert "Watchlist registration completed"

#### Dialog
- **Type**: Decision prompt
- **Variants**: Confirmation, Destructive
- **Elevation**: 3
- **Screens Used**: SCR-015 (delete document), SCR-021 (disable user), SCR-024 (session timeout) (3/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-015: Delete Document Confirmation dialog
  - SCR-021: Disable User confirmation dialog

#### Tooltip
- **Type**: Contextual help hint
- **Variants**: Default (dark background)
- **Trigger**: Hover (desktop), Long-press (mobile)
- **Screens Used**: SCR-009, SCR-017, SCR-019, SCR-023 (4/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-009: Watchlist status indicator tooltip
  - SCR-017: Confidence badge tooltip
  - SCR-023: Permissions explanation tooltips

### 2.6 Data Visualization (4 components)

#### BarChart
- **Type**: Categorical data bar chart
- **Variants**: Vertical, Horizontal
- **Screens Used**: SCR-028 (1/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-028: Conflicts detected bar chart (vertical)

#### LineChart
- **Type**: Time-series line chart
- **Variants**: Single series, Multi-series
- **Screens Used**: SCR-028 (1/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-028: Booking trends over time line chart

#### PieChart
- **Type**: Proportional data pie/donut chart
- **Variants**: Default (Pie), Donut
- **Screens Used**: SCR-028 (1/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-028: No-show risk distribution pie chart

#### Gauge
- **Type**: Single metric gauge
- **Variants**: Semi-circle, Full-circle
- **Screens Used**: SCR-028 (1/28)
- **Implementation Status**: ✅ Defined
- **Wireframe Examples**:
  - SCR-028: AI agreement rate gauge (semi-circle, target >98%)

---

## 3. Component Usage Matrix

| Component | Usage Count | Most Used Screens |
|-----------|-------------|-------------------|
| Button | 28/28 | All screens |
| Skeleton | 28/28 | All screens (loading states) |
| Toast | 28/28 | All screens (notifications) |
| Header | 28/28 | All screens (via persistent header) |
| BottomNav | 28/28 | All screens (mobile viewport) |
| Sidebar | 17/28 | Staff (13), Admin (4) |
| TextField | 8/28 | Forms (Login, Patient Details, Manual Intake, Walk-In, User Management, Profile) |
| Badge | 12/28 | Dashboards, Lists, Status Indicators |
| Alert | 11/28 | Forms, Error states, Confirmations |
| Card | 11/28 | Dashboards, Content grouping, Lists |
| Table | 6/28 | Dashboards, Management screens |
| Select | 6/28 | Forms |
| Tabs | 3/28 | Multi-category screens (360-View, Settings, Metrics) |
| Checkbox | 4/28 | Forms, Bulk actions |
| DatePicker | 4/28 | Booking, Metrics filtering |
| Modal | 4/28 | CRUD operations, Previews |
| Tooltip | 4/28 | Help text, Explanations |
| Drawer | 3/28 | Source citations, Audit logs |
| Avatar | 3/28 | User profile representations |
| Breadcrumbs | 3/28 | Workflow depth indication |
| Dialog | 3/28 | Confirmations |
| ProgressBar | 3/28 | Multi-step flows, Uploads |
| IconButton | 5/28 | Tables, Lists, Inline actions |
| Link | 3/28 | Navigation, External links |
| ListItem | 2/28 | Lists, Navigation |
| StatusIndicator | 2/28 | Status display |
| Radio | 2/28 | Forms, Conflict resolution |
| FileUpload | 2/28 | Document upload, Profile image |
| Toggle | 1/28 | Settings |
| BarChart | 1/28 | Metrics |
| LineChart | 1/28 | Metrics |
| PieChart | 1/28 | Metrics |
| Gauge | 1/28 | Metrics |
| FAB | 0/28 | Reserved for future use |

---

## 4. Responsive Component Behavior

| Component | Desktop | Tablet | Mobile |
|-----------|---------|--------|--------|
| Sidebar | Persistent (240px) | Collapsible (icon-only) | Hidden (use BottomNav) |
| Table | Full width, all columns | Horizontal scroll | Horizontal scroll, priority columns |
| Modal | Centered, max-width | Centered, padding reduced | Full width, top-aligned |
| Drawer | 400px width | 320px width | Full width |
| Card Grid | 4 columns | 2 columns | 1 column (stacked) |
| BottomNav | Hidden | Hidden | Fixed bottom (56px) |
| Breadcrumbs | Full path | Truncated with ellipsis | Hidden or minimal (current only) |

---

## 5. Interaction States Summary

**Standard State Support** (all interactive components):
- **Default**: Visual baseline
- **Hover**: Cursor over element (desktop only)
- **Focus**: Keyboard/accessibility focus (2px solid primary.500 outline, 2px offset)
- **Active**: Click/tap in progress (deeper color, reduced elevation)
- **Disabled**: Non-interactive state (neutral.200 background, neutral.400 text, no elevation)

**Loading States**:
- **Button**: Circular spinner (16px), text transparent
- **Card**: Skeleton content placeholders
- **Table**: Skeleton rows
- **Form**: Disabled inputs + loading indicator

**Error States**:
- **TextField**: Red border (error.main), error icon, helper text below
- **Form**: Alert component with error message
- **API Failure**: Toast notification with retry option

---

## Version History
| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2024 | Initial component inventory for Phase 1 MVP - 31 component types, 28 screens |
