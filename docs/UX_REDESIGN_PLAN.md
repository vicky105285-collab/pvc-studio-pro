# PVC Studio Pro — Complete UX Redesign Plan (Phase 0.1 Revised)
**Material Design 3 / Fluent Desktop Fusion: High-Density, Commercial Ergonomics**

- **Document:** `docs/UX_REDESIGN_PLAN.md`
- **Revision:** Phase 0.1
- **Date:** 2026-11-19
- **Auditor:** Senior Windows Desktop Systems & Print Engine Architect
- **Status:** Architectural Design Baseline (Awaiting Review & Phase 1 Authorization)

---

## 1. UI Framework Evaluation & Cost-Benefit Analysis

To provide a modern visual language without destabilizing the Windows desktop application, we evaluated three options for .NET 8 WPF:

| Framework Option | Architecture & Compatibility | Accessibility & Windows 11 Native | Theming & Visual Hierarchy | Cost / Risk Assessment | Final Recommendation |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Option 1: Retain WPF-UI v3.0 + Custom M3 Token Layer** | Native .NET 8 WPF. Zero external dependencies beyond installed package. | Full support for Windows 11 Mica, Snap Layouts, high contrast, and screen readers. | Clean, high-density Fluent 2 / M3 fusion. Dynamic Light/Dark switching. | **Zero migration risk.** No XAML namespace breaks; immediate build stability. | **RECOMMENDED & ADOPTED** |
| **Option 2: MaterialDesignInXamlToolkit (v5.x)** | Complete Material Design implementation for WPF. | Good contrast, but emulates mobile Android components on Windows. | Very rich palette, but heavy shadow composition causes CPU raster lag on cheap laptops. | **High Risk.** Requires rewriting 18 Views, replacing styles, and managing competing brushes. | **REJECTED** (High migration cost, unnecessary bloat) |
| **Option 3: Hybrid (WPF-UI + MaterialDesignInXaml)** | Two complete UI libraries in one app. | Conflicting resource dictionaries; high risk of focus ring overlap. | Visual clash between Fluent 2 window chrome and Android Material buttons. | **Critical Risk.** Guarantees styling collisions and double memory footprint. | **STRICTLY FORBIDDEN** |

### Implementation Strategy:
We establish a dedicated **Material Design 3 Token ResourceDictionary (`M3Tokens.xaml`)** within the existing WPF-UI application. This enforces:
- **Typography:** Strict 5-tier type scale (Display, Headline, Title, Body, Label) using Segoe UI Variable.
- **Surface Elevation:** `SurfaceContainerLowest`, `SurfaceContainerLow`, `SurfaceContainer`, `SurfaceContainerHigh`, and `SurfaceContainerHighest`.
- **Contrast Ratios:** Mandatory $>4.5:1$ contrast for text and $>3:1$ for UI control boundaries in both Light and Dark modes.

---

## 2. Detailed Desktop Wireframes

### Workspace 1: PVC Production Studio (Normal Viewport: 1440 × 900 / 1920 × 1080)

