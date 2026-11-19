# PVC Studio Pro — Phase 0.1 Architectural Decisions & Alternatives Rejected
**Architectural Decision Records (ADR): Rationale, Trade-Offs, and Engineering Governance**

- **Document:** `docs/PHASE_0_1_ARCHITECTURE_DECISIONS.md`
- **Date:** 2026-11-19
- **Status:** Architectural Decisions (Refined & Validated in Phase 0.2: see [`docs/PHASE_0_2_FINAL_VALIDATION.md`](file:///d:/pvc%20card%20pro/docs/PHASE_0_2_FINAL_VALIDATION.md))
- **Auditor:** Senior Windows Desktop Systems & Print Engine Architect

---

## 1. ADR-01: Lifecycle-Managed Immutable Source Assets vs. Simplistic Mutable Singletons

### Context & Problem Statement:
In the initial prototype, each ViewModel held raw, mutable `SKBitmap?` fields (e.g. `_cachedPageBitmap`, `_sourceImage`, `_processedSinglePhoto`). A simplistic singleton containing mutable bitmap references causes severe architectural bugs:
- **Destructive Cropping:** If an operator crops an image, the original raster is overwritten or disposed, making subsequent recropping or un-cropping impossible without reloading the file from disk.
- **State Collisions:** When navigating between Production Studio, Crop Studio, and Document Desk, concurrent read/write access to a shared mutable bitmap triggers memory access violations (`AccessViolationException` in unmanaged Skia/OpenCV).
- **Native Memory Leaks:** Unmanaged SkiaSharp bitmap buffers are not automatically collected by the .NET GC, causing memory bloat (500 MB+ within minutes of loading high-DPI scans).

### Decision:
Implement an **Immutable Source Asset & Functional Derivation Model**:
1. **`SourceImageAsset` (Immutable Root):** The original raster decoded from PDFium or image file is stored in a read-only, thread-safe wrapper. It is never modified or cropped directly.
2. **`DerivedImageAsset` (Versioned Node):** Every crop, rotation, color adjustment, or background removal generates a new `DerivedImageAsset` with an explicit `RevisionId`, `ParentAssetId`, `TransformOperation`, and physical metadata ($DPI, Width_{mm}, Height_{mm}$).
3. **Reference-Counted Native Lifecycles:** Implement `IDisposable` with an internal atomic reference counter. Assets are safely reclaimed only when no active workspace or undo stack holds a reference.
4. **Isolated Workspace Views:** Workspaces subscribe to an `AssetWorkspaceBinding`. A workspace reads the active revision but cannot mutate another workspace's scratchpad without an explicit `CommitRevision` event.

### Alternatives Rejected:
- **Simplistic Mutable Bitmap Singleton:** Rejected because it causes race conditions, irreversible destructive edits, and cross-workspace state contamination.
- **File-Backed Temporary Disk Caching:** Rejected because writing intermediate uncompressed TIFF/BMP files to `%TEMP%` introduces disk I/O latency, SSD wear, and violates citizen privacy mandates (Zero-PII).

---

## 2. ADR-02: Centralized Reusable Visual Crop Engine vs. Screen-Specific Crop Implementations

### Context & Problem Statement:
The codebase previously contained three separate crop implementations:
1. `CropStudioViewModel`: Floating adorner with zoom/pan, but wired exclusively to `DocumentDeskViewModel`.
2. `ProductionWorkspaceViewModel`: A modal drawer with four raw integer textboxes (`CropX`, `CropY`, `CropW`, `CropH`).
3. `VisionService`: Center-crop and auto-contour crop without visual interaction.

### Decision:
Create **ONE Authoritative Reusable Visual Component (`InteractiveCropCanvas.xaml`)** powered by **`ICropEngine`**:
1. **Mathematical Engine (`CropEngine.cs`):** Unified coordinate transforms between Viewport Screen DIPs, Native Image Pixels, and Real-World Millimeters.
2. **Visual Interaction Control:**
   - Smooth mouse-wheel zoom centered on cursor ($0.1\times$ to $5.0\times$).
   - Pan via Middle-Click or Space+Drag.
   - 8-point interactive bounding handles with sub-pixel snap.
   - Aspect ratio constraints: CR80 ($85.60 \times 53.98\text{ mm}$), Passport ($35 \times 45\text{ mm}$), Stamp ($25 \times 30\text{ mm}$), 4x6 Photo, and Freeform.
   - Quadrilateral 4-corner perspective quad warping for camera-captured mobile scans.
   - 90° clockwise/counter-clockwise and arbitrary angle rotation.
   - Dual-mode inputs: Interactive visual manipulation on canvas, alongside two-way synchronized numeric millimeter/pixel inputs.
   - Per-template saved crop profiles and multi-level Undo/Redo stack.
3. **Universal Consumption:** Production Studio, Crop Studio, Document Desk, and Passport Photo Studio all host this identical control.

### Alternatives Rejected:
- **Retaining Modal Text Boxes in Production Workspace:** Rejected because operators cannot intuitively crop citizen cards by guessing pixel offsets.
- **Separate Standalone `CropTool.exe` Process (Competitor Model):** Rejected because spawning an external WinForms process breaks WPF theme consistency, requires fragile inter-process IPC, and exposes citizen images in transit.

---

## 3. ADR-03: Multi-Stage Print Job Lifecycle & Spool Verification vs. Method-Return Assumptions

### Context & Problem Statement:
Previously, the print button in `PhotoStudioViewModel` did not print at all, while `ProductionWorkspaceViewModel` declared success immediately upon calling `_printService.SpoolPageAsync`. If the Windows print spooler rejected the job, or the printer was offline/out of paper, the software claimed success and logged a completed financial transaction.

### Decision:
Establish an explicit **7-Stage Print Job Lifecycle (`PrintJobLifecycleStatus`)**:
```
[Draft] ──► [AwaitingOperatorApproval] ──► [SubmittedToWindowsSpooler]
                                                    │
                 ┌──────────────────────────────────┴──────────────────┐
                 ▼                                                     ▼
         [SpoolAccepted]                                            [Failed]
                 │                                                     │
                 ▼                                                     ▼
     [CompletedWithEvidence]                                    [OperatorAlert]
```

1. **State Definitions:**
   - `Draft`: Job parameters configured in UI.
   - `AwaitingOperatorApproval`: Sheet layout and billing verified by operator on physical preview.
   - `SubmittedToWindowsSpooler`: RAW/EMF print stream dispatched to Win32 `StartDocPrinter`/`StartPagePrinter`.
   - `SpoolAccepted`: Win32 spooler confirms receipt of all bytes without error code.
   - `CompletedWithEvidence`: Windows print queue monitors job status via Win32 `GetJob`/WMI to confirm job transition through the spooler.
   - `Failed`: Spooler returns error (e.g. driver mismatch, invalid DEVMODE, offline port).
   - `Cancelled`: Aborted by operator prior to spool dispatch.
2. **Ledger Synchronization:** A financial record is marked `PENDING_PRINT` upon submission and is updated to `PAID_SPOOLED` only after `SpoolAccepted` or `CompletedWithEvidence`.

### Alternatives Rejected:
- **Instantaneous Completion on Button Click:** Rejected as fundamentally deceptive and non-commercial.
- **Polling Printer Hardware Sensors (SNMP/USB direct status):** Rejected as a hard release gate because many consumer USB ink-tank printers (Epson L805, Canon G-Series) use proprietary bi-directional status protocols that lock out third-party Win32 status queries while printing. Win32 Spooler API tracking provides reliable, universal evidence.

---

## 4. ADR-04: Normalized Multi-Payment Financial Ledger vs. Single Payment Mode Field

### Context & Problem Statement:
The prototype `print_jobs` table had a single string column `payment_mode` and an immutable record structure. An operator could not split a ₹140 bill into ₹100 Cash + ₹40 UPI, could not issue discounts or fee waivers, could not record customer credit balances, and could not edit or void mistakes without corrupting the daily closing drawer.

### Decision:
Decompose transactions into a **Normalized Master-Detail Financial Architecture**:
1. **`business_transactions` (Master Invoice):** Stores invoice number, total amount, discount, net payable, total paid, balance due, and job status.
2. **`transaction_payments` (Individual Payment Entries):**
   - Each payment is a separate auditable row with `payment_id`, `transaction_id`, `amount`, `payment_method` (`CASH`, `UPI`, `CARD`), `collected_at`, and `operator_id`.
   - Supports split payments (e.g. Part 1: ₹50 Cash, Part 2: ₹20 UPI).
3. **Daily Cash/UPI Reconciliation:** Reconciliations are computed by querying `SUM(amount) FROM transaction_payments WHERE payment_method = 'CASH'`, guaranteeing 100% mathematical accuracy against the physical drawer.
4. **Controlled Audit Trail (`transaction_audit_trail`):** Edits, fee adjustments, and voids require an operator explanation and are recorded immutably.
5. **Database Migration Strategy:** Safe 3-step automatic migration:
   - Step 1: Automatic timestamped SQLite backup (`pvc_studio_pre_migration.db`).
   - Step 2: DDL schema migration with transaction rollback protection.
   - Step 3: Migration verification script checking record counts and foreign keys before opening the UI.

### Alternatives Rejected:
- **Single Flat Table with Comma-Separated Values:** Rejected as a database anti-pattern that prevents SQL aggregations and reporting.
- **In-Memory Ledger (No Database):** Rejected because financial and tax records must persist permanently across application and PC restarts.

---

## 5. ADR-05: Total Environment Isolation for Test Mode vs. Transaction-Only Filtering

### Context & Problem Statement:
Developers and shop operators frequently need to test tray alignment, test print layouts, and train staff. If test operations run against the live business database, customer invoice numbering has missing gaps, printer calibration offsets get overwritten, and shop daily totals are corrupted.

### Decision:
Implement **Total Physical Environment Isolation via `IDataModeProvider`**:
1. **Separate Physical Storage:**
   - **Production:** `%LOCALAPPDATA%\PVCStudioPro\data\pvc_studio_prod.db` and `config\prod_settings.json`.
   - **Test Mode:** `%LOCALAPPDATA%\PVCStudioPro\data\pvc_studio_test.db` and `config\test_settings.json`.
2. **Isolated Invoice Sequences:** Production invoice sequence (`INV-20261119-0001`) is never touched by test invoices (`TEST-0001`).
3. **Isolated Calibration Offsets:** Test tray offset calibration does not alter verified production printer alignments.
4. **Visual & Behavioral Guardrails:**
   - Permanent orange header banner when Test Mode is active.
   - Confirmation dialog with explicit safety prompt when entering or exiting Test Mode.
   - "Reset Test Sandbox" button that purges only `pvc_studio_test.db` and test files, with 0% risk to production data.

### Alternatives Rejected:
- **A Single Database with `is_test = 1` Column:** Rejected because human query errors, buggy `WHERE` clauses, or aggregation bugs could accidentally leak test totals into commercial tax or GST reports.

---

## 6. ADR-06: Adaptive Confidence-Based Aadhaar Detection vs. Blind 100% Automation Claims

### Context & Problem Statement:
Aadhaar PDFs downloaded from the UIDAI portal do not adhere to a single rigid coordinate grid. Layouts vary between Modern e-Aadhaar, Baal Aadhaar, mAadhaar, Masked Aadhaar, and regional language prints. Claiming 100% automatic extraction using static pixel slicing is a guaranteed point of failure.

### Decision:
Implement a **Multi-Stage Adaptive Detection Pipeline with Low-Confidence Operator Handoff**:
```
Stage 1: PDF Point Geometry & Page Box Inspection
   │
Stage 2: Anchor Keyword Text Extraction (UIDAI, Enrollment, Mera Aadhaar)
   │
Stage 3: Calibrated Template Coordinate Seed
   │
Stage 4: OpenCV Adaptive Canny Edge & Hough Line Boundary Refinement (±15mm window)
   │
Stage 5: Confidence Scoring Engine (0% - 100%)
   ├── If Confidence >= 85%: Auto-extract and present in Production Studio
   └── If Confidence < 85%: Visual Operator Prompt + Instant Crop Drawer with Calibrated Defaults
```
1. **Low-Confidence Transparency:** If detection confidence is below 85% (e.g. skewed scan, unusual print margin), the software **never silently slices a wrongly cropped card**. It highlights the detection boundary in amber, informs the operator, and opens the crop drawer with calibrated guides for instant 1-click confirmation or minor adjustment.
2. **Saved Custom Overrides:** Operators can save verified crop offsets as custom template profiles for repeated shop customers.

### Alternatives Rejected:
- **Blind Static Coordinate Slicing (Current Flawed Implementation):** Slices cards without checking margins, causing text truncation.
- **Heavy Cloud/Remote AI Detection:** Rejected because Indian e-Sevai shops require 100% offline functionality, and uploading citizen Aadhaar data to external servers violates Aadhaar Act regulations.

---

## 7. ADR-07: Standardizing on WPF-UI with Material Design 3 Tokens vs. Multi-Framework Rewrites

### Context & Problem Statement:
The application requires a modern, professional, high-density desktop aesthetic with flawless Dark and Light modes. Evaluating whether to introduce `MaterialDesignInXamlToolkit`, rewrite in WinUI 3 / Avalonia, or retain WPF-UI.

### Decision:
**Retain WPF-UI v3.0.0 and augment it with an internal Material Design 3 (M3) Token System**:
1. **Zero Framework Rewrite Cost:** WPF-UI is already fully integrated, compiled, and verified on .NET 8 x64.
2. **Native Windows 11 Integration:** WPF-UI provides native Windows 11 Mica backdrop, native window title bars, Snap Layouts, and Fluent typography out of the box.
3. **M3 Token Layer (`M3Tokens.xaml`):** Establish clean M3 color roles (`Surface`, `SurfaceContainer`, `Primary`, `OnPrimary`, `SecondaryContainer`, `OutlineVariant`) and elevation tokens directly inside XAML styles.
4. **Rejecting Multi-Framework Mixing:** Do **not** install `MaterialDesignInXamlToolkit` alongside WPF-UI. Dual UI frameworks introduce competing global styles, bloated binaries, conflicting theme managers, and rendering performance degradation.

---

## Summary of Architectural Commitments

| Subsystem | Initial Phase 0 Design | Phase 0.1 Upgraded Decision |
| :--- | :--- | :--- |
| **Asset Model** | Mutable `SKBitmap` singleton | **Immutable Source Asset + Versioned Derived Assets with Ref-Counting** |
| **Crop Engine** | Modal textboxes + isolated CropStudio | **Unified `InteractiveCropCanvas` control across all 4 workspaces** |
| **Print Spooling** | Assumed success on API return | **7-Stage Win32 Spooler Lifecycle (`SpoolAccepted` / `Evidence`)** |
| **Ledger** | Flat single table with `payment_mode` | **Master-Detail `transaction_payments` (Multi-payment / Split / Refunds)** |
| **Database Safety** | Production DB polluted by tests | **100% Physical Isolation (`pvc_studio_prod.db` vs `pvc_studio_test.db`)** |
| **Aadhaar Detection** | Static pixel box slicing | **Adaptive OpenCV edge refinement + Confidence scoring + Operator fallback** |
| **Template Registry** | Disconnected Core class | **Universal categorized selector in top toolbar across all 29 templates** |
| **UI Framework** | Unclear styling layer | **WPF-UI v3 + Material Design 3 Token Hierarchy in XAML** |
