# PVC Studio Pro — Phased Implementation Roadmap
**Strictly Sequenced Engineering Delivery Plan: Dependencies, Quality Gates & Acceptance**

- **Date:** 2026-11-19
- **Auditor:** Senior Windows Desktop Systems & Print Engine Architect
- **Strategy:** Rigorous Phased Engineering. Each phase gates the next. Zero code in Phase 0.

---

## 1. Master Phase Sequence & Dependency Graph

```mermaid
graph TD
    P0["Phase 0: Forensic Audit & Roadmap (Current Baseline)"]
    P1["Phase 1: Shared Session Architecture & State Integration"]
    P2["Phase 2: Unified Interactive Crop Engine"]
    P3["Phase 3: Adaptive Card Detection & Extraction"]
    P4["Phase 4: Shared Physical Print & Spooling Pipeline"]
    P5["Phase 5: Passport Photo Studio & Biometric Engine"]
    P6["Phase 6: Full Template Catalogue & Manager Integration"]
    P7["Phase 7: Auditable Ledger, Corrections & Test Mode"]
    P8["Phase 8: Material Design 3 / Fluent Desktop Polish"]
    P9["Phase 9: End-to-End Regression, QA & Release Candidate"]

    P0 --> P1
    P1 --> P2
    P1 --> P4
    P2 --> P3
    P3 --> P6
    P4 --> P5
    P1 --> P7
    P6 --> P8
    P5 --> P8
    P7 --> P8
    P8 --> P9
```

---

## 2. Phase-by-Phase Technical Specifications

### Phase 0: Forensic Analysis, Architecture Review & Delivery Plan (CURRENT)
- **Objectives:** Establish the unvarnished reality of the repository; reverse-engineer competitor binaries; document all root causes, disconnections, and deficiencies; define the complete architecture roadmap.
- **Source Files Affected:** Strictly `docs/` files only. Zero source code modifications.
- **Deliverables:** 10 comprehensive architectural and audit documents.
- **Status:** **COMPLETE & AWAITING USER APPROVAL**.

---

### Phase 1: Shared Session Architecture & State Integration
- **Objectives:** Implement the shared document session and asset store to eliminate data silos and state drift between workspaces.
- **Exact Source Files Likely to Change:**
  - `src/PVCStudioPro.Core/Models/DocumentSessionModels.cs` (New)
  - `src/PVCStudioPro.Core/Services/IDocumentSessionManager.cs` (New)
  - `src/PVCStudioPro.Core/Services/Implementations/DocumentSessionManager.cs` (New)
  - `src/PVCStudioPro.App/App.xaml.cs` (DI registration)
  - `src/PVCStudioPro.App/ViewModels/MainViewModel.cs` (Session binding)
  - `src/PVCStudioPro.App/ViewModels/ProductionWorkspaceViewModel.cs` (Consume session)
- **Existing Functionality to Reuse:** `PdfDocumentService` (PDFium loading), `DocumentImportService` (Magic sniffing).
- **New Models / Interfaces:** `IDocumentSessionManager`, `DocumentSessionState`, `ImageAsset`.
- **UI Changes:** Active document indicator and security status badge linked to the shared session in the application header.
- **Dependencies:** Phase 0 approval.
- **Automated Tests:** `DocumentSessionManagerTests.cs` (Multi-workspace state consistency, thread-safe access, cleanup).
- **Manual Acceptance Criteria:** Open a document in Production Workspace; navigate to Document Desk or Photo Studio; confirm all screens immediately reflect the loaded document without re-opening.
- **Risks:** Memory leaks from unmanaged `SKBitmap` handles if session disposal is not strict.
- **Definition of Done:** All workspaces observe document changes through `IDocumentSessionManager`.
- **Estimated Complexity:** Medium (2 days).

---

### Phase 2: Unified Interactive Crop Engine
- **Objectives:** Merge the three fragmented crop implementations into a single authoritative `ICropEngine` with interactive mouse-wheel zoom, pan, 8-point bounding handles, aspect-ratio locks, and perspective quad rectification.
- **Exact Source Files Likely to Change:**
  - `src/PVCStudioPro.Core/Services/ICropEngine.cs` (New)
  - `src/PVCStudioPro.Core/Services/Implementations/CropEngine.cs` (New)
  - `src/PVCStudioPro.App/Controls/InteractiveCropCanvas.xaml` & `.xaml.cs` (New reusable WPF UserControl)
  - `src/PVCStudioPro.App/ViewModels/CropStudioViewModel.cs` (Refactor to use engine)
  - `src/PVCStudioPro.App/ViewModels/ProductionWorkspaceViewModel.cs` (Replace textboxes with `InteractiveCropCanvas`)