```
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ [Logo] PVC STUDIO PRO v2.0  [Production Mode ●] | [Theme ☼/☾] [Language: தமிழ்]         [_] [□] [✕]   │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ [Top Toolbar]  [+ Open Document (Ctrl+O)]  [Adjust Crop]  [Rotate 90°]  [Swap Faces]  [Save]  [Export] │
│ Document: 2024_Aadhaar_Letter.pdf | Template: [UIDAI e-Aadhaar ▼] (Confidence: 98%) | [Unmask ☑]       │
├─────────────────────┬──────────────────────────────────────────────────────────┬───────────────────────┤
│ LEFT PANEL (280px)  │ CENTER CANVAS (Aspect-Fit True Physical Sheet)           │ RIGHT PANEL (320px)   │
│                     │                                                          │                       │
│ Extracted Faces     │  ┌────────────────────────────────────────────────────┐  │ HARDWARE SETUP        │
│ ┌─────────────────┐ │  │ PHYSICAL A4 SHEET PREVIEW (210.0 x 297.0 mm)       │  │ ───────────────────── │
│ │ FRONT FACE      │ │  │                                                    │  │ Target Printer:       │
│ │ 85.60 x 53.98mm │ │  │   Margins: L 5.0mm, T 5.0mm | Output: 600 DPI      │  │ [Epson L805 Series  ▼]│
│ └─────────────────┘ │  │                                                    │  │                       │
│ ┌─────────────────┐ │  │   ┌─────────────┐       ┌─────────────┐            │  │ Media Layout:         │
│ │ BACK FACE       │ │  │   │ Front Card  │       │ Back Card   │            │  │ [A4 Single-Side 2-Up▼]│
│ │ 85.60 x 53.98mm │ │  │   │ 85.6x54.0mm │       │ 85.6x54.0mm │            │  │                       │
│ └─────────────────┘ │  │   └─────────────┘       └─────────────┘            │  │ Media Tray / Feed:    │
│                     │  │   · · · · · · · · · · · · · · · · · · ·            │  │ [Manual Tray / Sheet] │
│ Sheet Presets       │  │                                                    │  │                       │
│ ● A4 Single-Side    │  │   (Full physical paper sheet visible at a single   │  │ Fine Calibration:     │
│ ○ A4 Center-Fold    │  │    glance; automatically centered and scaled)      │  │ X: [ 0.0] Y: [ 0.0]mm │
│ ○ Epson 2-Card Tray │  │                                                    │  │ [◄] [►] [▲] [▼] [0]   │
│ ○ Canon Multi-Tray  │  │   [+] [-] [Fit 100%] [Cut Marks ☑] [Mirror Mode ☐] │  │                       │
│ ○ 4x6 Photo Paper   │  └────────────────────────────────────────────────────┘  │ BILLING & LEDGER      │
│ ○ Direct CR80 Card  │                                                          │ Rate: [₹ 70.00]       │
│                     │  Status: ISO/IEC 7810 CR80 Standard Compliant (600 DPI)  │ Copies: [ 1 ]         │
│ [◀ Collapse Panel]  │                                                          │ Mode: [ CASH       ▼] │
│                     │                                                          │ [  START PRINTING   ] │
├─────────────────────┴──────────────────────────────────────────────────────────┴───────────────────────┤
│ [Status] Ready | Local Offline Engine | Zero Citizen PII Retention | 600 DPI Calibrated Spooler         │
└────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### Workspace 1 (Compact 1280 × 800 Viewport with Panels Collapsed)
When screen width drops below 1280px or operator clicks `[◀ Collapse]`:
- The left thumbnails minimize into a compact $48\text{ px}$ icon rail (`[F] [B]`).
- The right panel collapses to a $48\text{ px}$ print action rail (`[🖨️ Print]`).
- The center physical page canvas expands to occupy $92\%$ of screen real estate, maintaining perfect fit-to-window scaling without horizontal scrolling.

```
┌────────────────────────────────────────────────────────────────────────────────────────────────┐
│ [Logo] PVC STUDIO PRO  [Production Mode ●] | [Theme] [தமிழ்]                       [_] [□] [✕] │
├────────────────────────────────────────────────────────────────────────────────────────────────┤
│ [+ Open (Ctrl+O)] [Crop] [Rotate] [Swap] | Template: [UIDAI e-Aadhaar ▼] (98%) [Unmask ☑]     │
├───┬────────────────────────────────────────────────────────────────────────────────────────┬───┤
│[F]│  ┌──────────────────────────────────────────────────────────────────────────────────┐  │[P]│
│[B]│  │ FULL A4 PHYSICAL SHEET PREVIEW (Centered, Fit to Window, Zero Scroll)            │  │ R │
│───│  │                                                                                  │  │ I │
│[S]│  │   ┌─────────────┐                      ┌─────────────┐                           │  │ N │
│[C]│  │   │ Front Card  │                      │ Back Card   │                           │  │ T │
│[E]│  │   │ 85.6x54.0mm │                      │ 85.6x54.0mm │                           │  │───│
│   │  │   └─────────────┘                      └─────────────┘                           │  │[₹]│
│[▶]│  └──────────────────────────────────────────────────────────────────────────────────┘  │[◀]│
└───┴────────────────────────────────────────────────────────────────────────────────────────┴───┘
```

---

### Workspace 2: Passport Photo Studio (Biometric Frame & Multi-Up Sheet)

```
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ [Top Toolbar]  [+ Import Portrait]  [Detect Face]  [Reset Alignment]  [Export PNG/JPG]  [Print Sheet]   │
├──────────────────────────────────────┬─────────────────────────────────────────────────────────────────┤
│ LEFT: SINGLE 35x45mm PASSPORT FRAME  │ RIGHT: PHOTO SHEET PRINT COMPOSER (4x6 / A4)                    │
│                                      │                                                                 │
│ ┌──────────────────────────────────┐ │ Paper Size: [4x6 Photo Paper (102x152mm) ▼] Layout: [8-Up    ▼] │
│ │   (Top Crown Guide: 3-5mm)       │ │ Target Printer: [Epson L805 Series ▼]  Paper: [Glossy Photo ▼] │
│ │   - - - - - - - - - - - - - -    │ │                                                                 │
│ │           (  Face  )             │ │  ┌────────────────────────────────────────────────────────────┐ │
│ │   - - - - ( Eye Level ) - - -    │ │  │ 4x6 GLOSSY PHOTO SHEET (8-UP WITH SUB-MM CUT GUIDES)       │ │
│ │           (  Head  )             │ │  │                                                            │ │
│ │   - - - - - - - - - - - - - -    │ │  │  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐                       │ │
│ │   (Chin Base Guide: 10-14mm)     │ │  │  │ P-1  │ │ P-2  │ │ P-3  │ │ P-4  │                       │ │
│ │                                  │ │  │  └──────┘ └──────┘ └──────┘ └──────┘                       │ │
│ │   [Interactive Pan & Face Zoom]  │ │  │  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐                       │ │
│ └──────────────────────────────────┘ │  │  │ P-5  │ │ P-6  │ │ P-7  │ │ P-8  │                       │ │
│ Studio Background: [● Pure White]    │  │  └──────┘ └──────┘ └──────┘ └──────┘                       │ │
│   [○ Studio Blue] [○ Off-White Grey] │  └────────────────────────────────────────────────────────────┘ │
│ [Background Removal: Active]         │ Total Fee: [₹ 50.00]  Payment Mode: [CASH                    ▼] │
│ Straight Clean Bottom Edge (No Waves)│ [  PRINT PHOTO SHEET (SPOOL HARDWARE)                         ] │
└──────────────────────────────────────┴─────────────────────────────────────────────────────────────────┘
```

---

### Workspace 3: Batch Card Processing

```
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ [Top Toolbar]  [+ Add Files (PDF/Images)]  [+ Add Folder]  [Clear Queue]  [Process Batch to Tray]      │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ FILE QUEUE (4 Files Loaded)                                                                            │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Status   File Name                  Document Kind       Detected Layout   Front Card   Back Card       │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ [✓ Ready] Customer1_Aadhaar.pdf      Aadhaar Letter      A4 2-Up (Side)    Extracted    Extracted       │
│ [✓ Ready] Customer2_PAN_Card.pdf     Income Tax PAN      CR80 Single       Extracted    None (1-Sided)  │
│ [✓ Ready] Customer3_VoterID.jpg      Voter EPIC          A4 Center-Fold    Extracted    Extracted       │
│ [! Lock]  Customer4_Protected.pdf    Password Protected  Pending Password  --           --              │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Output Destination: [Direct Spool to Multi-Card Tray ▼]   Target: [Epson 10-Card Dragon Sheet        ▼] │
│ Spool Order: [Collate Pairs (Front + Back)]  Cut Guidelines: [Print 0.2mm Cut Lines ☑]                 │
│ Total Cards to Print: 6 CR80 Cards (3 Sheets) | Total Fee: [₹ 210.00] | [  START BATCH PRODUCTION   ] │
└────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

