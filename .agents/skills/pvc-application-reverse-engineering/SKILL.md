---
name: pvc-application-reverse-engineering
description: Rigorous, evidence-based reverse engineering, UI workflow reconstruction, and feature parity analysis of Windows desktop PVC card and photo printing applications. Use when auditing competitor software, reverse engineering Windows desktop workflows, inspecting binaries, decompiling managed metadata, or mapping hardware print settings.
metadata:
  version: "1.0.0"
  author: "PVC Studio Pro Architecture Team"
---

# PVC Application Reverse-Engineering Skill

This skill provides an evidence-based, legally compliant, and technically rigorous methodology for reverse engineering Windows desktop printing and document-processing software (specifically PVC card, ID card, and photo maker applications).

---

## 1. Core Operating Principles

1. **Evidence-First Verification:** Never claim a feature exists, is understood, or is implemented without primary evidence (decompiled symbols, database schemas, UI screenshots, raster measurements, or runtime traces).
2. **Usability Over Existence:** A visible button or XAML control is never proof of functionality. A passing unit test is never proof of UI usability or driver compatibility.
3. **Canonical Status Taxonomy:** Every inspected and implemented feature must strictly receive one of these five statuses:
   - `VERIFIED`: Confirmed working end-to-end with automated test coverage and visual/interactive verification.
   - `PARTIALLY VERIFIED`: Backend logic verified, but UI interaction or hardware driver pending physical testing.
   - `FAILED`: Attempted implementation or reference workflow that fails specification or execution.
   - `NOT IMPLEMENTED`: Feature identified in reference software but not yet built in target application.
   - `NOT VERIFIED`: Reference behavior observed but underlying mechanism not independently proven.
4. **Zero-PII & Clean-Room Boundaries:** Never extract, store, log, or leak actual customer identity documents, Aadhaar numbers, biometric photos, or proprietary licensing keys. All testing must use clean-room synthetic fixtures. Never copy proprietary source code, obfuscated code, or trademarked visual assets.

---

## 2. Investigation Protocol

### 2.1 Black-Box Inspection of Windows Desktop Applications
- Analyze directory topologies, embedded PE resources, dependency trees, and external configuration databases (SQLite, XML DiffGrams, INI files).
- Trace process creation, command-line arguments, inter-process communication (IPC), and child helper executables (`CropTool.exe`, `PhotoMaker.exe`, `PrintTool.exe`).
- Inspect Windows message handling, GDI/GDI+ rendering pipelines, and print spooler interaction (`PrintDocument`, `DEVMODEW`, `XPS`).

### 2.2 Workspace and Menu Inventory
- Systematically traverse the reference application's shell, primary menus, ribbon bars, floating tool windows, context menus, and shortcut keys.
- Document every workspace mode (e.g. Card Studio, Document Desk, Crop Editor, Photo Maker, Print Setup, Tray Calibration, Sales Ledger).

### 2.3 Workflow Reconstruction
- Document the end-to-end operator journey for real shop tasks:
  `Customer File Import -> Auto/Manual Recognition -> Decryption/Password -> Rectification/Crop -> Page Layout & Design -> Printer/Tray Selection -> Physical Alignment Spooling -> Receipt/Ledger Record`.

### 2.4 Interaction and Dialog Inspection
- Document modal dialogs, error handlers, password prompts, file open filters, and slider interactions.
- Record default values, min/max ranges, tick frequencies, and keyboard accelerators.

### 2.5 Screenshot and Visual Comparison
- Capture high-DPI screenshots of reference dialogs, rulers, handles, selection adorners, and print previews.
- Compare layout geometry, margin indicators, and visual hierarchy against the target application.

### 2.6 Import/Export Behaviour Analysis
- Inspect file format sniffing, magic bytes validation, multi-page handling, and password retry limits.
- Verify whether documents are converted to raster or vector intermediate models.

### 2.7 Print Workflow Analysis
- Trace how document coordinates are mapped to physical paper:
  - Canonical unit definition (millimeters vs 1/100 inch vs device points).
  - Driver printable area vs physical page size.
  - Front-and-back duplex orientation and tray slot registration.

### 2.8 Printer & Media Configuration Analysis
- Inspect tray configurations (Epson 2-card trays, Canon multi-trays, direct thermal CR80, A4 sheets, 4x6" photo paper).
- Extract factory millimeter offsets, slot coordinate matrices, and hardware gap parameters.

### 2.9 Feature Parity Mapping & Traceability
- Maintain a bidirectional traceability matrix linking reference feature IDs to target ViewModels, Views, Core Services, and automated test fixtures.

---

## 3. Mandatory Prohibitions

1. **NO UNVERIFIED CLAIMS:** Never state that a feature has been reverse-engineered without documenting the exact class, database row, or byte sequence.
2. **NO PLACEHOLDER PASSES:** Never mark a feature complete because a button exists on the screen.
3. **NO UNIT TEST CONFLATION:** Unit tests verifying mathematical formulas do not prove that mouse drag handles work or that buttons remain readable on hover.
4. **NO PROPRIETARY ASSET REUSE:** Do not extract proprietary icons, banners, branding, or decompiled C# source files directly into the repository. Re-implement cleanly using modern .NET architectures (WPF, SkiaSharp, OpenCvSharp).
5. **NO REAL IDENTITY DATA:** Never commit or process real Aadhaar letters, PAN cards, or customer portraits.
6. **NO PREMATURE 100% PARITY DECLARATIONS:** Maintain honest gap disclosure until all listed capabilities have been independently demonstrated.
