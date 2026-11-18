# Printer Compatibility & Hardware Verification Matrix — PVC Studio Pro

**Status:** Release Candidate 1 (RC1)  
**Last Updated:** 2026-11-18  

> [!IMPORTANT]
> In accordance with engineering release discipline, software mathematical tray profiles are distinct from physical print verification. A profile is only marked **PHYSICALLY VERIFIED** after an operator prints a calibration sheet and confirms alignment with a digital caliper.

---

## 1. Hardware Compatibility Status Matrix

| Printer Model | Connection / Driver | Tray Mechanism | Software Profile ID | Mathematical Canvas Size | Registration Status |
|---|---|---|---|---|---|
| **Epson L805** | Windows Spooler / ESC/P-R | Classic 2-Card CD Tray (Type C) | `EPSON_L805_TRAY` | 140.0 × 297.0 mm (600 DPI) | **Profile Ready — Physical Test Pending** |
| **Epson EcoTank L8050** | Windows Spooler / ESC/P-R | Modern EcoTank 2-Card Tray | `EPSON_L8050_TRAY` | 140.0 × 297.0 mm (600 DPI) | **Profile Ready — Physical Test Pending** |
| **Epson EcoTank L18050** | Windows Spooler / ESC/P-R (A3+) | Wide-Format EcoTank Tray | `EPSON_L8050_TRAY` | 140.0 × 297.0 mm (600 DPI) | **Profile Ready — Physical Test Pending** |
| **Canon Pixma G570 / G670** | Windows Spooler / Canon Driver | Canon Multi-purpose Disc Tray | `CANON_G_SERIES_TRAY` | 130.0 × 240.0 mm (600 DPI) | **Profile Ready — Physical Test Pending** |
| **Standard CR80 Direct Card** (Fargo, Evolis, Zebra) | Windows Card Printer Driver | Edge-to-Edge Direct Feed | `DIRECT_CR80` | 85.60 × 53.98 mm (300/600 DPI) | **Standard CR80 Geometry Defined** |
| **A4 PVC Thermal Lamination** (Dragon Sheet) | Any Standard Color Inkjet | A4 Mirror Sheet Feed | `A4_DRAGON_SHEET` | 210.0 × 297.0 mm (600 DPI) | **Software Verified in Unit Tests** |

---

## 2. Key Physical Differences: Epson L805 vs L8050 / L18050

A critical finding from reverse-engineering physical trays:
- **Epson L805:** Utilizes a classic CD/DVD guide with mechanical insertion stop at **Y = 18.00 mm**. Tray width is 140.0 mm.
- **Epson L8050 / L18050:** Uses the revised front-loading EcoTank mechanism with revised optical registration markers and an initial tray offset at **Y = 22.50 mm**.
- Treating both machines with identical offsets results in a vertical registration error of approximately $4.5\text{ mm}$, cutting off headers or footers on PVC cards. PVC Studio Pro provides dedicated independent profiles for each hardware generation.

---

## 3. Step-by-Step Operator Caliper Calibration Procedure

To achieve sub-millimeter edge registration ($\le 0.2\text{ mm}$ error) for your specific physical printer:

### Step 1: Print Calibration Target
1. In PVC Studio Pro, navigate to **Hardware Calibration** (`/calibration`).
2. Select your printer model (e.g. *Epson L8050 EcoTank Tray*).
3. Insert a scrap PVC card or the plastic tray with paper inserted into slot 1.
4. Click **Print Calibration Sheet**.
5. The printer will output precise 1mm coordinate grid lines, crosshairs, and a nominal $85.60 \times 53.98\text{ mm}$ reference rectangle.

### Step 2: Measure Alignment Discrepancies
Using a vernier or digital caliper, measure:
1. **Horizontal Offset ($\Delta X$):** Distance between the printed left edge crosshair and the physical plastic card left edge.
2. **Vertical Offset ($\Delta Y$):** Distance between the printed top edge crosshair and the physical plastic card top edge.

$$\Delta X = X_{\text{measured}} - X_{\text{nominal}}$$
$$\Delta Y = Y_{\text{measured}} - Y_{\text{nominal}}$$

### Step 3: Enter Corrections in Live Calibration Panel
1. Adjust the **Offset X** slider by $-\Delta X\text{ mm}$.
2. Adjust the **Offset Y** slider by $-\Delta Y\text{ mm}$.
3. Click **Save Calibration Profile**.
4. The calibration values are persisted locally to `config/printer_profiles.json` and applied automatically to all future print jobs.
