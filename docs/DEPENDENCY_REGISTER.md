# Dependency Register & Software Bill of Materials (SBOM)

**Target Framework:** .NET 8.0 LTS (`net8.0-windows;net8.0`)  
**Host Environment:** Windows 11 64-bit (AMD CPU)  
**SDK Version:** 8.0.428 (LTS supported through November 10, 2026)  
**Licensing Policy:** Strict exclusion of copyleft/viral licenses (GPL/AGPL); only permissive commercial licenses (MIT, Apache 2.0, BSD-3, MS-PL) permitted. Zero annual licensing royalties.

---

## 1. Verified Dependency Matrix

| Package Name | Installed Version | License | Native Assets | Role in PVC Studio Pro |
|---|---|---|---|---|
| **`Wpf.Ui`** | `4.3.0` | MIT | Managed | Windows 11 Fluent Design controls, Mica backdrop, SymbolIcon, TitleBar |
| **`CommunityToolkit.Mvvm`** | `8.4.2` | MIT | Managed (Source Generators) | High-performance MVVM architecture (`[ObservableProperty]`, `[RelayCommand]`) |
| **`Docnet.Core`** | `2.6.0` | MIT | `pdfium.dll` (Win x64/x86 bundled) | Vector PDF parsing, 600 DPI rasterization, character and text extraction |
| **`OpenCvSharp4`** | `4.10.0.20240616` | Apache 2.0 | Managed wrappers | Computer vision, GrabCut segmentation, Canny contour detection, perspective warp |
| **`OpenCvSharp4.runtime.win`** | `4.10.0.20240616` | Apache 2.0 | `OpenCvSharpExtern.dll` (Win x64) | Native OpenCV binaries compiled for Windows x64 |
| **`SkiaSharp`** | `4.157.0` | MIT | `libSkiaSharp.dll` (Win x64) | 2D canvas drawing, high-quality Mitchell bicubic sampling, text overlays |
| **`Microsoft.Data.Sqlite`** | `8.0.11` | MIT | `e_sqlite3.dll` (Win x64) | Local transaction accounting, daily KPIs, zero-PII ledger storage |
| **`Dapper`** | `2.1.89` | Apache 2.0 | Managed | Micro-ORM for high-throughput parameterized SQLite queries |
| **`System.Drawing.Common`** | `8.0.11` | MIT | Win32 GDI | `PrinterSettings.InstalledPrinters`, `DeviceCapabilities`, `CreateDC` |
| **`Microsoft.Extensions.DependencyInjection`**| `8.0.1` | MIT | Managed | Inversion of Control (IoC) and dependency lifecycle management |

---

## 2. Framework Evaluation: .NET 8 LTS vs .NET 10 LTS

- **Current Evaluation:**
  - .NET 8 LTS is the active Microsoft LTS release, supported through November 2026.
  - .NET 10 LTS is Microsoft's next scheduled LTS release (slated for late 2025/2026).
  - The installed SDK on this Windows 11 host is `.NET SDK 8.0.428`.
  - All native libraries (`pdfium.dll`, `OpenCvSharpExtern.dll`, `libSkiaSharp.dll`, `e_sqlite3.dll`) are verified and stable on `net8.0-windows`.
- **Migration Strategy:**
  - The codebase adheres to modern C# 12/13 standard idioms without deprecated APIs.
  - When .NET 10 LTS SDK is installed on the host, migration only requires updating `<TargetFramework>net10.0-windows</TargetFramework>` in the three project files without architectural rewrites.

---

## 3. Vulnerability & Security Audit

- **Commercial Licensing:** SixLabors.ImageSharp was removed due to non-permissive split licensing. SkiaSharp (MIT) and native WPF imaging are used instead.
- **Redistribution Rights:** All 10 dependencies allow commercial, royalty-free distribution.
- **Vulnerabilities:** Zero high/critical CVEs reported in NuGet restore audit.
