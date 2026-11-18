# System Architecture — PVC Studio Pro (Revised 2.0)

**Target Platform:** Windows 10 (1809+) & Windows 11 (64-bit, x64)  
**Framework:** .NET 10 LTS / C# 13 + Windows Presentation Foundation (WPF)  
**Design System:** Windows 11 Fluent Design (`Wpf.Ui` by lepo.co + `ui-ux-pro-max`)  
**Data Policy:** Zero-PII Storage (Strict DPDP Act 2023 Compliance)  

---

## 1. High-Level Architectural Flow

```mermaid
graph TD
    subgraph UI_Layer ["Presentation Layer (WPF XAML + WPF UI)"]
        Shell["Application Shell & Modern NavigationView"]
        Views["Feature Views (CardPrint, PhotoStudio, Designer, Batch, Jobs, Calibrate)"]
        ViewModels["MVVM ViewModels (CommunityToolkit.Mvvm)"]
        Localization["Bilingual Localization (English / Tamil ResourceDictionaries)"]
    end

    subgraph Core_Services ["Application & Domain Service Layer"]
        PrintService["Printing & Tray Engine (Direct2D / Win32 winspool.drv)"]
        PdfService["Vector PDF Engine (Google PDFium Native 600 DPI)"]
        VisionService["Computer Vision Engine (OpenCVSharp4 Quad Warp)"]
        AiPhotoService["Local AI Portrait Segmentation (ONNX Runtime 8-Thread CPU)"]
        TemplateService["Dynamic Versioned JSON Card Template Registry"]
        LedgerService["Transaction Register & Daily Revenue Analytics"]
        SecurityService["Unmanaged Memory Pinner, VirtualLock & SecureZeroMemory"]
    end

    subgraph Persistence ["Persistence & Hardware Interop"]
        SQLite["SQLite Embedded Database (Non-PII Accounting Log)"]
        WinSpool["Windows Print Spooler (System.Printing / GDI / XPS)"]
    end

    Shell --> Views
    Views --> ViewModels
    ViewModels --> Core_Services
    Core_Services --> Persistence
```

---

## 2. Hardened Service Contracts

1. **`IPdfService`:** Takes `Stream` or `byte[]` in memory. Uses Google PDFium native C++ rasterizer to render vector cards at 600 DPI. Never creates intermediate decrypted files on disk.
2. **`IPrinterDiscoveryService`:** Interrogates connected Windows printers via `DeviceCapabilities` (Win32 `winspool.drv`), identifying supported bins (`DC_BINNAMES`), paper dimensions, duplex support, and physical unprintable margins.
3. **`IVisionService`:** Uses OpenCVSharp4 contour approximation (`Cv2.ApproxPolyDP`) and 4-point perspective warping (`Cv2.WarpPerspective`) to deskew photographed or scanned cards into a standardized 85.60 × 53.98 mm CR80 geometry in ~31 ms.
4. **`IAiPhotoService`:** Executes multi-threaded ONNX inference on CPU using quantized portrait models in ~1.5s with zero external cloud dependencies.
5. **`ISecurityService`:** Pins memory buffers using `VirtualLock`, executes cryptographic overwrites via `SecureZeroMemory`, and enforces the Zero-PII storage policy.