- **Existing Functionality to Reuse:** `ManualCropService` matrix transformations and OpenCV `WarpPerspective`.
- **New Models / Interfaces:** `ICropEngine`, `CropBounds`, `CropInteractionState`.
- **UI Changes:** Both the modal Crop Studio and the in-place crop drawer render the identical visual canvas with blue interactive drag handles and live millimeter dimensions.
- **Dependencies:** Phase 1 (operates on `ImageAsset`).
- **Automated Tests:** `CropEngineTests.cs` (Sub-pixel coordinate mapping, aspect ratio preservation, 90° rotation crop invariance).
- **Manual Acceptance Criteria:** Open scan; drag crop handle; confirm image scales smoothly with mouse-wheel ($0.1\times$ to $5\times$) without pixel jumping; apply crop; verify card dimensions are exactly $85.60 \times 53.98\text{ mm}$ CR80.
- **Definition of Done:** Zero numeric typing required for standard cropping; both modal and drawer share the same control.
- **Estimated Complexity:** High (3 days).

---

### Phase 3: Adaptive Aadhaar & Identity Document Extraction
- **Objectives:** Replace blind static pixel slicing with confidence-based multi-factor detection (OCR keywords + OpenCV adaptive Canny/Hough line detection) to accurately isolate front and back cards across all e-Aadhaar generations.
- **Exact Source Files Likely to Change:**
  - `src/PVCStudioPro.Core/Services/Implementations/CardDetectionService.cs`
  - `src/PVCStudioPro.Core/Services/Implementations/DocumentTypeDetectionService.cs`
  - `src/PVCStudioPro.Core/Models/UniversalDocumentModels.cs`
  - `tests/PVCStudioPro.Tests/DocumentEngineTests.cs`
- **Existing Functionality to Reuse:** `TemplateService` calibrated anchor coordinates, `VisionService` OpenCV bindings.
- **New Models / Interfaces:** `ICardDetectionEngine`, `DetectionConfidenceScore`, `CardExtractionResult`.
- **UI Changes:** Displays detection confidence percentage (`98% High Confidence`) with visual feedback if edge detection required fallback.
- **Dependencies:** Phase 2.
- **Automated Tests:** `CardDetectionAdaptiveTests.cs` (Modern e-Aadhaar, Baal Aadhaar, mAadhaar, shifted scans, and rotation vectors).
- **Manual Acceptance Criteria:** Load e-Aadhaar with varied printer margins; confirm both front and back cards are cropped exactly to the cutlines with zero text truncation and zero aspect-ratio squishing.
- **Definition of Done:** 100% extraction accuracy on standard test corpus without manual ratio adjustments.
- **Estimated Complexity:** High (3 days).

---

### Phase 4: Shared Physical Print & Spooling Pipeline
- **Objectives:** Enforce the single source of truth layout principle so the canvas preview, PDF export, and Windows print spooler share identical millimeter geometry.
- **Exact Source Files Likely to Change:**
  - `src/PVCStudioPro.Core/Services/Implementations/PhysicalPageRenderService.cs`
  - `src/PVCStudioPro.Core/Services/Implementations/PrintService.cs`
  - `src/PVCStudioPro.App/ViewModels/ProductionWorkspaceViewModel.cs`
  - `src/PVCStudioPro.App/ViewModels/PrintPreviewApprovalViewModel.cs`
- **Existing Functionality to Reuse:** SkiaSharp physical rendering and Windows GDI+ `PrintDocument`.
- **New Models / Interfaces:** `IPhysicalPageLayoutEngine`, `PrintSpoolJob`.
- **UI Changes:** Instant live toggle for cut guidelines (0.2 mm crop lines), center folding line, and mirror printing directly on the sheet preview.
- **Dependencies:** Phase 1.
- **Automated Tests:** `PhysicalPageRenderTests.cs` (Pixel-to-mm roundtrip accuracy at 150, 300, 600, and 1200 DPI).
- **Manual Acceptance Criteria:** Spool A4 sheet to Microsoft Print to PDF; measure exported PDF points; verify front card is exactly at $X=15.0\text{ mm}$ and back card at $X=109.4\text{ mm}$.
- **Definition of Done:** Zero divergence between screen preview, exported PDF, and print spooler raster.
- **Estimated Complexity:** Medium (2 days).

---

### Phase 5: Passport Photo Studio & Biometric Engine
- **Objectives:** Fix the GrabCut bottom-edge wavy scalloping; enable interactive face positioning; implement standard 4x6 (8-up) and A4 (16/32-up) photo sheets; connect to real hardware spooling.
- **Exact Source Files Likely to Change:**
  - `src/PVCStudioPro.Core/Services/Implementations/VisionService.cs`
  - `src/PVCStudioPro.App/ViewModels/PhotoStudioViewModel.cs`
  - `src/PVCStudioPro.App/Views/PhotoStudioView.xaml`
- **Existing Functionality to Reuse:** OpenCV GrabCut, `IPrintService`.
- **New Models / Interfaces:** `IPassportPhotoEngine`, `BiometricGuidelineModel`.
- **UI Changes:** Interactive portrait adorner allowing the operator to drag and re-scale the face within ICAO 9303 biometric guide lines; real printer selection and physical spooling button.
- **Dependencies:** Phase 4 (uses shared physical layout engine).
- **Automated Tests:** `PassportPhotoEngineTests.cs` (GrabCut bottom boundary verification, ICAO dimensional checks).
- **Manual Acceptance Criteria:** Import portrait; verify bottom edge is completely clean with zero wavy scallops; click Print; verify print job enters Windows print queue and logs in ledger.
- **Definition of Done:** Flawless background removal and genuine hardware printing.
- **Estimated Complexity:** Medium (2 days).

