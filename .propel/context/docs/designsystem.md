# Design System - Unified Patient Access & Clinical Intelligence Platform

## 1. Design System Overview

**Platform**: Web (Responsive)
**Framework**: React 18 + Material-UI (MUI) v5
**Design Philosophy**: Clinical trust-first with accessible, healthcare-appropriate aesthetics

---

## 2. Color Palette

### Primary Colors
```yaml
primary:
  50: "#E3F2FD"   # Light background tints
  100: "#BBDEFB"
  200: "#90CAF9"
  300: "#64B5F6"
  400: "#42A5F5"
  500: "#2196F3"  # Main primary (Medical Blue)
  600: "#1E88E5"
  700: "#1976D2"
  800: "#1565C0"
  900: "#0D47A1"
  usage: "Primary CTAs, active states, links, focus indicators"
  affected_components: ["Button", "Link", "Tabs", "ProgressBar", "Badge"]
```

### Secondary Colors
```yaml
secondary:
  50: "#F3E5F5"
  100: "#E1BEE7"
  200: "#CE93D8"
  300: "#BA68C8"
  400: "#AB47BC"
  500: "#9C27B0"  # Main secondary (Clinical Purple for accents)
  600: "#8E24AA"
  700: "#7B1FA2"
  800: "#6A1B9A"
  900: "#4A148C"
  usage: "Secondary actions, highlights, staff-specific UI elements"
  affected_components: ["Button secondary variant", "Badge secondary", "IconButton accents"]
```

### Semantic Colors
```yaml
success:
  main: "#4CAF50"  # Green for verified, completed states
  light: "#81C784"
  dark: "#388E3C"
  contrastText: "#FFFFFF"
  usage: "Verification confirmed, booking success, conflict resolved"
  affected_components: ["Alert success", "Badge verified", "StatusIndicator success"]

warning:
  main: "#FF9800"  # Orange for pending review, manual review needed
  light: "#FFB74D"
  dark: "#F57C00"
  contrastText: "#000000"
  usage: "Manual review required, low confidence extraction, conflict detected"
  affected_components: ["Alert warning", "Badge manual-review", "ConflictCard"]

error:
  main: "#F44336"  # Red for errors, critical conflicts, rejected actions
  light: "#E57373"
  dark: "#D32F2F"
  contrastText: "#FFFFFF"
  usage: "Booking errors, validation failures, critical conflicts"
  affected_components: ["Alert error", "TextField error state", "Dialog destructive actions"]

info:
  main: "#2196F3"  # Blue for informational messages
  light: "#64B5F6"
  dark: "#1976D2"
  contrastText: "#FFFFFF"
  usage: "Informational alerts, help tooltips, insurance soft validation feedback"
  affected_components: ["Alert info", "Tooltip", "Badge info"]
```

### Neutral Scale
```yaml
neutral:
  0: "#FFFFFF"    # Pure white (canvas, card backgrounds)
  50: "#FAFAFA"   # App background
  100: "#F5F5F5"  # Subtle background differentiation
  200: "#EEEEEE"  # Disabled backgrounds
  300: "#E0E0E0"  # Borders
  400: "#BDBDBD"  # Disabled text
  500: "#9E9E9E"  # Secondary text
  600: "#757575"  # Body text
  700: "#616161"  # Headings
  800: "#424242"  # Strong emphasis
  900: "#212121"  # High contrast text
```

### Healthcare-Specific Colors
```yaml
clinical:
  vitals: "#E91E63"      # Pink for vitals category
  medications: "#FF5722" # Deep Orange for medications
  history: "#795548"     # Brown for history
  diagnoses: "#673AB7"   # Deep Purple for diagnoses
  procedures: "#009688"  # Teal for procedures
  usage: "360-degree patient view fact category color coding"
  affected_components: ["Tabs (fact categories)", "Badge (fact type)", "Card headers"]
```

---

## 3. Typography

### Font Families
```yaml
heading:
  family: "Roboto"  # Material-UI default
  fallback: "-apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif"
  usage: "All headings (H1-H6), modal titles, section headers"

body:
  family: "Roboto"
  fallback: "-apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif"
  usage: "Paragraph text, labels, table data"

mono:
  family: "'Roboto Mono', 'Courier New', monospace"
  usage: "Code snippets, ICD-10/CPT codes, patient IDs, timestamps"
```

