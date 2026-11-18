# Design System & Visual Hierarchy — PVC Studio Pro

**Design Standard:** Windows 11 Fluent Design System + `ui-ux-pro-max` + `accessibility-as-code`  
**Target Platform:** Windows 10 & 11 Desktop  
**Primary Typography:** `Segoe UI Variable`, `Segoe UI`, `Nirmala UI` (Tamil Typography)  
**Date:** 2026-11-18  

---

## 1. Visual Hierarchy & Philosophy

PVC Studio Pro is built for daily commercial operation at high throughput. The design prioritizes:
1. **Zero Clutter:** High-frequency actions (Document Load, Auto-Crop, Tray Selection, Print) are accessible in under 2 clicks.
2. **True Visual Preview:** The central canvas renders the card front and back exactly at the physical CR80 aspect ratio (1.5858) with visual millimeter grid overlays and border radius (3.18 mm).
3. **Ergonomic Contrast:** Strict adherence to WCAG 2.1 AA (minimum 4.5:1 body text contrast, 3:1 graphical elements).
4. **Native Fluent Aesthetics:** Windows 11 Mica material backdrop, rounded corners, subtle elevation depth, and Fluent System Icons.

---

## 2. Color Palette & Semantic Tokens

### 2.1 Light Theme Tokens (Daylight e-Sevai Operation)
| Token Name | Hex Value | Semantic Usage | Contrast against Surface |
|---|---|---|---|
| `SurfaceBackground` | `#F3F3F3` | Application window root background | — |
| `SurfaceCard` | `#FFFFFF` | Work area cards, preview panels, settings containers | — |
| `SurfaceCardStroke` | `#E5E5E5` | 1px border around cards and input controls | 1.15:1 |
| `TextPrimary` | `#1A1A1A` | Main headers, citizen names, critical field labels | 13.8:1 (AAA) |
| `TextSecondary` | `#5C5C5C` | Sub-labels, helper hints, tray parameters | 5.9:1 (AA) |
| `TextDisabled` | `#9E9E9E` | Disabled buttons and inactive controls | 2.5:1 |
| `AccentPrimary` | `#0067B8` | Primary "PRINT NOW", active tab indicator | 5.2:1 (AA) |
| `AccentHover` | `#005DA6` | Button hover state | — |
| `StatusSuccess` | `#107C41` | "Verification Passed", "Print Spooled", "QR Valid" | 5.8:1 (AA) |
| `StatusWarning` | `#BC4B00` | "Media Size Mismatch", "Password Required" | 4.8:1 (AA) |
| `StatusError` | `#A80000` | "Paper Jam", "Invalid PDF Password", "Tray Drift" | 7.1:1 (AAA) |

### 2.2 Dark Theme Tokens (Night / Low-Fatigue Mode)
| Token Name | Hex Value | Semantic Usage | Contrast against Surface |
|---|---|---|---|
| `SurfaceBackground` | `#202020` | Application window root background | — |
| `SurfaceCard` | `#2B2B2B` | Work area cards, preview panels, settings containers | — |
| `SurfaceCardStroke` | `#383838` | 1px border around cards | 1.25:1 |
| `TextPrimary` | `#FFFFFF` | Main headers, citizen names, labels | 15.2:1 (AAA) |
| `TextSecondary` | `#D1D1D1` | Sub-labels, metadata hints | 10.4:1 (AAA) |
| `AccentPrimary` | `#4CC2FF` | Active accent, primary action button fill | 9.8:1 (AAA) |
| `StatusSuccess` | `#6CCB5F` | Success badges, validation indicators | 8.2:1 (AAA) |
| `StatusWarning` | `#FCE100` | Warning notifications | 12.1:1 (AAA) |
| `StatusError` | `#FF99A4` | Error banners | 8.6:1 (AAA) |

---

## 3. Typography & Tamil Script Hierarchy

| Hierarchy Role | Font Family | Size (pt) | Weight | Line Height | Usage |
|---|---|---|---|---|---|
| **Header 1** | `Segoe UI Variable Display` | 20pt | SemiBold | 28pt | Window page headers (e.g. "Card Printing Studio") |
| **Header 2** | `Segoe UI Variable Text` | 16pt | SemiBold | 22pt | Section headers (e.g. "Tray Configuration") |
| **Subtitle** | `Segoe UI Variable Text` | 13pt | Medium | 18pt | Card titles, modal titles |
| **Body (English)** | `Segoe UI Variable Text` | 11pt | Regular | 16pt | Standard UI text, labels, dropdown items |
| **Body (Tamil)** | `Nirmala UI` | 11.5pt | Regular | 17pt | Tamil UI labels (e.g. "அட்டை அச்சிடுதல்") |
| **Caption / Meta**| `Segoe UI Variable Text` | 9.5pt | Regular | 13pt | DPI indicators, physical millimeter dimensions |
| **Micro / Status** | `Segoe UI Variable Text` | 8.5pt | SemiBold | 12pt | Badges, cut-line markers, version labels |

---

## 4. Layout Grid, Spacing & Component Ergonomics

- **8-Point Base Multiplier:** All margins and paddings follow `4px`, `8px`, `12px`, `16px`, `24px`, `32px`.
- **Target Sizes (Accessibility Enforced):**
  - Minimum button height: `44px` (compliant with touch and rapid mouse clicking).
  - Primary "PRINT NOW" button: `56px` height with prominent icon and accelerator key `Ctrl+P`.
  - Text input boxes: `40px` height with visible border and focus ring (`#0067B8` 2px solid).
- **Corner Radii:**
  - Standard buttons & inputs: `4px`
  - Cards & containers: `8px`
  - Floating dialogs & flyouts: `12px`
  - Card Preview Canvas: Exact `3.18mm` (CR80 standard physical corner radius).