---

### Phase 6: Full Template Catalogue & Manager Integration
- **Objectives:** Wire all 29+ competitor templates directly into `ProductionWorkspaceViewModel` with an intuitive template switcher and custom coordinate overrides.
- **Exact Source Files Likely to Change:**
  - `src/PVCStudioPro.App/ViewModels/ProductionWorkspaceViewModel.cs`
  - `src/PVCStudioPro.App/Views/ProductionWorkspaceView.xaml`
  - `src/PVCStudioPro.Core/Services/Implementations/TemplateService.cs`
- **Existing Functionality to Reuse:** All 29 templates in `TemplateService.cs`.
- **New Models / Interfaces:** `TemplateSelectionEventArgs`, `TemplateCategoryGroup`.
- **UI Changes:** Clean categorized template dropdown on top toolbar (Identity, Health, Labour, State Rations, Transport) with quick search.
- **Dependencies:** Phase 3.
- **Automated Tests:** `TemplateRegistryIntegrationTests.cs` (All 29 templates loaded, matched, and applied to canvas).
- **Manual Acceptance Criteria:** Select "Kerala Ration Card" or "Ayushman PM-JAY"; verify layout instantly snaps to the calibrated template coordinates.
- **Definition of Done:** Every competitor template reachable and functional in the primary workspace.
- **Estimated Complexity:** Low (1.5 days).

---

### Phase 7: Auditable Ledger, Corrections, Daily Closing & Test Mode
- **Objectives:** Implement enterprise financial ledger with transaction editing, void/cancellations with mandatory reasons, partial payments, daily closing reconciliation, CSV export, and strict physical Test Mode isolation.
- **Exact Source Files Likely to Change:**
  - `src/PVCStudioPro.Core/Services/Implementations/JobLedgerService.cs` (Upgrade to `TransactionLedgerService`)
  - `src/PVCStudioPro.Core/Services/IDataModeProvider.cs` (New)
  - `src/PVCStudioPro.App/ViewModels/JobLedgerViewModel.cs`
  - `src/PVCStudioPro.App/Views/JobLedgerView.xaml`
  - `src/PVCStudioPro.App/MainWindow.xaml` (Test Mode banner)
- **Existing Functionality to Reuse:** Dapper ORM and SQLite database connectivity.
- **New Models / Interfaces:** `ITransactionLedgerService`, `BusinessTransaction`, `TransactionAuditTrail`, `IDataModeProvider`.
- **UI Changes:** Transaction edit/void modals; daily cash reconciliation panel; top bar Test Mode toggle with prominent orange warning banner.
- **Dependencies:** Phase 1.
- **Automated Tests:** `TransactionLedgerTests.cs` (Auditable updates, void calculations, Test Mode database separation).
- **Manual Acceptance Criteria:** Enter transaction in Test Mode; reset test data; confirm production ledger remains completely untouched; edit a transaction in Production Mode; confirm audit trail records the change reason.
- **Definition of Done:** Zero test data leakage; full financial reconciliation capabilities.
- **Estimated Complexity:** High (3 days).

---

### Phase 8: Material Design 3 / Fluent Desktop Polish
- **Objectives:** Enforce uniform visual styling across all 18 views using the M3 color/elevation token system; ensure crisp contrast in Light and Dark modes; implement keyboard accelerators (Ctrl+O, Ctrl+P, arrow key nudges).
- **Exact Source Files Likely to Change:**
  - `src/PVCStudioPro.App/Resources/Theme/M3Tokens.xaml` (New)
  - `src/PVCStudioPro.App/App.xaml`
  - `src/PVCStudioPro.App/MainWindow.xaml`
  - All workspace XAML views.
- **Existing Functionality to Reuse:** WPF-UI control library.
- **New Models / Interfaces:** Key gesture handlers.
- **UI Changes:** Polished, professional high-density commercial desktop styling.
- **Dependencies:** Phases 1 through 7.
- **Automated Tests:** Theme resource verification tests.
- **Manual Acceptance Criteria:** Toggle Light/Dark mode; verify all text meets 4.5:1 contrast; resize window to $1280 \times 800$; confirm zero layout clipping or overlap.
- **Definition of Done:** Product looks and feels like a tier-1 commercial application.
- **Estimated Complexity:** Medium (2 days).

---

### Phase 9: End-to-End Regression Testing, Hardware QA & Release Candidate
- **Objectives:** Execute complete end-to-end acceptance test suite; test physical print spooling; build self-contained Release installer; verify zero regressions.
- **Exact Source Files Likely to Change:**
  - `scripts/install_local.ps1`
  - `publish/` artifacts
- **Deliverables:** Verified Release Candidate and updated user documentation.
- **Dependencies:** All previous phases.
- **Estimated Complexity:** Low (1.5 days).
