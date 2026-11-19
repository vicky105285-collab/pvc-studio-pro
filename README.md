# PVC Studio Pro

**Professional Windows Desktop PVC Card Production Suite for e-Sevai Centres & Commercial Print Shops**

[![Platform: Windows 11 / 10](https://img.shields.io/badge/Platform-Windows%2011%20%7C%2010-0078D4?logo=windows)](https://microsoft.com)
[![Framework: .NET 11](https://img.shields.io/badge/Framework-.NET%2011-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![UI: WPF UI Fluent Design](https://img.shields.io/badge/UI-Fluent%20Design%20(Mica)-0067B8)](https://github.com/lepoco/wpfui)
[![Tests: 179 Passing](https://img.shields.io/badge/Tests-179%20Passed-107C41)](tests/PVCStudioPro.Tests)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Offline: 100% Local](https://img.shields.io/badge/Operation-100%25%20Offline-green)](#privacy--compliance)

---

## 1. Overview

**PVC Studio Pro** is an independent, 100% offline Windows desktop application engineered to replace proprietary subscription-locked card printing software (such as Real PVC Card Pro). It is specifically tailored for commercial e-Sevai centres, CSC kiosks, and photo studios in Tamil Nadu and across India.

### Verified Working Features (Generated from `docs/FEATURES.json`)

All features listed below are verified Working via automated unit/integration tests and traceable from XAML through ViewModels to Core engine services:

- **600 DPI Vector PDF & Image Ingestion:** Precision vector rasterization via PDFium (`PdfService.RenderPageAsync`) and multi-format raster image decoding (PNG, JPEG, TIFF, BMP, WebP) with interactive password unlock for protected PDFs.
- **5-Stage Adaptive Card Detection:** Automatic document categorization and confidence scoring (`CardDetectionService.DetectAndExtractAdaptiveAsync`) with fallback bounds estimation.
- **29 Indian ID Card Templates:** Full coverage across 5 official categories:
  - *Identity:* e-Aadhaar (UIDAI), NSDL PAN, UTIITSL PAN, e-EPIC Voter ID (Modern & Legacy)
  - *Healthcare:* Ayushman Bharat (PM-JAY), ABHA Digital Health ID
  - *Labour & Social:* e-Shram National Database, State Labour Welfare cards
  - *State Rations:* Tamil Nadu Smart Ration Card (TNPDS) & regional PDS cards
  - *Transport:* Parivahan Driving Licence (DL) & Registration Certificate (RC)
- **6 Custom & Institutional Card Presets:** Direct selection for generic dual/single cards, student IDs, staff badges, and membership cards (`TemplateService.GetOtherCardTemplates`).
- **True Native BGRA Color Preservation:** End-to-end 32bpp BGRA pipeline (`PhysicalPageRenderService`) guaranteeing identical color rendering across WPF displays, PDF exports, and direct GDI printer spooling.
- **Advanced Crop Studio & Geometry:**
  - Interactive 8-handle crop canvas with rule-of-thirds grid
  - Sub-degree fine tilt straighten slider ($-45^\circ$ to $+45^\circ$ with $0.1^\circ$ precision)
  - Lossless exact $90^\circ$ right-angle rotations with zero pixel drift
  - 4-corner perspective unwarping via OpenCV homography (`ManualCropService.UnwarpPerspective`)
  - Aspect ratio presets (CR80 $85.60 \times 53.98\text{ mm}$, Passport $35 \times 45\text{ mm}$, Stamp $25 \times 30\text{ mm}$, Freeform, and Custom mm)
- **Physical Sheet & Tray Layout Engine:**
  - SingleSideA4 (upper dual cards with symmetrical 15mm margins)
  - DoubleSideFoldA4 (centered front and back cards with fold guide)
  - SixCardSheetA4 (3 rows $\times$ 2 columns for batch production)
  - Non-CR80 aspect ratio preservation (zero card squishing or stretching)
- **Direct Win32 GDI Hardware Spooler:** Direct unmanaged `Gdi32.StretchDIBits` driver transmission for Epson L805, EcoTank L8050/L18050, and Canon CD tray attachments without Windows print dialog distortion.
- **Hardware Tray & Offset Calibration Manager:** Independent hardware attachment profiles with millimeter $X/Y$ mechanical offset calibration and test sheet printing.
- **ICAO Biometric Passport Photo Engine:** Multi-up passport photo sheet generation ($35 \times 45\text{ mm}$) with facial alignment and customizable sheet grids.
- **Integer-Paise Financial Cash Ledger:** ACID transactional accounting stored strictly as integer paise with explicit Indian Standard Time (`Asia/Kolkata`) day boundaries.
- **Test Mode Sandbox:** Instant sandbox toggle isolating test transactions into `pvc_ledger_test.db` with zero production database contamination.
- **Local Backup & Restore:** Zero-cloud encrypted database and profile backup archive generator and restore manager.
- **Shop Profile & Branding:** Custom business details, GSTIN, and receipt header configuration.
- **Bilingual Interface:** Live dynamic switcher between English and Tamil (தமிழ்).
- **Light & Dark Theme Engine:** Full theme token parity across all views.
- **Native Memory Leases:** Thread-safe `IAssetLease<SKBitmap>` reference counting preventing use-after-free or native memory leaks.

*(Note: Features currently classified as Partial or UI-only—such as UIDAI first-8-digits redaction in the adaptive pipeline, automated batch physical spooling, and raw ESC/POS thermal receipt streaming—are documented in `docs/FEATURES.json` and scheduled for completion in subsequent phases).*

---

## 2. System Architecture

The solution follows clean architectural separation across three core layers:

```
PVCStudioPro.sln
├── src/
│   ├── PVCStudioPro.Core/          # Domain models, service abstractions & engine implementations
│   │   ├── Models/                 # CardTemplate, PrinterProfile, PrintJobRecord, PrinterCapabilities
│   │   └── Services/
│   │       ├── IPdfService.cs      # 600 DPI vector PDF rasterization & face extraction
│   │       ├── IPrintService.cs    # Multi-tray canvas composition & Win32 GDI print spooler
│   │       ├── IPrinterDiscovery.cs# Dynamic winspool.drv hardware capabilities interrogation
│   │       ├── ITemplateService.cs # Versioned JSON template engine with anchor keyword matching
│   │       ├── IVisionService.cs   # OpenCV perspective deskewing & passport photo sheet layouts
│   │       ├── ISecurityService.cs # Unmanaged memory pinning, zeroization & DPAPI encryption
│   │       └── IJobLedgerService.cs# Local SQLite accounting repository
│   │
│   └── PVCStudioPro.App/           # Windows Presentation Foundation (WPF) GUI
│       ├── I18n/                   # Resource dictionaries (Strings.en.xaml, Strings.ta.xaml)
│       ├── ViewModels/             # CommunityToolkit.Mvvm ViewModels
│       ├── Views/                  # Modern Windows 11 Fluent Views (WPF-UI with Mica backdrop)
│       └── Converters/             # Zero-copy SkiaSharp to WPF WriteableBitmap memory blitter
│
└── tests/
    └── PVCStudioPro.Tests/         # xUnit unit & empirical integration test suite
```

---

## 3. Getting Started

### Prerequisites
- Windows 10 (1809+) or Windows 11 64-bit
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or higher)

### Build & Run
```powershell
# Restore NuGet dependencies
dotnet restore

# Build Solution
dotnet build -c Release

# Run Unit Tests
dotnet test

# Launch Application
dotnet run --project src/PVCStudioPro.App/PVCStudioPro.App.csproj
```

### Build Standalone Release Package
```powershell
# Publish self-contained 64-bit binary
dotnet publish src/PVCStudioPro.App/PVCStudioPro.App.csproj -c Release -r win-x64 --self-contained true -o publish/PVCStudioPro_win-x64
```

### Local Laptop Installation
```powershell
# Installs application to %LocalAppData%\Programs\PVC Studio Pro with Desktop & Start Menu shortcuts
powershell -ExecutionPolicy Bypass -File scripts/install_local.ps1
```

---

## 4. Hardware Support

| Device Category | Supported Hardware | Tray Profile |
|---|---|---|
| **Epson Photo Inkjet** | L805, L800, L850, L8050, L18050 | `EPSON_2CARD_TRAY` (140 x 297 mm) |
| **Canon Photo Series** | G570, G670, Pixma TS/iP series | `CANON_MULTI_TRAY` (130 x 254 mm) |
| **Direct PVC Printers** | Zebra ZC300, Evolis Zenius/Primacy, Magicard, Fargo | `CR80_DIRECT` (85.60 x 53.98 mm) |
| **Lamination Heat Press** | Dragon Sheet PVC / Thermal Lamination | `DRAGON_SHEET_A4` (Mirror Mode) |

---

## 5. Security & Privacy Policy

- **No Network Telemetry:** The application operates completely offline. No analytics, tracking, or network calls are made.
- **Zero Citizen PII Retention:** No customer names, phone numbers, Aadhaar numbers, or photos are ever persisted to SQLite or disk.
- **Volatile Buffer Sanitization:** All decrypted PDF byte streams and uncompressed bitmaps in memory are zeroized using Win32 `SecureZeroMemory` after rendering.

---

## 6. License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