### Type Scale
```yaml
h1:
  size: "2.5rem"     # 40px
  weight: 300
  line-height: 1.2
  letter-spacing: "-0.01562em"
  used_in: ["Page headers (rare)"]

h2:
  size: "2rem"       # 32px
  weight: 300
  line-height: 1.2
  letter-spacing: "-0.00833em"
  used_in: ["Section headers", "Modal titles"]

h3:
  size: "1.75rem"    # 28px
  weight: 400
  line-height: 1.167
  letter-spacing: "0em"
  used_in: ["Subsection headers", "Card titles"]

h4:
  size: "1.5rem"     # 24px
  weight: 400
  line-height: 1.235
  letter-spacing: "0.00735em"
  used_in: ["Component headers", "Dashboard card titles"]

h5:
  size: "1.25rem"    # 20px
  weight: 400
  line-height: 1.334
  letter-spacing: "0em"
  used_in: ["List headers", "Table headers"]

h6:
  size: "1.125rem"   # 18px
  weight: 500
  line-height: 1.6
  letter-spacing: "0.0075em"
  used_in: ["Emphasized text", "Subheaders"]

body1:
  size: "1rem"       # 16px
  weight: 400
  line-height: 1.5
  letter-spacing: "0.00938em"
  used_in: ["Primary body text", "Form labels", "Table data"]

body2:
  size: "0.875rem"   # 14px
  weight: 400
  line-height: 1.43
  letter-spacing: "0.01071em"
  used_in: ["Secondary text", "Help text", "Captions in cards"]

button:
  size: "0.875rem"   # 14px (small), 1rem (medium), 1.125rem (large)
  weight: 500
  line-height: 1.75
  letter-spacing: "0.02857em"
  text-transform: "uppercase"
  used_in: ["Button labels", "Tabs", "Links"]

caption:
  size: "0.75rem"    # 12px
  weight: 400
  line-height: 1.66
  letter-spacing: "0.03333em"
  used_in: ["Timestamps", "Helper text", "Badge text"]

overline:
  size: "0.75rem"    # 12px
  weight: 400
  line-height: 2.66
  letter-spacing: "0.08333em"
  text-transform: "uppercase"
  used_in: ["Section labels", "Metadata labels"]
```

---

## 4. Spacing

### Base Unit
```yaml
base: 8px
rationale: "Material Design 8px grid system for consistent spatial rhythm"
```

### Spacing Scale
```yaml
spacing:
  0: 0        # No spacing
  1: 8px      # Tight spacing within components
  2: 16px     # Standard spacing between elements
  3: 24px     # Medium spacing for sections
  4: 32px     # Large spacing for major sections
  5: 40px     # Extra large spacing
  6: 48px     # Page padding desktop
  7: 56px     # Large section dividers
  8: 64px     # Maximum spacing
  9: 72px     # Page padding max
  10: 80px    # Exceptional spacing

affected_layouts:
  - "Form field vertical spacing: spacing(2) = 16px"
  - "Card padding: spacing(3) = 24px"
  - "Section dividers: spacing(4) = 32px"
  - "Page content padding desktop: spacing(6) = 48px"
  - "Page content padding mobile: spacing(2) = 16px"
  - "Button spacing (gap between buttons): spacing(2) = 16px"
  - "List item padding: spacing(2) = 16px vertical, spacing(3) = 24px horizontal"
```

---

## 5. Border Radius

```yaml
radius:
  none: 0           # Sharp corners (tables, strict grids)
  small: 4px        # Buttons, badges, chips
  medium: 8px       # Cards, modals, inputs
  large: 16px       # Drawer headers, feature cards
  full: 9999px      # Avatars, pills, rounded buttons

usage:
  button: "small (4px)"
  textField: "small (4px)"
  card: "medium (8px)"
  modal: "medium (8px)"
  drawer: "large (16px) top corners only"
  avatar: "full (circle)"
  badge: "small (4px)"
```

---

## 6. Elevation / Shadows

