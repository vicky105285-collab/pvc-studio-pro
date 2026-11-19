# PVC Studio Pro — Actual Feature Parity Matrix
**Forensic Comparison: Real PVC Card Pro v8.8 vs. PVC Studio Pro Baseline**

- **Evaluation Date:** 2026-11-19
- **Auditor:** Senior Windows Desktop Systems & Print Engine Architect
- **Status Taxonomy:**
  - **A: Not implemented** — Feature completely absent from codebase and UI.
  - **B: Source code exists but is not connected to the UI** — Underlying service/logic exists in Core, but no ViewModel/View wires it.
  - **C: UI exists but functionality is incomplete** — Control or button is visible, but functionality fails, mocks data, or is partially built.
  - **D: Implemented but uses a separate/duplicated implementation** — Feature works in an isolated prototype, disconnected from the primary workflow.
  - **E: Integrated and functionally tested** — Wired end-to-end and passes automated test harness.
  - **F: Visually verified in the running application** — Visually verified running live in the compiled application on Windows 11.
  - **G: Verified through an actual hardware/printer workflow** — Spooled to a physical printer or hardware print driver and physically measured.

---

## 1. Comprehensive 24-Area Feature Inventory

| Area # | Feature Category | Competitor Capability (`Real PVC Card Pro v8.8`) | PVC Studio Pro Screen / View | ViewModel | Service / Implementation | Honest Status | Forensic Findings & Evidence |
| :---: | :--- | :--- | :--- | :--- | :--- | :---: | :--- |
| **1** | **Document Import & Recognition** | Direct PDF/Image open via Ghostscript & iTextSharp. Automatic text sniffing. | `ProductionWorkspaceView` & `DocumentDeskView` | `ProductionWorkspaceViewModel` & `DocumentDeskViewModel` | `DocumentImportService`, `PdfDocumentService`, `DocumentDetectionService` | **F** | Sniffs magic bytes (PDF, JPG, PNG, TIFF). Correctly checks PDF header `%PDF-`. Text inspection detects keywords ("Aadhaar", "Income Tax"). |
| **2** | **Aadhaar PDF Processing** | Direct password unlock, renders e-Aadhaar PDF at 300 DPI, handles Identity-H fonts. | `ProductionWorkspaceView` | `ProductionWorkspaceViewModel` | `PdfDocumentService`, `CardDetectionService` | **C** | Renders via Docnet/PDFium. However, assumes fixed A4 height ($297\text{ mm}$); if scan dimensions vary, deduced DPI is wrong and extraction fails. |
| **3** | **Password-Protected PDFs** | Modal dialog prompting for 8-character password with unlock retry. | `ProductionWorkspaceView` | `ProductionWorkspaceViewModel` | `PdfDocumentService.TryUnlockPasswordAsync` | **F** | In-place password banner appears when `IsPasswordPromptVisible = true`. Successfully unlocks AES-128/256 password-protected PDFs. |
| **4** | **Front/Back Detection & Extraction** | Static pixel extraction from `Real.db` (`fx=210, fy=2595, fw=1045, fh=665` / `bx=1300, by=2595, bw=1045, bh=665`). | `ProductionWorkspaceView` | `ProductionWorkspaceViewModel` | `CardDetectionService.DetectAndExtractDualCardsAsync` | **C** | Uses static box subsets (`ExtractSubset`). **Zero contour or OpenCV edge detection** is performed. Stretches $88.48\text{ mm}$ crop into $85.60\text{ mm}$ CR80, distorting aspect ratio. |
| **5** | **PAN, Voter ID & Other Document Types** | Separate coordinate presets for NSDL PAN, UTI PAN, Voter EPIC v2, Ayushman, e-Shram. | `ProductionWorkspaceView` | `ProductionWorkspaceViewModel` | `CardDetectionService.DetectAndExtractDualCardsAsync` | **C** | Hardcoded switch cases exist for `Pan`, `VoterId`, `AyushmanBharat`, `EShram`, but secondary variants (Instant PAN, Baal Aadhaar, PM-SYM) are ignored. |
| **6** | **All Competitor Templates** | 35 templates stored in `Real.db` `Real` table, selectable via menus. | `TemplateManagerView` (Isolated) | `TemplateManagerViewModel` | `TemplateService` (29 templates registered) | **B** | **Severe Disconnect:** 29 templates exist in `TemplateService.cs`, but `ProductionWorkspaceViewModel` **never calls `_templateService`**. The main workspace has no template dropdown. |
| **7** | **Crop, Zoom, Pan & Rotation** | Dedicated `RealCropTool.exe` with Fit-to-Window, wheel zoom, pan, 8 resize handles, AadhaarMode. | `CropStudioView` & `ProductionWorkspaceView` | `CropStudioViewModel` & `ProductionWorkspaceViewModel` | `ManualCropService` | **D** | **Fragmented:** `CropStudioViewModel` has interactive pan/zoom/handles, but is isolated; `ProductionWorkspaceViewModel` has an in-place modal with raw text boxes. Neither shares state. |
| **8** | **Image Editing & Background Processing** | HSLFilter saturation slider (`tbSaturation`), brightness, contrast, sharp edge enhancement. | `ProductionWorkspaceView` | `ProductionWorkspaceViewModel` | `ManualCropService.ApplyAdjustments` | **C** | Brightness, contrast, and saturation sliders exist in right panel, but live update is laggy; no sharpness/denoise filters; no HSL color tuning. |
| **9** | **Passport Photograph Management** | Dedicated `RealPhotoMaker.exe` with face alignment guides, background selection, 35x45mm frame. | `PhotoStudioView` | `PhotoStudioViewModel` | `VisionService.ProcessPassportPhotoAsync` | **C** | Face positioning cannot be adjusted interactively; GrabCut leaves wavy scallops at the bottom edge; no portrait lighting adjustment. |
| **10** | **Photo Sheet Generation** | 4x6 (8-up), A4 (16-up, 32-up) photo sheets with hairline dashed cut borders. | `PhotoStudioView` | `PhotoStudioViewModel` | `VisionService.ComposePassportPhotoSheet` | **C** | Generates 8, 16, 32-up sheets via SkiaSharp. However, layout is hardcoded in `VisionService`, does not use `PageLayoutModel`, and cut marks cannot be customized. |
| **11** | **Paper & PVC Tray Layouts** | 5 layouts in `printSettings`: Single Side, Double Side, Center Fold, 4x6, Epson Tray. | `ProductionWorkspaceView` | `ProductionWorkspaceViewModel` | `PageLayoutModels.cs` & `PhysicalPageRenderService` | **D** | Implemented as inline switch cases inside `ProductionWorkspaceViewModel.BuildPhysicalSheetLayout`. Duplicated across `PrintPreviewApprovalViewModel` and `PageDesignerViewModel`. |
| **12** | **Printer Discovery & Settings** | Discovers all Windows print queues, remembers last used printer and tray configuration. | `ProductionWorkspaceView` | `ProductionWorkspaceViewModel` | `PrinterDiscoveryService` | **E** | Enumerates installed printers via `PrinterSettings.InstalledPrinters`. Dropdown binds correctly. Does not yet persist user's default printer to settings. |
| **13** | **Printer Calibration** | $X/Y$ offset adjustments stored per printer in `PrinterData` binary file. | `CalibrationStudioView` | `CalibrationStudioViewModel` | `PrinterTrayService` & `ProductionWorkspaceViewModel` | **C** | Nudge buttons exist ($\pm 0.1\text{ mm}$), but offsets are stored in ViewModel memory and are not persisted per printer device. |
| **14** | **Physical Page Preview** | `RealPrintTool.exe` displays full A4 ($210 \times 297\text{ mm}$) sheet with margins and fold lines. | `ProductionWorkspaceView` | `ProductionWorkspaceViewModel` | `PhysicalPageRenderService.RenderLayoutToBitmap` | **F** | Renders full A4 sheet ($210 \times 297\text{ mm}$) or Epson Tray ($140 \times 297\text{ mm}$) with drop shadow, margins, and cut marks. Visually verified. |
| **15** | **PDF & Image Export** | Direct export to print-ready vector PDF or high-DPI image via virtual driver. | `ProductionWorkspaceView` | `ProductionWorkspaceViewModel` | `PhysicalPageRenderService.ExportLayoutToPdf` | **E** | Generates 600 DPI PDF using SkiaSharp PDF canvas (`SKDocument.CreatePdf`). Fully functional. |
| **16** | **Batch Processing** | Batch card extraction from folder of PDFs/images into auto-named card files. | `BatchProcessingView` | `BatchProcessingViewModel` | `BatchProcessingService` | **C** | Can process a queue of files to an output folder. Cannot spool batch queues directly to the physical print engine or dragon sheet array. |
| **17** | **Project Save / Reopen** | Competitor only records database history; does not support native `.pvcproj` projects. | `ProductionWorkspaceView` | `ProductionWorkspaceViewModel` | `PhysicalPageRenderService.SaveProjectAsync` | **E** | Saves and loads complete layout state to/from JSON `.pvcproj` format with embedded Base64 image payloads. |
| **18** | **Business Profile** | Stores shop name, address, contact, and header branding for reports. | `BusinessProfileView` | `BusinessProfileViewModel` | `BusinessProfileService` | **E** | Saves shop identity and GSTIN to SQLite. Persists across application restarts. |
| **19** | **Financial Transaction Ledger** | Simple daily sales counter without audit trail or editing. | `JobLedgerView` | `JobLedgerViewModel` | `JobLedgerService` | **C** | Inserts print jobs via Dapper. **Read-only interface**. Cannot edit amounts, cannot record partial payments, cannot void or cancel transactions. |
| **20** | **Daily / Monthly Reporting** | Basic sales counts and income summary. | `ReportsAnalyticsView` | `ReportsAnalyticsViewModel` | `JobLedgerService.GetDailySummaryAsync` | **C** | Displays counts and cash/UPI split in cards. Cannot export to CSV/Excel; cannot filter by custom date ranges. |
| **21** | **Receipt Generation** | Competitor does not support receipt printing. | None | None | None | **A** | **Not implemented**. No receipt generation, thermal POS 80mm/58mm format, or customer billing invoice. |
| **22** | **Backup & Recovery** | Manual file copying of `Real.db` and `CardMaker\`. | `BackupRestoreView` | `BackupRestoreViewModel` | `BackupService` | **E** | Backs up SQLite database and configurations into a compressed ZIP file. Tested and verified. |
| **23** | **Settings & Localization** | Static English Windows Forms UI. | `SettingsView` & Shell Header | `SettingsViewModel` & `MainViewModel` | WPF ResourceDictionaries (`Strings.en.xaml`, `Strings.ta.xaml`) | **F** | Dynamic English/Tamil switching and Dark/Light theme switching functioning properly. |
| **24** | **Keyboard Shortcuts & Usability** | Standard Windows menu shortcuts (Ctrl+O, Ctrl+P, F5). | `MainWindow` | `MainViewModel` | XAML `KeyBindings` | **C** | Ctrl+O and Ctrl+P exist on top bar, but canvas keyboard navigation (arrow nudging, Esc to cancel drawer, Enter to print) is missing. |

---

## 2. Summary Breakdown by Implementation Status

```
Status Category                                Count    Percentage
─────────────────────────────────────────────────────────────────
A: Not implemented                                1        4.2 %
B: Code exists but disconnected from UI           1        4.2 %
C: UI exists but functionality is incomplete      9       37.5 %
D: Implemented but uses separate/duplicated code  3       12.5 %
E: Integrated and functionally tested             5       20.8 %
F: Visually verified in running application       5       20.8 %
G: Verified through hardware print workflow       0        0.0 %
─────────────────────────────────────────────────────────────────
Total Distinct Feature Areas                     24      100.0 %
```

### Critical Reality Finding:
- **Only 41.6% of feature areas (Categories E & F) are genuinely working and verified.**
- **Over 54.2% of feature areas (Categories B, C, & D) are incomplete, fragmented, or disconnected from the primary UI.**
- **0% (Category G) have been verified against physical hardware spooling.**
- This conclusively proves why the user found the application unfinished and unsuitable for daily shop production, despite previous reports claiming feature parity.
