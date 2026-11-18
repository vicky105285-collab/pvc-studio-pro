# Professional Product Design System — PVC Studio Pro

**Design Language:** Microsoft Fluent 2 for Windows 11 Desktop  
**Target Environment:** Windows 11 64-bit Commercial Desktop  
**Languages Supported:** English & Tamil (தமிழ்)  
**Themes:** Light Theme, Dark Theme, System Theme  

---

## 1. Design Principles

1. **Production-Grade Utility:** Designed for rapid, repetitive daily transactions in busy e-Sevai centres. Controls are tactile, obvious, and immediate.
2. **Calm Visual Hierarchy:** Neutral backgrounds with focused, high-contrast semantic accents. No decorative clutter, unnecessary 3D gradients, or oversized dashboard widgets.
3. **Resilient Typography:** Engineered to display both compact English technical terms and longer Tamil script (`தமிழ்`) without truncation, overlap, or clipping.
4. **Accessible by Default:** Every interactive element has a visible focus ring, high-contrast text ($\ge 4.5:1$), and minimum 36px touch/click bounds.

---

## 2. Color Palette & Semantic Design Tokens

### Core Neutral Palette

| Token | Light Theme | Dark Theme | Purpose |
|---|---|---|---|
| `AppBackground` | `#F8FAFC` (Slate 50) | `#0F172A` (Slate 900) | Root window canvas |
| `CardBackground` | `#FFFFFF` (Pure White) | `#1E293B` (Slate 800) | Surface containers, card stages |
| `CardBackgroundSubtle` | `#F1F5F9` (Slate 100) | `#334155` (Slate 700) | Form wells, preview backdrops |
| `BorderSubtle` | `#E2E8F0` (Slate 200) | `#334155` (Slate 700) | Dividers, card boundaries |
| `BorderStrong` | `#CBD5E1` (Slate 300) | `#475569` (Slate 600) | Input field outlines, active borders |
| `TextPrimary` | `#0F172A` (Slate 900) | `#F8FAFC` (Slate 50) | Primary headers, values, body |
| `TextSecondary` | `#475569` (Slate 600) | `#94A3B8` (Slate 400) | Labels, descriptions, meta-info |
| `TextMuted` | `#64748B` (Slate 500) | `#64748B` (Slate 500) | Disabled text, subtle hints |

### Semantic Accent Palette

| Token | Hex Value | Dark Hex Value | Usage |
|---|---|---|---|
| `AccentPrimary` | `#0067B8` (Fluent Blue) | `#38BDF8` (Sky 400) | Primary action buttons, selection indicators |
| `AccentPrimaryHover` | `#005DA6` | `#0284C7` | Hover interaction state |
| `AccentSuccess` | `#107C41` (Office Green) | `#22C55E` (Emerald 500) | Revenue, successful print, verified status |
| `AccentWarning` | `#D97706` (Amber 600) | `#F59E0B` (Amber 500) | Operator confirmation required, pending calibration |
| `AccentDanger` | `#DC2626` (Red 600) | `#EF4444` (Red 500) | Errors, cancel job, delete profile |
| `CardPreviewBorder` | `#0284C7` | `#38BDF8` | CR80 physical card outline |

---

## 3. Typography System

The application uses **Segoe UI Variable** with automatic fallback to **Nirmala UI** for authentic, unclipped Tamil rendering.

| Style Role | Font Family | Size | Weight | Line Height | Tracking |
|---|---|---|---|---|---|
| **Display Title** | Segoe UI Variable Display, Nirmala UI | 22 pt | SemiBold (600) | 28 px | Normal |
| **Section Header** | Segoe UI Variable Display, Nirmala UI | 16 pt | SemiBold (600) | 22 px | Normal |
| **Subsection / Card Title** | Segoe UI Variable Text, Nirmala UI | 13 pt | Bold (700) | 18 px | Normal |
| **Form Label** | Segoe UI Variable Text, Nirmala UI | 11 pt | SemiBold (600) | 16 px | Normal |
| **Body / Input Text** | Segoe UI Variable Text, Nirmala UI | 12 pt | Regular (400) | 16 px | Normal |
| **Caption / Help Text** | Segoe UI Variable Text, Nirmala UI | 10.5 pt | Regular (400) | 14 px | Normal |
| **Metric Value (KPI)** | Segoe UI Variable Display | 26 pt | Bold (700) | 32 px | Tight |

---

## 4. Layout & Spacing Tokens

- **Grid System:** Consistent 8pt spatial grid (`8px`, `16px`, `24px`, `32px`).
- **Margins:** Standard outer margin is `20px` to `24px`.
- **Card Padding:** Standard card padding is `16px` (compact) to `24px` (feature forms).
- **Control Heights:**
  - Primary Action Buttons: `44px` height with `16px` horizontal padding.
  - Secondary / Utility Buttons: `36px` height with `12px` horizontal padding.
  - Text Boxes & ComboBoxes: `38px` height with centered vertical content alignment.
  - DataGrid Rows: `40px` height with generous cell padding.

---

## 5. Control Component Standards

### 1. Form Inputs
- Every form field consists of a dedicated label and input container:
  ```xml
  <StackPanel Margin="0,0,0,16">
      <TextBlock Text="Label Text" Style="{StaticResource FormLabelStyle}" TextWrapping="Wrap"/>
      <TextBox Style="{StaticResource FluentTextBoxStyle}"/>
      <TextBlock Text="Helpful instruction or validation message" Style="{StaticResource FormHelpTextStyle}"/>
  </StackPanel>
  ```
- No placeholder-only labels. Labels remain visible when fields have values.

### 2. Card Containers
- Standardized `<Border Style="{StaticResource SurfaceCardStyle}">` with:
  - `CornerRadius="8"`
  - `BorderThickness="1"`
  - `BorderBrush="{DynamicResource BorderSubtleBrush}"`
  - Subtle drop shadow or elevation border.

### 3. Status Badges & Pills
- Status indicators (e.g. `Ready`, `Printing`, `Confirmed`, `Unverified`) use rounded pill borders with dedicated semantic background and foreground tints (e.g. Success: Green tint `#E8F5E9` with `#1B5E20` text).