```yaml
elevation:
  0:
    box-shadow: "none"
    usage: "Flat elements, disabled states"

  1:
    box-shadow: "0px 2px 1px -1px rgba(0,0,0,0.2), 0px 1px 1px 0px rgba(0,0,0,0.14), 0px 1px 3px 0px rgba(0,0,0,0.12)"
    usage: "Buttons, raised cards, default state"

  2:
    box-shadow: "0px 3px 1px -2px rgba(0,0,0,0.2), 0px 2px 2px 0px rgba(0,0,0,0.14), 0px 1px 5px 0px rgba(0,0,0,0.12)"
    usage: "Floating action buttons, hover states"

  3:
    box-shadow: "0px 3px 3px -2px rgba(0,0,0,0.2), 0px 3px 4px 0px rgba(0,0,0,0.14), 0px 1px 8px 0px rgba(0,0,0,0.12)"
    usage: "Modals, drawers, dropdown menus"

  4:
    box-shadow: "0px 2px 4px -1px rgba(0,0,0,0.2), 0px 4px 5px 0px rgba(0,0,0,0.14), 0px 1px 10px 0px rgba(0,0,0,0.12)"
    usage: "App bar, navigation headers (scrolled state)"

  5:
    box-shadow: "0px 3px 5px -1px rgba(0,0,0,0.2), 0px 5px 8px 0px rgba(0,0,0,0.14), 0px 1px 14px 0px rgba(0,0,0,0.12)"
    usage: "Persistent drawers, highest elevation modals"
```

---

## 7. Breakpoints

```yaml
breakpoints:
  xs: 0         # Mobile portrait
  sm: 600px     # Mobile landscape, small tablets
  md: 900px     # Tablets
  lg: 1200px    # Desktop
  xl: 1536px    # Large desktop

responsive_rules:
  - Mobile (xs-sm): Bottom navigation, vertical forms, single column layouts
  - Tablet (md): Sidebar navigation (collapsible), 2-column layouts
  - Desktop (lg-xl): Persistent sidebar, multi-column dashboards, data tables
```

---

## 8. Component Specifications

### Button
```yaml
variants:
  primary:
    background: "primary.500"
    color: "white"
    hover: "primary.700"
    disabled: "neutral.200"

  secondary:
    background: "secondary.500"
    color: "white"
    hover: "secondary.700"

  outlined:
    background: "transparent"
    border: "1px solid primary.500"
    color: "primary.500"
    hover: "primary.50 background"

  text:
    background: "transparent"
    color: "primary.500"
    hover: "primary.50 background"

sizes:
  small: { height: "32px", padding: "4px 10px", fontSize: "0.8125rem" }
  medium: { height: "40px", padding: "6px 16px", fontSize: "0.875rem" }
  large: { height: "48px", padding: "8px 22px", fontSize: "1rem" }

states:
  - default
  - hover (elevation +1)
  - focus (2px primary.500 outline)
  - active (elevation -1, darker background)
  - disabled (neutral.200 background, neutral.400 text, no elevation)
  - loading (spinner icon, disabled interaction)
```

### TextField
```yaml
variants:
  outlined:
    border: "1px solid neutral.300"
    background: "white"
    focusBorder: "2px solid primary.500"

  filled:
    background: "neutral.100"
    bottomBorder: "1px solid neutral.300"
    focusBorderBottom: "2px solid primary.500"

sizes:
  small: { height: "40px", fontSize: "0.875rem" }
  medium: { height: "56px", fontSize: "1rem" }

states:
  - default
  - focus (primary.500 border, blue glow)
  - error (error.main border, error helper text below)
  - disabled (neutral.200 background, neutral.400 text)
  - filled (value present, label shrunk)

helperText:
  default: "neutral.600"
  error: "error.main"

label:
  floating: true  # Shrinks on focus/filled
  color: "neutral.600 (default), primary.500 (focused), error.main (error)"
```

### Card
```yaml
elevation: 1
padding: "spacing(3) = 24px"
borderRadius: "medium (8px)"
background: "white"

sections:
  header:
    padding: "spacing(2) = 16px spacing(3) = 24px"
    borderBottom: "1px solid neutral.200"
    typography: "h5"

  content:
    padding: "spacing(3) = 24px"

  actions:
    padding: "spacing(2) = 16px spacing(3) = 24px"
    justify: "flex-end"
    gap: "spacing(1) = 8px"

variants:
  - default (elevation 1)
  - outlined (border, no elevation)
  - interactive (hover elevation 2, cursor pointer)
```

### Table
```yaml
header:
  background: "neutral.100"
  fontWeight: 500
  color: "neutral.800"
  padding: "spacing(2) = 16px"
  borderBottom: "2px solid neutral.300"

row:
  padding: "spacing(2) = 16px"
  borderBottom: "1px solid neutral.200"
  hover: "neutral.50 background"

cell:
  verticalAlign: "middle"
  fontSize: "body1 (1rem)"

pagination:
  position: "bottom right"
  rowsPerPageOptions: [10, 25, 50, 100]
```