### Workspace 4: Business Ledger & Cashier Journal (with Multi-Payment Edit Modal)

```
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ [Top Bar]  Search: [Invoice #, Customer, Date] [Filter: All Statuses ▼] [Date: Today ▼] [Export CSV]   │
│ Day Summary: Invoices: 42 | Gross: ₹ 2,940.00 | Cash Drawer: ₹ 2,100 | UPI Digital: ₹ 840 | Void: 2   │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Invoice #       Date & Time       Document Type   Qty    Total Fee   Paid     Balance  Method  Status  │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ INV-20261119-01 19/11 09:15 AM    AADHAAR_CR80     1     ₹ 70.00    ₹ 70.00   ₹ 0.00   CASH    PAID    │
│ INV-20261119-02 19/11 09:42 AM    PHOTO_8UP        1     ₹ 50.00    ₹ 50.00   ₹ 0.00   UPI     PAID    │
│ INV-20261119-03 19/11 10:05 AM    PAN_NSDL_CR80    2     ₹ 140.00   ₹ 100.00  ₹ 40.00  SPLIT   PARTIAL │
│ INV-20261119-04 19/11 10:22 AM    AADHAAR_CR80     1     ₹ 70.00    ₹ 0.00    ₹ 0.00   CASH    VOID    │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ [EDIT TRANSACTION MODAL (Active for INV-20261119-03)]                                                  │
│ Customer Token: [Token #42          ]  Document Type: [PAN_NSDL_CR80]                                  │
│ Quantity: [ 2 ]  Unit Price: [₹ 70.00]  Promotional Discount: [₹ 0.00 ]  Net Total: [₹ 140.00]         │
│ Payments Recorded:                                                                                     │
│   • 19/11 10:05 AM — ₹ 100.00 (CASH)                                                                   │
│   • [+ Record Payment]: Amount: [₹ 40.00 ] Method: [UPI ▼] Ref: [UPI-TXN-984214]  [Add Payment]       │
│ Mandatory Operator Change Reason: [Customer paid balance amount via GPay GID                           │
│ [  SAVE AUDITABLE CORRECTION  ]    [  VOID / CANCEL TRANSACTION  ]    [  CANCEL  ]                     │
└────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

### Workspace 5: Printer & Media Tray Management

```
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ [Top Bar]  [+ Add Printer Profile]  [Detect New Printers]  [Calibrate Offset Test Pattern]              │
├──────────────────────────────────────┬─────────────────────────────────────────────────────────────────┤
│ INSTALLED PRINTER PROFILES           │ HARDWARE CALIBRATION & MEDIA PROPERTIES                         │
│                                      │                                                                 │
│ ● Epson L805 Series (Default)        │ Profile Name: [Epson L805 Series (2-Card Tray)                ] │
│   • Tray: Epson Direct 2-Card Tray   │ Windows Driver Queue: [EPSON L805 Series                      ▼]│
│   • X: +0.2mm, Y: -0.1mm             │ Tray Media Geometry: [Epson 2-Card Aftermarket Tray (140x297mm)▼]│
│                                      │                                                                 │
│ ○ Canon G3010 Multi-Card             │ Slot 1 (Front Card Offset): X: [ 27.2 mm] Y: [ 18.0 mm]         │
│   • Tray: Canon G-Series Multi Tray  │ Slot 2 (Back Card Offset):  X: [ 27.2 mm] Y: [110.0 mm]         │
│   • X: 0.0mm, Y: 0.0mm               │                                                                 │
│                                      │ Calibration Fine Tuning:                                        │
│ ○ Brother T420W A4 Paper Sheet       │ X Offset: [ +0.2 mm]  [◄ -0.1] [► +0.1]                         │
│   • Tray: A4 Standard Sheet Paper    │ Y Offset: [ -0.1 mm]  [▲ -0.1] [▼ +0.1]                         │
│   • X: 0.0mm, Y: 0.0mm               │                                                                 │
│                                      │ [  PRINT CALIBRATION VERIFICATION TEST PATTERN                ] │
│ [Set as Default Printer Profile]     │ [  SAVE HARDWARE PROFILE                                       ] │
└──────────────────────────────────────┴─────────────────────────────────────────────────────────────────┘
```

---

### Workspace 6: Settings & Preferences

```
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ APPLICATION PREFERENCES & ENVIRONMENT MODE                                                             │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Operation Mode:                                                                                        │
│ [● Production Mode (Live Database: pvc_studio_prod.db)]                                                 │
│ [○ Test Mode Sandbox (Isolated Simulation: pvc_studio_test.db)]                                        │
│ Note: Switching to Test Mode isolates all transaction, invoice, and calibration records.              │
│                                                                                                        │
│ User Interface & Language:                                                                             │
│ Theme: [System Default ▼] (Options: Dark Mode, Light Mode, Follow Windows)                             │
│ Language: [English (United States) ▼] (Options: English, தமிழ் - Tamil)                                 │
│                                                                                                        │
│ Hardware & Security:                                                                                   │
│ Default Billing Fee per CR80 Card: [₹ 70.00 ]                                                          │
│ Default Passport Photo Sheet Fee:  [₹ 50.00 ]                                                          │
│ Citizen Data Retention: [Strict Zero-PII (In-Memory Only, Temp Purged on Close) ☑]                     │
│                                                                                                        │
│ System Maintenance:                                                                                    │
│ Database Backup: [Create Instant ZIP Backup Now]  Database Restore: [Restore Database from Backup]     │
│ [  SAVE PREFERENCES  ]                                                                                 │
└────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```
