# Document Page Designer — Functional & Technical Specification

**Date:** 2026-11-19  
**Branch:** `competitor-feature-parity`  
**Component:** `PVCStudioPro.App.ViewModels.DocumentPageDesignerViewModel` & `Views.DocumentPageDesignerView`  
**Author:** Senior Windows Desktop Software Architect & Graphic Pipeline Specialist  

---

## 1. Architectural Mission

The previous Document Visualizer was a passive, non-interactive bitmap display. The new **Document Page Designer** transforms this into an editable 2D desktop layout studio for Indian e-Sevai operators.

The operator can design, arrange, and fine-tune physical ID card placements on real media sheets (A4 Dragon Sheet, 4×6" photo paper, A5, Epson/Canon PVC hardware trays, and direct CR80 cards) with millimeter precision.

---

## 2. Canonical Physical Mathematics & Units

- **Base Coordinate Unit:** Millimeters (mm), stored as double precision (`double`).
- **Screen Mapping:** Screen Device-Independent Pixels (DIPs) are derived dynamically via:
  $$\text{DIPs} = \frac{\text{Millimeters} \times 96.0}{25.4}$$
- **Raster Output Mapping:**
  $$\text{Pixels} = \frac{\text{Millimeters} \times \text{DPI}}{25.4}$$
- **Viewport Independence:** Zoom factor ($0.1\times$ to $5.0\times$) and pan offsets ($T_x, T_y$) modify only the viewport matrix transform. They never alter physical millimeter coordinates or output raster generation.

---

## 3. Physical Page Canvas & Rulers

1. **Page Presets:**
   - `A4_SHEET`: $210.0 \times 297.0$ mm
   - `A5_SHEET`: $148.0 \times 210.0$ mm
   - `PHOTO_4X6`: $101.6 \times 152.4$ mm
   - `EPSON_TRAY`: $140.0 \times 297.0$ mm (2 card pockets: $Y=18.0$ mm, $Y=110.0$ mm)
   - `CANON_TRAY`: $130.0 \times 220.0$ mm (2 card pockets: $Y=25.0$ mm, $Y=115.0$ mm)
   - `DIRECT_CR80`: $85.60 \times 53.98$ mm (Direct-to-card)
   - `CUSTOM`: Arbitrary width and height
2. **Visual Canvas Guides:**
   - **Horizontal and Vertical Rulers:** Calibrated in millimeters with major ticks every 10 mm and minor ticks every 1 mm.
   - **Printable Area Boundary:** Highlighted inner boundary reflecting printer driver hardware margins.
   - **Grid Lines:** Configurable 5 mm or 10 mm background grid with snap-to-grid alignment.
   - **Center Guidelines:** Dynamic dashed center-X and center-Y alignment markers.
   - **Cut Lines:** 0.5 pt dashed gray lines indicating card cut edges.

---

## 4. Editable Object Model (`PageLayoutItem`)

Every item on the canvas is an instance of `PageLayoutItem`:
- `Id`: Unique item identifier.
- `ItemType`: `FrontCard`, `BackCard`, `PassportPhoto`, `ImportedImage`, `TextBlock`.
- `XPositionMm`, `YPositionMm`: Millimeter offset from top-left of physical page.
- `WidthMm`, `HeightMm`: Millimeter dimensions (Default CR80: $85.60 \times 53.98$ mm).
- `RotationDegrees`: $0^\circ, 90^\circ, 180^\circ, 270^\circ$.
- `LockAspectRatio`: Enforces standard CR80 ($1.5857$) or Passport ($0.7778$) ratios during resize.
- `IsLocked`: Prevents accidental drag displacement.
- `ZIndex`: Layer ordering for stacking.
- `BitmapData`: Source high-resolution image data (SkiaSharp `SKBitmap`).

---

## 5. Interaction & Manipulation Engine

- **Selection & Multi-Select:** Clicking an object displays 8 draggable bounding handles and a rotation anchor.
- **Drag & Move:** Moving selected objects with live millimeter readouts.
- **Numeric Property Editing:** Side properties panel allows direct typing of exact $X, Y, W, H$ in millimeters.
- **Alignment Suite:**
  - Align Left, Center, Right, Top, Middle, Bottom.
  - Distribute Horizontally, Distribute Vertically with equal spacing.
- **Quick Actions:** Duplicate (Ctrl+D), Delete (Del), Bring to Front, Send to Back.
- **Undo / Redo Stack:** Complete command history capturing canvas modifications.

---

## 6. Project Persistence File Format (`.pvcproj` / JSON)

Layouts can be saved as human-readable, portable JSON projects containing:
- Page configuration (`WidthMm`, `HeightMm`, `Orientation`, `Margins`, `SelectedTrayProfile`).
- Array of objects with physical millimeter transforms and base64-encoded or referenced bitmap data.
- Print configuration (DPI, calibration offsets, mirror mode).
- Capabilities: Save, Save As, Duplicate, and Revert.
