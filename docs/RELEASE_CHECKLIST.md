# Release Readiness Checklist — PVC Studio Pro

**Target Version:** 1.0.0-rc1  
**Branch:** `release-readiness-audit`  
**Gate Lead:** Senior Desktop Software Architect / Release Manager  
**Status:** **READY FOR RELEASE CANDIDATE PUBLICATION**  

---

## 1. Release Gate Criteria

| Category | Verification Item | Status | Notes / Evidence |
|---|---|---|---|
| **Compilation** | Clean build with zero warnings treated as fatal | **PASSED** | .NET 8.0 SDK (x64) |
| **Unit & Integration Tests** | 100% test pass rate with zero silent skips | **PASSED** | 37 of 37 passed (`dotnet test`) |
| **Template Completeness** | All 10 Indian Government ID formats initialized | **PASSED** | Aadhaar, PAN (NSDL/UTIITSL), Voter, Ayushman, TN Ration, DL, RC, e-Shram, ABHA |
| **Template Safety Gate** | Rejection of unknown docs without fallback | **PASSED** | Explicit failure returned, no silent Aadhaar fallback |
| **Photo Studio** | Offline GrabCut segmentation & 4x6" photo grid | **PASSED** | Verified in `VisionServiceTests` & integrated in UI |
| **Privacy & Security** | Zero customer identity data stored | **PASSED** | Verified via SQLite pragma schema inspection |
| **Bilingual Support** | Full English & Tamil (தமிழ்) parity | **PASSED** | `Strings.en.xaml` and `Strings.ta.xaml` |
| **Packaging** | Self-contained x64 Windows executable | **PENDING PUBLISH** | `publish/PVCStudioPro_win-x64` |
| **Hardware Caliper Test** | Physical print registration verification | **PENDING USER VERIFICATION** | Calibration sheet export ready; user test required |

---

## 2. Release Artifact Inventory

When published, the self-contained deployment bundle contains:
- `PVCStudioPro.App.exe` (WPF UI Entry point)
- `PVCStudioPro.Core.dll` (Business logic, PDFium, OpenCV, SQLite)
- Native runtime dependencies:
  - `pdfium.dll` (via `Docnet.Core`)
  - `OpenCvSharpExtern.dll` (via `OpenCvSharp4.runtime.win`)
  - `libSkiaSharp.dll` (via `SkiaSharp.NativeAssets.Win32`)
  - `e_sqlite3.dll` (via `SQLitePCLRaw.bundle_e_sqlite3`)
- `templates/` (JSON-based versioned card templates)
- `config/` (Default printer profile configurations)
- `scripts/install_local.ps1` (Automated desktop shortcut and registry setup)