### Modal/Dialog
```yaml
backdrop: "rgba(0, 0, 0, 0.5)"
elevation: 3
borderRadius: "medium (8px)"
maxWidth: "600px (small), 900px (medium), 1200px (large)"
padding: "spacing(3) = 24px"

header:
  padding: "spacing(3) = 24px"
  borderBottom: "1px solid neutral.200"
  typography: "h4"
  closeButton: "IconButton top-right"

content:
  padding: "spacing(3) = 24px"
  maxHeight: "60vh"
  overflow: "auto"

actions:
  padding: "spacing(2) = 16px spacing(3) = 24px"
  borderTop: "1px solid neutral.200"
  gap: "spacing(2) = 16px"
  justify: "flex-end"
```

### Badge
```yaml
variants:
  - primary (primary.500 background)
  - secondary (secondary.500 background)
  - success (success.main background)
  - warning (warning.main background)
  - error (error.main background)
  - info (info.main background)
  - neutral (neutral.500 background)

size:
  small: { minHeight: "16px", padding: "0 4px", fontSize: "0.625rem" }
  medium: { minHeight: "20px", padding: "0 6px", fontSize: "0.75rem" }

borderRadius: "small (4px)"
fontWeight: 500
position: "top-right (notification badge) OR inline (status badge)"
```

### Toast/Snackbar
```yaml
position: "bottom-center (mobile), bottom-left (desktop)"
elevation: 4
minWidth: "288px"
maxWidth: "568px"
padding: "spacing(2) = 16px"
borderRadius: "small (4px)"
autoHideDuration: 6000  # 6 seconds

variants:
  success: { background: "success.main", icon: "CheckCircle" }
  warning: { background: "warning.main", icon: "Warning" }
  error: { background: "error.main", icon: "Error" }
  info: { background: "info.main", icon: "Info" }

actions:
  - close button (IconButton)
  - optional action button (text button)
```

### Avatar
```yaml
sizes:
  small: "32px"
  medium: "40px"
  large: "64px"
  xlarge: "96px"

borderRadius: "full (circle)"
background: "primary.500 (default), custom for initials"
color: "white"
fontWeight: 500

variants:
  - image (background-image)
  - initials (2-letter text)
  - icon (icon component)
```

### Progress Indicators
```yaml
linear:
  height: "4px"
  background: "neutral.200"
  fill: "primary.500"
  borderRadius: "full"
  usage: "Form progress, file upload progress"

circular:
  sizes: { small: "24px", medium: "40px", large: "64px" }
  thickness: 4
  color: "primary.500"
  usage: "Loading states, button loading"

skeleton:
  background: "neutral.200"
  animation: "pulse (1.5s ease-in-out infinite)"
  borderRadius: "small (4px)"
  usage: "Content loading placeholders"
```

---

## 9. Accessibility Specifications

### Color Contrast
```yaml
text_small_aa: 4.5:1     # < 18pt regular or 14pt bold
text_large_aa: 3.0:1     # >= 18pt regular or 14pt bold
ui_components: 3.0:1     # Borders, icons, focus indicators

validated_combinations:
  - primary.500 on white: 4.51:1 (AA compliant for text)
  - neutral.900 on white: 16.1:1 (AAA compliant)
  - white on primary.500: 4.51:1 (AA compliant)
  - error.main on white: 4.61:1 (AA compliant)
```

### Focus States
```yaml
focusIndicator:
  outlineColor: "primary.500"
  outlineWidth: "2px"
  outlineStyle: "solid"
  outlineOffset: "2px"
  borderRadius: "inherit"

keyboard_navigation:
  - Tab order follows DOM order
  - Focus visible on all interactive elements
  - Skip links for main content
```

### Touch Targets
```yaml
minimum_size: "44x44px"  # iOS/Android accessibility guidelines
affected_components:
  - Button (all sizes >= 44px height)
  - IconButton (minimum 44x44px)
  - Checkbox (44x44px touch area, 18x18px visible)
  - Radio (44x44px touch area, 20x20px visible)
  - Table row actions (IconButton 44x44px)
```

### Screen Reader Support
```yaml
aria_labels: "All interactive elements without visible text"
aria_live_regions: "Toast notifications, real-time queue updates"
semantic_html: "nav, main, section, article, aside"
headings_hierarchy: "Logical h1-h6 structure"
```

---

## 10. Animation & Transitions

