# PVC Studio Pro — Competitor Workflow Replication Matrix
## Reverse-Engineered Feature Parity Audit & Architectural Rebuild Plan

**Date:** 2026-11-19  
**Reference Competitor:** Real Softwares PVC Card Pro v8.8 (`C:\Program Files (x86)\Real Softwares\PVC Card Pro\`)  
**Auditor:** Senior Windows Desktop Software Architect  
**Methodology:** REA Skill Protocol (`.agents/skills/reverse-engineer-anything/SKILL.md`), Database Reverse Engineering (`Real.db`, `printSettings`, `Printer`), and Binary Reflection (`RealCardMaker.exe`, `RealCropTool.exe`, `RealPrintTool.exe`).

---

## 1. Feature Parity & Workflow Comparison Matrix

| Area | Competitor Functionality (Real PVC Card Pro) | Current PVC Studio Pro | Missing / Incorrect in Current Version | Required Rebuild Implementation |
| :--- | :--- | :--- | :--- | :--- |
| **1. Application Architecture & Navigation** | Single integrated workspace (`RealCardMaker.exe`) with top menu, left thumbnail panel, central sheet preview, and right printer configuration panel. No page jumping. | 14 fragmented tabs on sidebar (Document Desk, Card Studio, Crop Studio, Designer, Print Preview, etc.). | Operator is forced to navigate across 5 separate pages just to print one card. Standalone Designer is redundant and causes confusion. | **Unified Task-Oriented Workspace:** Top toolbar (Open, Save, Undo, Redo, Settings), Left panel (Document queue, detected cards, layout presets), Center canvas (True physical sheet preview), Right panel (Object properties & Print settings), Bottom status bar. |
| **2. Aadhaar PDF Import & Recognition** | Direct PDF open with iTextSharp / Ghostscript. Reads standard & custom UIDAI layouts. Immediate extraction without manual ratio selection. | Supported in Document Desk, but Card Studio previously failed on Identity-H fonts. Required manual ratio selections. | Aadhaar auto-ratio was not automatically locked to CR80 upon import; user had to correct ratios repeatedly. | **Zero-Click Aadhaar Automation:** Sniff PDF magic bytes, unlock password, visual multi-factor layout detection, automatically isolate front and back cards, lock to CR80 ($85.60 \times 53.98$ mm), and place onto physical sheet instantly. |
| **3. Password-Protected PDF Handling** | In-place modal prompt requesting 8-character password. Remembers password session. | In-place password banner in Document Desk. | Inconsistent behavior between Card Studio and Document Desk. | **Unified Decryption Dialog:** Clean in-place password prompt with show/hide toggle and instant validation. |
| **4. Automatic Front/Back Extraction** | Calibrated coordinate extraction from `Real.db` (Aadhaar: `(210, 2595, 1045, 665)` & `(1300, 2595, 1045, 665)`). | Calibrated coordinates in `CardDetectionService`, but output was sent to isolated views. | Extracted cards were not immediately visible on the final physical page. | **Instant Dual-Face Presentation:** Auto-extract front and back and immediately mount them onto the active sheet layout in the central canvas. |
| **5. Identity Document Templates** | 35 built-in template coordinate profiles in `Real.db` (Aadhaar, Child Aadhaar, M-Aadhaar, PAN NSDL, PAN UTI, Instant PAN, Voter ID, Ayushman PMJAY, UAN, E-Shram, Driving License, RC Book, etc.). | 10 template definitions in `CardTemplate.cs`. | Missing 25 secondary government card presets found in competitor. | **Full 35-Template Inventory:** Import all 35 calibrated coordinate presets directly into `TemplateService.cs` matching `Real.db` values. |
| **6. Crop Studio & Viewport Controls** | Dedicated `RealCropTool.exe` with `AadhaarMode`, fit-to-window, zoom in/out, rotate left/right, and quick commit. | Separate full-screen `CropStudioView` with complex controls; user reported excessive zoom and disorientation. | Initial view opened at 100% pixel zoom on high-DPI scans; lacked smooth auto-fit and felt detached from workflow. | **Integrated Crop Drawer / Dialog:** Opens automatically fitted to window (`FitToWindow`), mouse-wheel zoom centered on cursor ($0.1\times$ to $5\times$), 8 resize handles, CR80 aspect lock, and immediate live preview. |
| **7. Physical A4 Sheet Print Preview** | `RealPrintTool.exe` renders the entire A4 physical page (827 × 1169 hundredths of an inch = $210 \times 297$ mm) showing the exact card locations. | Displayed two cards in a scrollable container rather than the actual physical sheet as printed. | **Fundamental Defect:** Did not render the full A4 portrait paper sheet; operator could not verify sheet alignment. | **True Physical A4 Sheet Canvas:** Authoritative physical page rendering showing the full A4 sheet ($210 \times 297$ mm) fitted to the screen, showing margins, cut guidelines, and cards in exact print locations. |
| **8. Sheet Layout Presets** | 5 layouts in `printSettings`: Single Side ($X=150, 1230$), Double Side ($X=78, 78$), Double Side Center ($X=643, 643$), 4x6 ($X=55, 55$), Epson Tray ($X=135, 120$). | Partial preset support scattered across settings. | Layout arrangements were not easily switchable with one click on the main workspace. | **One-Click Layout Switcher:** Instant switching between Single Side A4 (side-by-side), Center Fold A4, Double Side Duplex, 4x6 Photo Sheet, and Epson/Canon Direct Trays. |
| **9. Hardware Tray Profiles** | 10 factory printer profiles in `Printer` (Epson Tray, Canon PVC, A4 5-Card, A4 10-Card Dragon Sheet, A4 Half-A4, 4x6 Photo, Epson PVC, Epson New Tray). | Defined in `PrinterTrayService`, but disconnected from main preview canvas. | Profile switching required navigating to "Printer & Tray Manager" instead of changing directly in the workspace. | **In-Workspace Media Profile Selector:** Select printer and media tray directly in the right panel with immediate canvas recalculation. |
| **10. Fine Calibration Offsets** | $X$ and $Y$ offset adjustments stored per printer in `PrinterData`. | Nudge buttons in `PrintPreviewApprovalViewModel`. | Offset adjustments were not visible on the main page layout. | **Direct $\pm 0.1$ mm Calibration Nudges:** Real-time nudge arrows with live millimeter update directly on the active sheet canvas. |
| **11. Image Adjustments & Filters** | `HSLFilter.dll` and `tbSaturation` slider in `RealCardMaker.exe` for ink saturation tuning. | Brightness, contrast, saturation, and sharpness in `ManualCropService`. | Controls were isolated inside Crop Studio; operator could not tune saturation directly during layout preview. | **Direct Quick-Adjustment Sliders:** Brightness, Contrast, and Saturation sliders available directly in the Right Properties Panel. |
| **12. Reverse / Mirror Printing** | `ReversePrinting` property in `RealCardMaker` for printing on transparent film for thermal dragon sheet lamination. | `MirrorMode` boolean in `PageLayoutModel`. | Missing clear visual preview of mirror mode on the A4 sheet. | **Live Sheet Mirror Toggle:** Checkbox immediately mirrors the cards horizontally on the canvas so operator sees exact thermal film orientation. |
| **13. Cutting Guidelines & Fold Lines** | Print border / cut marks checkbox (`ckprintborder`) drawing 0.5 pt cut guidelines around cards. | Implemented in `PhysicalPageRenderService`. | Did not allow dynamic toggle with instant preview on the sheet. | **Interactive Cut Guides & Fold Crease:** Toggleable 0.2 mm crop marks at 4 corners + center dashed folding line. |
| **14. Output Export (PDF & PNG)** | Export to PDF or image via virtual print driver. | `ExportLayoutToPdf` and `ExportLayoutToBitmap` in `PhysicalPageRenderService`. | Hidden behind secondary menus. | **One-Click PDF/PNG Export:** Immediate toolbar buttons to save 600 DPI print-ready PDF or PNG without spooling. |
| **15. Project Save & Reopen** | Competitor does not have a native editable project format; only saves database history. | Custom `.pvcproj` JSON schema implemented in `PhysicalPageRenderService`. | Was only accessible in the confusing standalone Designer page. | **Integrated Project Save/Open:** Save `.pvcproj` from the main top toolbar and reopen at any time to resume editing. |
| **16. Printer Discovery & Memory** | Queries `InstalledPrinters` and remembers `DefaultProfile` and last-used printer. | `PrinterDiscoveryService` implemented, but did not persist operator's default printer between sessions. | Operator had to re-select printer on application restart. | **Persistent Printer Memory:** Remembers last-selected printer, tray profile, and calibration offsets in user settings. |
| **17. Operator Approval Gate** | Confirmation before dispatching to Windows print spooler. | Implemented in `PrintPreviewApprovalViewModel`. | Separated from main layout. | **Prominent Action Button:** `START PRINTING (SPOOL TO HARDWARE)` with explicit confirmation and ledger logging. |
| **18. Visual Design & Theme System** | Windows Forms (MetroFramework) with static light styling. | WPF Mica / Fluent 2 design with Light and Dark modes. | Excessive whitespace, large cards, vertical scrolling, and low information density. | **Compact Commercial UI:** High-density, professional layout with zero vertical scrolling required for primary workflows. |
| **19. Zero-Citizen-PII Retention** | Stores customer print history and temp images in `%TEMP%` and `CardMaker\`. | Ephemeral in-memory streams with SQLite audit ledger recording only financial totals (Zero citizen numbers or photos). | None (PVC Studio Pro is superior in privacy and security). | **Enforce Strict Zero-PII Policy:** Ephemeral memory processing, scrubbed temp buffers, zero citizen data written to logs or disk. |
| **20. Self-Contained Deployment** | Requires .NET Framework 4.5.2 pre-installed on Windows. | Self-contained .NET 8 x64 desktop package. | Previous build had `--self-contained false` causing runtime prompt (now fixed to `--self-contained true`). | **Self-Contained Standalone App:** Bundles all runtime DLLs (`coreclr.dll`, `hostfxr.dll`, WPF assemblies); zero prerequisites on any Windows 11 PC. |

---

## 2. Target Workflow Map (The "2-Click" Operator Journey)

```
┌────────────────────────────────────────────────────────────────────────┐
│                        PVC STUDIO PRO WORKSPACE                        │
├────────────────────────────────────────────────────────────────────────┤
│ [Top Bar]  Open Document | Save | Undo | Redo | Template: Auto | Dark  │
├───────────────┬────────────────────────────────────────┬───────────────┤
│ [Left Panel]  │ [Center Canvas - Full Physical Sheet]  │ [Right Panel] │
│               │                                        │               │
│ Imported Doc: │  ┌──────────────────────────────────┐  │ Print Settings│
│ Aadhaar.pdf   │  │ A4 Portrait Sheet (210 x 297 mm) │  │ ───────────── │
│               │  │                                  │  │ Printer:      │
│ Detected:     │  │  ┌───────────┐   ┌───────────┐   │  │ [Epson L805 ▼]│
│ [Aadhaar Gov] │  │  │ Front     │   │ Back      │   │  │               │
│               │  │  │ 85.6x54mm │   │ 85.6x54mm │   │  │ Layout:       │
│ Layout Mode:  │  │  └───────────┘   └───────────┘   │  │ [Single Side▼]│
│ ● A4 Side-Side│  │                                  │  │               │
│ ○ A4 Fold Cent│  │                                  │  │ Copies: [ 1 ] │
│ ○ Epson Tray  │  │  (Full sheet visible without      │  │ Price:  [₹70] │
│ ○ 4x6 Photo   │  │   scrolling; Fit to Window)      │  │ Mode:   [CASH]│
│               │  │                                  │  │               │
│ [Adjust Crop] │  └──────────────────────────────────┘  │ [ START     ] │
│ [Swap Faces]  │  [Fit] [100%] [Zoom -/+] [Cut Marks ☑] │ [ PRINTING  ] │
├───────────────┴────────────────────────────────────────┴───────────────┤
│ [Status] A4 Sheet (210 x 297 mm) | Cards: 85.6 x 54 mm | Ready to Print│
└────────────────────────────────────────────────────────────────────────┘
```
