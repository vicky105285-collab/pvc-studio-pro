# Hardware Validation Plan & Physical Testing Matrix

**Standard:** ISO/IEC 7810 (ID-1 / CR80 Physical Card Specification)  
**Target Hardware:** Epson L805, Epson L8050, Epson L18050, Canon G570/G670, CR80 Thermal Printers  
**Date:** 2026-11-18  

---

## 1. Physical Hardware Separation Policy

In accordance with Section 13 of the Master Project Instruction:
> *"Never mark a printer fully validated based solely on a simulated print or successful Windows print-spooler submission. Separate automated results from actual physical hardware results."*

All validation statuses in PVC Studio Pro are classified into two strict categories:
1. **SPOOLER_VERIFIED (Automated):** The software correctly constructs the XPS canvas, sets driver DEVMODE flags (PaperSize, TrayBin, Orientation), and successfully transmits data to the Windows Spooler without errors.
2. **PHYSICAL_HARDWARE_VALIDATED (Manual):** An operator has physically loaded PVC card blanks into the tray, printed the physical test pattern, measured the printed card with a digital caliper, and verified zero edge cut-offs and scan accuracy.

---

## 2. Hardware Testing Matrix

| Printer Model | Hardware Mechanism | Required Paper Source | Test Pattern Document | Dimensional Tolerance | Front/Back Registration | Physical Validation Status |
|---|---|---|---|---|---|---|
| **Epson L805** | CD/DVD Slot (2-Card Tray) | `CD/DVD` or `CD/DVD Premium Surface` | `CALIB_RULER_CR80.pdf` | ±0.2 mm | ≤ 0.3 mm offset | **PENDING PHYSICAL TEST** (Spooler Logic Ready) |
| **Epson L8050** | Dedicated ID Card Tray Slot | `ID Card` / `Multi-Purpose Tray` | `CALIB_RULER_CR80.pdf` | ±0.2 mm | ≤ 0.3 mm offset | **PENDING PHYSICAL TEST** (Spooler Logic Ready) |
| **Epson L18050** | Wide-Format A3+ Tray Slot | `ID Card Tray` | `CALIB_RULER_CR80.pdf` | ±0.2 mm | ≤ 0.3 mm offset | **PENDING PHYSICAL TEST** (Spooler Logic Ready) |
| **Canon G570/G670** | Multi-Purpose Disc/Card Slot | `Multi-Purpose Tray` | `CALIB_RULER_CR80.pdf` | ±0.3 mm | ≤ 0.4 mm offset | **PENDING PHYSICAL TEST** (Spooler Logic Ready) |
| **CR80 Direct Card** | Automatic Hopper Feed | `CR80 (85.6 × 53.98 mm)` | `CALIB_RULER_CR80.pdf` | ±0.1 mm | Edge-to-Edge bleed | **PENDING PHYSICAL TEST** (Spooler Logic Ready) |
| **Dragon Sheet Fusing** | Rear Paper Feeder (A4 Film) | `A4 (Glossy Photo Paper)` | `DRAGON_MIRROR_A4.pdf` | ±0.5 mm | Manual alignment | **PENDING PHYSICAL TEST** (Spooler Logic Ready) |

---

## 3. Physical Hardware Test Procedure (Step-by-Step)

### Step 1: Baseline Plain-Paper Test Print
1. Cut a sheet of ordinary 80 GSM A4 paper to the exact outer dimensions of the plastic printer tray (or insert plain A4 into rear feeder).
2. Launch the **PVC Studio Pro Calibration Wizard**.
3. Select the target printer and click **"Print Millimeter Calibration Grid"**.
4. The printer feeds the paper and prints:
   - Card 1 perimeter box (85.60 × 53.98 mm).
   - Card 2 perimeter box (85.60 × 53.98 mm).
   - X-axis millimeter ruler (-10 mm to +10 mm).
   - Y-axis millimeter ruler (-10 mm to +10 mm).

### Step 2: Caliper Measurement & Offset Entry
1. Place the printed sheet over the physical plastic tray.
2. If the printed perimeter box is shifted:
   - Shifted 1.2 mm to the right: Enter `X-Offset = -1.2 mm`.
   - Shifted 0.8 mm downward: Enter `Y-Offset = -0.8 mm`.
3. Click **"Save Calibration Profile"**. Profile is stored in SQLite for this specific printer.

### Step 3: PVC Plastic Verification Print
1. Insert two blank white CR80 PVC cards into Tray Slot 1 and Slot 2.
2. Click **"Print Verification Test Card"**.
3. Inspect physical output:
   - Measure card border margin using a digital vernier caliper (must be uniform around all 4 edges).
   - Test QR code scanner app on smartphone (must scan within 1.0 second).
   - Check Tamil text legibility (no bleeding or blurriness).
4. Operator signs off the physical profile in the application.