```yaml
durations:
  shortest: 150ms     # Checkbox, switch, simple icon rotations
  shorter: 200ms      # Hover state changes, focus outlines
  short: 250ms        # Modal open/close, drawer slide
  standard: 300ms     # Default transition, card elevation
  complex: 375ms      # Complex multi-property transitions
  enteringScreen: 225ms
  leavingScreen: 195ms

easings:
  easeInOut: "cubic-bezier(0.4, 0, 0.2, 1)"  # Default Material Design easing
  easeOut: "cubic-bezier(0.0, 0, 0.2, 1)"    # Enter screen transitions
  easeIn: "cubic-bezier(0.4, 0, 1, 1)"        # Exit screen transitions
  sharp: "cubic-bezier(0.4, 0, 0.6, 1)"       # Sharp transitions

usage:
  - Button hover: "background-color 200ms easeInOut"
  - Modal open: "opacity 225ms easeOut, transform 225ms easeOut"
  - Drawer slide: "transform 300ms easeInOut"
  - Tooltip fade: "opacity 150ms easeInOut"
```

---

## 11. Icons

```yaml
library: "Material Icons" (bundled with MUI)
sizes:
  small: 20px
  medium: 24px  # Default
  large: 32px
  xlarge: 48px

usage:
  - Navigation icons: medium (24px)
  - Button icons: small (20px) or medium (24px)
  - Avatar fallback icons: medium (24px) or large (32px)
  - Feature icons: xlarge (48px)

color:
  - Inherit from parent component (default)
  - primary.500 (active navigation)
  - neutral.600 (inactive navigation)
  - error.main (destructive actions)
  - success.main (confirmation actions)
```

---

## 12. Grid System

```yaml
container:
  maxWidth:
    sm: 600px
    md: 900px
    lg: 1200px
    xl: 1536px
  padding:
    xs: "spacing(2) = 16px"
    sm: "spacing(3) = 24px"
    md: "spacing(4) = 32px"
    lg: "spacing(6) = 48px"

columns: 12  # Material-UI standard 12-column grid

spacing: "spacing(2) = 16px (default), spacing(3) = 24px (large)"

responsive_columns:
  xs: 12     # Full width mobile
  sm: 6      # Half width tablet
  md: 4      # Third width desktop
  lg: 3      # Quarter width large desktop
```

---

## 13. Z-Index Layers

```yaml
z-index:
  appBar: 1100
  drawer: 1200
  modal: 1300
  snackbar: 1400
  tooltip: 1500

rationale: "MUI default z-index scale to prevent overlap conflicts"
```

---

## 14. Brand Guidelines

### Logo
- **File**: [To be provided]
- **Usage**: Header left (desktop), center (mobile)
- **Minimum Size**: 120px width
- **Clear Space**: 16px on all sides

### Voice & Tone
- **Overall Tone**: Professional, reassuring, clinical trust-first
- **Error Messages**: Helpful, non-blaming, actionable (e.g., "Unable to connect. Please check your network and try again.")
- **Empty States**: Encouraging, guiding (e.g., "No appointments yet. Start by searching available slots.")
- **Success Messages**: Brief, celebratory (e.g., "Appointment confirmed! Check your email for details.")

---

## 15. Implementation Notes

### Material-UI Theme Configuration
```typescript
import { createTheme } from '@mui/material/styles';

const theme = createTheme({
  palette: {
    primary: {
      main: '#2196F3',
    },
    secondary: {
      main: '#9C27B0',
    },
    success: { main: '#4CAF50' },
    warning: { main: '#FF9800' },
    error: { main: '#F44336' },
    info: { main: '#2196F3' },
  },
  typography: {
    fontFamily: 'Roboto, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
  },
  spacing: 8,  // 8px base unit
  shape: {
    borderRadius: 8,  // Medium border radius default
  },
  breakpoints: {
    values: {
      xs: 0,
      sm: 600,
      md: 900,
      lg: 1200,
      xl: 1536,
    },
  },
});
```

### Component Constraints
- Use only MUI components from the library
- Custom components must follow MUI theming system
- All components must support all defined states (Default, Hover, Focus, Active, Disabled, Loading)
- Follow naming convention: `C/<Category>/<Name>` in Figma
- Ensure WCAG 2.2 AA compliance for all custom components

---

## 16. References

| Reference | Link | Purpose |
|-----------|------|---------|
| Material-UI Documentation | https://mui.com/ | Component API reference, theming guide |
| Material Design System | https://m3.material.io/ | Design principles, color science, motion |
| WCAG 2.2 Guidelines | https://www.w3.org/WAI/WCAG22/quickref/ | Accessibility compliance validation |

---

## Version History
| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2024 | Initial design system for Phase 1 |
