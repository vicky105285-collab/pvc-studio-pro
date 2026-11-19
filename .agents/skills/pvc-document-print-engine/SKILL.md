---
name: pvc-document-print-engine
description: High-fidelity document rendering, physical print geometry, printer driver capability discovery, and true print preview layout composition for PVC ID cards, photo sheets, and large documents. Use when engineering WPF/SkiaSharp print pipelines, page visual designers, virtual tray preview renderers, or printer offset calibration engines.
metadata:
  version: "1.0.0"
  author: "PVC Studio Pro Architecture Team"
---

# PVC Document Print Engine & Visual Layout Skill

This skill governs the physical mathematics, color pipelines, driver abstractions, and rendering architectures required to achieve bit-accurate, millimeter-exact printing of ID cards and photo sheets on Windows.

---

## 1. Single Source of Truth Architectural Principle

The **Visual Designer Canvas**, the **Saved Project Layout**, the **Interactive Print Preview**, the **Exported PDF/PNG**, and the **Windows Print Job** must share **ONE unified layout calculation engine**:

```
                        ┌──────────────────────────────┐
                        │   Physical Layout Model      │
                        │   - Canvas Size (mm)         │
                        │   - Printable Area (mm)      │
                        │   - Object Placements (mm)   │
                        │   - Margins & Offsets (mm)   │
                        │   - Target Output DPI        │
                        └──────────────┬───────────────┘
                                       │
         ┌─────────────────────────────┼────────────────────────────┐
         ▼                             ▼                            ▼
┌──────────────────┐          ┌──────────────────┐         ┌──────────────────┐
│ Interactive Page │          │ True Print       │         │ GDI+ / XPS       │
│ Designer Canvas  │          │ Preview Raster   │         │ Windows Spooler  │
│ (Screen DIPs)    │          │ (150/300 DPI)    │         │ (600/1200 DPI)   │
└──────────────────┘          └──────────────────┘         └──────────────────┘
```

**Rule:** Never independently compute card coordinates or margins in preview vs. printing. Both must consume identical physical layout descriptions.

---

## 2. Canonical Coordinate System

1. **Internal Storage Unit:** All canvas coordinates, margins, card widths, heights, offsets, and gaps must be defined in **Millimeters (mm)** as double-precision floating-point numbers.
2. **Standard Conversion Constants:**
   $$\text{MmPerInch} = 25.4$$
   $$\text{DIPs (WPF)} = \frac{\text{Millimeters} \times 96.0}{25.4}$$
   $$\text{Device Pixels} = \frac{\text{Millimeters} \times \text{DPI}}{25.4}$$
3. **Viewport Scale Isolation:** Zooming and panning on screen only alter the WPF `ScaleTransform` and `TranslateTransform`. They must **NEVER** alter the physical millimeter coordinates or output raster pixels.

---

## 3. Physical Media Geometry Specifications

| Media Profile | Physical Sheet Size (mm) | Standard Card Placement | Cut Lines / Margin Notes |
| :--- | :--- | :--- | :--- |
| **CR80 Direct PVC** | $85.60 \times 53.98$ mm | Full Bleed ($0, 0$) | Edge-to-edge dye-sublimation; 0 margin |
| **Epson 2-Card Tray** | $140.0 \times 297.0$ mm | Slot 1: $Y=18.0$, Slot 2: $Y=110.0$ | Hardcoded slot pocket cutouts |
| **Canon Multi-Tray** | $130.0 \times 220.0$ mm | Slot 1: $Y=25.0$, Slot 2: $Y=115.0$ | Centered horizontal alignment |
| **4x6" Photo Sheet** | $101.6 \times 152.4$ mm | 2 Cards ($Y=8.0, Y=70.0$) or 8 Passport Photos | 0.5 pt light-gray cutting guidelines |
| **A4 Sheet (5 Card)** | $210.0 \times 297.0$ mm | 5 Cards stacked vertically ($Y=15, 68, 121, 174, 227$) | 0.5 pt dashed cutting guidelines |
| **A4 Dragon Sheet** | $210.0 \times 297.0$ mm | 10 Cards ($2 \times 5$ grid), Mirror mode | Thermal lamination alignment marks |
| **A4 Half-A4 Fold** | $210.0 \times 297.0$ mm | Front & Back side-by-side on upper $148$ mm | Center fold crease indicator line |

---

## 4. Printer Driver Integration & Spooler Mechanics

1. **Hardware Discovery:** Query `PrinterSettings.InstalledPrinters` and `PrintQueue` via `System.Printing`.
2. **Printable Area vs Physical Size:**
   - Always query driver `PageImageableArea` (OriginX, OriginY, ExtentWidth, ExtentHeight).
   - If the driver restricts margins, apply compensation offsets:
     $$\text{SpoolerX} = \text{TargetX}_{\text{mm}} - \text{DriverMarginLeft}_{\text{mm}} + \text{CalibrationOffsetX}_{\text{mm}}$$
3. **Explicit Operator Approval:** A print job must never be sent to the Windows spooler directly from an editing action. An explicit confirmation step (`Start Printing`) is mandatory.

---

## 5. Visual Fidelity & Color Management Disclosure

1. **RGB vs CMYK Gamut Boundary:**
   - Display colors on sRGB computer monitors cannot perfectly duplicate subtractive ink output on physical PVC cards.
   - Explain in the UI: *"Monitor preview simulates layout and alignment. Physical ink density depends on printer driver media type and paper profile."*
2. **Interpolation Quality:**
   - Use high-quality bicubic resampling (`SKCubicResampler.Mitchell`) when scaling card faces to 600 DPI target resolution ($2022 \times 1275\text{ px}$).
