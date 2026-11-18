# Technology Decisions & Architectural Rationale (Revised)

**Document ID:** TD-001 (Revision 2.0)  
**Date:** 2026-11-18  
**Project:** PVC Studio Pro  
**Status:** Updated following Independent Architecture Review & Empirical Benchmarks  

---

## 1. Core Framework Selection: .NET 10 LTS (over .NET 9 STS)

| Criteria | .NET 9 (STS) | .NET 10 (LTS) | Decision & Justification |
|---|---|---|---|
| **Support Lifecycle** | **Expired May 2026** (18 months support) | **Supported through late 2028/2029** (3 years LTS) | **.NET 10 LTS is selected.** Targeting .NET 9 in late 2026 would mean shipping on an unmaintained, end-of-life runtime with no security updates. |
| **WPF & Windows 11 Integration** | Baseline | Enhanced Windows 11 Fluent theming, hardware acceleration, improved PerMonitorV2 high-DPI scaling. | .NET 10 provides first-class support for Windows 11 23H2/24H2 APIs. |
| **WPF UI (Lepo.co)** | Supported | Fully supported in Wpf.Ui 3.x+ | Provides native Mica, Acrylic, NavigationView, and Fluent System Icons. |

---

## 2. Rendering Resolution Decision: 600 DPI (over 1200 DPI)

| Resolution | Render Latency | RAM Footprint / Face | QR Scan Accuracy | Visual Result on Plastic | Verdict |
|---|---|---|---|---|---|
| **300 DPI** | 52.38 ms | 1.86 MB | **FAILED** | Tamil text legible; dense QR blurred | **Rejected** (Unscannable QR) |
| **600 DPI** | **51.95 ms** | **7.44 MB** | **PASSED (100%)** | Razor-sharp micro-text; pristine QR | **SELECTED (Standard)** |
| **1200 DPI** | 99.70 ms | 29.75 MB | **FAILED** | No perceptible difference on plastic | **Rejected (Bloat)** |

- **Empirical Rationale:** Physical inkjet printers (Epson L805/L8050) have a physical nozzle pitch of 180 nozzles/inch. While micro-stepping allows 5760×1440 optimized ink dots, raster input above 600 DPI provides zero optical benefit on PVC plastic while consuming 400% more RAM and causing standard optical barcode decoders to fail.

---

## 3. Dynamic Printer Discovery (over Hardcoded Profiles)

- **Previous approach:** Hardcode fixed pixel offsets for Epson L805 and Canon.
- **Revised architecture:** Query Windows Print Spooler directly via Win32 `winspool.drv` (`DeviceCapabilities` & `GetDeviceCaps`).
- **Benefit:** Discovers actual driver paper sizes, input bins, and physical hardware margins dynamically. Paired with a 2-step calibration wizard with a printed millimeter test ruler.
