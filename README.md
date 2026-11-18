# PVC Studio Pro

**Professional Windows Desktop PVC Card Production Suite for e-Sevai Centres & Commercial Print Shops**

[![Platform: Windows 11 / 10](https://img.shields.io/badge/Platform-Windows%2011%20%7C%2010-0078D4?logo=windows)](https://microsoft.com)
[![Framework: .NET 8 LTS](https://img.shields.io/badge/Framework-.NET%208%20LTS-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![UI: WPF UI Fluent Design](https://img.shields.io/badge/UI-Fluent%20Design%20(Mica)-0067B8)](https://github.com/lepoco/wpfui)
[![Tests: 16 Passing](https://img.shields.io/badge/Tests-16%20Passed-107C41)](tests/PVCStudioPro.Tests)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Offline: 100% Local](https://img.shields.io/badge/Operation-100%25%20Offline-green)](#privacy--compliance)

---

## 1. Overview

**PVC Studio Pro** is an independent, 100% offline Windows desktop application engineered to replace proprietary subscription-locked card printing software (such as Real PVC Card Pro). It is specifically tailored for commercial e-Sevai centres, CSC kiosks, and photo studios in Tamil Nadu and across India.

### Key Highlights
- **Zero Recurring Fees:** 100% offline local operation without mandatory annual subscription locks.
- **600 DPI Vector Output:** Precision vector extraction via Google PDFium engine, preventing pixelation on fine text, micro-security lines, and QR codes.
- **10 Indian Identity Templates:** Instant auto-recognition and dual-face cropping for:
  - e-Aadhaar (UIDAI)
  - NSDL / UTIITSL PAN Card
  - e-EPIC (Election Commission of India Voter ID)
  - Ayushman Bharat (PM-JAY / ABHA)
  - Tamil Nadu Smart Ration Card (TNPDS)
  - Parivahan Driving Licence (DL)
  - Parivahan Registration Certificate (RC)
  - e-Shram Card
  - ABHA Health ID
  - Custom CR80 Staff / School ID Cards
- **UIDAI Masked Aadhaar Mode:** 1-click masking of the first 8 digits (`XXXX-XXXX-1234`) in strict compliance with Aadhaar regulations.
- **Hardware Tray Calibration:** Millimeter slider controls ($X$ and $Y$ offsets) with on-demand physical calibration test sheet printing to eliminate mechanical tray drift on Epson L805, L8050, L18050, and Canon printers.
- **Offline Business Ledger:** Built-in daily cash transaction accounting, card volume metrics, and 58mm/80mm ESC/POS thermal receipt printing.
- **Bilingual Interface:** Instant live toggle between English and Tamil (தமிழ்).
- **DPDP Act 2023 Compliance:** Zero-citizen-PII policy. Plaintext identity data is never stored on disk. Decrypted memory buffers are sanitized using Win32 `SecureZeroMemory` and `VirtualLock`.

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
