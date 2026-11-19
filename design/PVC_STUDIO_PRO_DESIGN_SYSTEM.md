# PVC STUDIO PRO — DESIGN SYSTEM SPECIFICATION & SEMANTIC TOKEN ARCHITECTURE

**Standard:** Windows 11 Fluent 2 Desktop Design System  
**Contrast Target:** WCAG 2.1 AA Compliant ($\ge 4.5:1$ body text, $\ge 3:1$ large text/controls)  
**Author:** Senior Windows Desktop Software Architect & UI/UX Designer  

---

## 1. Central Semantic Token Dictionary

All controls across all 14 workspaces, dialogs, crop canvases, preview viewports, and status bars must consume these semantic tokens exclusively via `{DynamicResource TokenName}`. No hardcoded hex color overrides are permitted in view XAML.

### 1.1 Palette & Surface Tokens

| Semantic Token Name | Light Mode Value | Dark Mode Value | Semantic Role & Usage |
| :--- | :--- | :--- | :--- |
| `WindowBackgroundBrush` | `#F3F4F6` (Cool Gray 100) | `#0F172A` (Slate 900) | Root application window background (behind sidebar and viewports) |
| `NeutralSurfaceBrush` | `#FFFFFF` (Pure White) | `#1E293B` (Slate 800) | Sidebar pane, toolbars, headers, status bar |
| `CardSurfaceBrush` | `#FFFFFF` (Pure White) | `#1E293B` (Slate 800) | Primary functional panels, workspace cards |
| `ElevatedSurfaceBrush` | `#F8FAFC` (Slate 50) | `#334155` (Slate 700) | Modals, flyouts, dropdown popups, floating palettes |
| `PreviewCanvasBgBrush` | `#E2E8F0` (Slate 200) | `#0B0F19` (Deep Slate) | Virtual print tray simulator and Crop Studio canvas background |
| `NeutralBorderBrush` | `#E2E8F0` (Slate 200) | `#334155` (Slate 700) | Outer container borders, card dividers |
| `SubtleBorderBrush` | `#F1F5F9` (Slate 100) | `#1E293B` (Slate 800) | Inner gridlines, subtle separators |
| `DividerBrush` | `#CBD5E1` (Slate 300) | `#475569` (Slate 600) | Splitters, section dividers |

### 1.2 Typography & Text Tokens

| Semantic Token Name | Light Mode Value | Dark Mode Value | Contrast Ratio Target | Usage |
| :--- | :--- | :--- | :--- | :--- |
| `TextPrimaryBrush` | `#0F172A` (Slate 900) | `#F8FAFC` (Slate 50) | $\ge 12:1$ | Window titles, card headings, primary data readouts |
| `TextSecondaryBrush` | `#475569` (Slate 600) | `#94A3B8` (Slate 400) | $\ge 5.2:1$ | Subtitles, labels, secondary metadata, unit labels |
| `TextTertiaryBrush` | `#64748B` (Slate 500) | `#64748B` (Slate 500) | $\ge 4.5:1$ | Timestamps, placeholders, inactive hints |
| `TextDisabledBrush` | `#94A3B8` (Slate 400) | `#475569` (Slate 600) | $\ge 3:1$ | Disabled buttons and grayed-out controls |

### 1.3 Action & Interactive Tokens

| Semantic Token Name | Light Mode Value | Dark Mode Value | Usage |
| :--- | :--- | :--- | :--- |
| `PrimaryAccentBrush` | `#0284C7` (Sky 600) | `#38BDF8` (Sky 400) | Primary action buttons ("Browse", "Approve & Print", "Apply Crop") |
| `PrimaryAccentHoverBrush` | `#0369A1` (Sky 700) | `#0EA5E9` (Sky 500) | Hover state of primary action buttons |
| `PrimaryAccentPressedBrush` | `#075985` (Sky 800) | `#0284C7` (Sky 600) | Pressed/clicked state of primary action buttons |
| `AccentTextBrush` | `#FFFFFF` (White) | `#0F172A` (Dark Slate) | Foreground text rendered on top of PrimaryAccentBrush |
| `ControlBackgroundBrush` | `#FFFFFF` (White) | `#1E293B` (Slate 800) | TextBoxes, ComboBoxes, Numeric UpDowns |
| `ControlBorderBrush` | `#CBD5E1` (Slate 300) | `#475569` (Slate 600) | Inactive input borders |
| `ControlFocusBorderBrush` | `#0284C7` (Sky 600) | `#38BDF8` (Sky 400) | Focused input border highlight (2px) |

### 1.4 State & Alert Tokens

| Semantic Token Name | Light Mode Value | Dark Mode Value | Usage |
| :--- | :--- | :--- | :--- |
| `StatusSuccessBgBrush` | `#ECFDF5` (Emerald 50) | `#064E3B` (Emerald 900) | Job complete banner, valid license pill |
| `StatusSuccessFgBrush` | `#047857` (Emerald 700) | `#34D399` (Emerald 400) | Success text and icons |
| `StatusWarningBgBrush` | `#FFFBEB` (Amber 50) | `#78350F` (Amber 900) | Password-required alert banner, calibration notice |
| `StatusWarningFgBrush` | `#B45309` (Amber 700) | `#FBBF24` (Amber 400) | Warning text and icons |
| `StatusErrorBgBrush` | `#FEF2F2` (Red 50) | `#7F1D1D` (Red 900) | Invalid password alert, corrupted document |
| `StatusErrorFgBrush` | `#B91C1C` (Red 700) | `#F87171` (Red 400) | Error text and validation icons |
| `StatusInfoBgBrush` | `#F0F9FF` (Sky 50) | `#0C4A6E` (Sky 900) | Tip boxes, keyboard shortcut hints |
| `StatusInfoFgBrush` | `#0369A1` (Sky 700) | `#38BDF8` (Sky 400) | Info text and hints |

---

## 2. Layout & Typography Rules

1. **Font Family Hierarchy:**
   - Primary: `"Segoe UI Variable Text"`, fallback `"Segoe UI"`, fallback `"Arial"`.
   - Numeric Readouts / Coordinate Inputs: `"Consolas"`, fallback `"Segoe UI"`.
2. **Type Scale:**
   - Page Header: 20px, SemiBold (`TextPrimaryBrush`)
   - Section Title: 14px, Bold (`TextPrimaryBrush`)
   - Body Text: 12px, Regular (`TextPrimaryBrush`)
   - Subtitle / Form Label: 11px, SemiBold (`TextSecondaryBrush`)
   - Captions / Badges: 10px, Bold (`TextSecondaryBrush` / Alert Fg)
3. **Control Standard Heights:**
   - Hero Buttons: 44px
   - Standard Buttons: 32px – 36px
   - Compact Toolbar Buttons: 28px
   - Input Fields: 32px
   - Table Row Height: 36px
4. **Corner Radius Standards:**
   - Outer Cards: 8px
   - Inner Containers & Badges: 4px – 6px
   - Pill Buttons: 14px
