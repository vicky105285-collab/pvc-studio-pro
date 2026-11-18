# ADR-001: Selection of WPF with .NET 9 and WPF UI over WinUI 3

**Status:** Accepted  
**Date:** 2026-11-18  
**Deciders:** Senior Windows Desktop Software Architect  

---

## Context
PVC Studio Pro requires a professional, modern desktop interface adhering to Windows 11 Fluent Design principles while providing deep, low-level programmatic control over physical Windows print queues, custom paper sizes, and specialized tray hardware (Epson CD/DVD tray, Canon multi-purpose tray, direct CR80 PVC card printers).

We evaluated two primary candidates from the Microsoft desktop ecosystem:
1. **WPF (.NET 9 LTS + `WPF UI`):** Mature, robust, rock-solid printing API (`System.Printing`), battle-tested over two decades, modern XAML, paired with `WPF UI` for native Windows 11 visual styling (Mica, Acrylic, NavigationView).
2. **WinUI 3 / Windows App SDK:** Microsoft's latest native desktop framework.

## Decision
We select **WPF on .NET 9 with `WPF UI`**.

## Rationale
1. **Printing Subsystem Maturity:**
   - In commercial PVC card production, printing precision is life-or-death. The application must programmatically select paper trays (such as CD/DVD slot), inject custom millimeter page dimensions, configure duplex flip orientation, and feed XPS vector spooler commands directly.
   - WPF provides direct, comprehensive access via `System.Printing.PrintQueue`, `PageMediaSize`, `PageOrientation`, and `XpsDocumentWriter`.
   - WinUI 3's printing API is heavily sandboxed, immature, and lacks low-level tray selector controls for specialized hardware without fragile Win32 P/Invoke workarounds.
2. **Visual Fidelity & User Experience:**
   - `WPF UI` by lepo.co provides pixel-perfect parity with Windows 11 Fluent Design, including native Mica window materials, dark/light theme switching, Segoe UI Variable typography, and Fluent System Icons.
3. **Deployment Friction:**
   - WinUI 3 requires the Windows App SDK runtime installer or MSIX packaging, which often faces deployment blockers on older or restricted Windows 10/11 machines in small Indian cyber cafes.
   - WPF on .NET 9 can be compiled into a self-contained, single-folder executable and packaged with an Inno Setup installer that installs in seconds without complex runtime prerequisites.

## Consequences
- Clean, maintainable XAML architecture following standard MVVM.
- Complete hardware compatibility with Epson L805, L8050, L18050, and Canon G-series drivers.
- Seamless integration with C++ native DLLs (PDFium, OpenCV).
