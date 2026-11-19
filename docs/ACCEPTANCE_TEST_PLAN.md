# PVC Studio Pro — Acceptance Test Plan & Quality Gates (Phase 0.1 Revised)
**Definitive Verification Protocol: Step-by-Step Acceptance Criteria & Quality Gates**

- **Document:** `docs/ACCEPTANCE_TEST_PLAN.md`
- **Revision:** Phase 0.1
- **Date:** 2026-11-19
- **Auditor:** Senior Windows Desktop Systems & Print Engine Architect
- **Quality Mandate:** Unit tests alone do not establish completion. Every feature requires demonstrable end-to-end acceptance testing.

---

## 1. Master Acceptance Gates

Before any phase or release candidate is declared complete, it must pass all six quality gates:

```
[ Gate 1: Zero Code Regressions ] ────► 100% Passing Unit & Integration Tests (dotnet test)
[ Gate 2: Clean Self-Contained Build ] ─► Compiles standalone win-x64 with bundled runtime (Zero .NET prompts)
[ Gate 3: Live Process Verification ] ──► Launches with working set <250 MB; zero startup exceptions
[ Gate 4: Interactive Acceptance Test ] ─► Passes all 6 End-to-End Manual Workflow Scripts below
[ Gate 5: Hardware Print Spool Test ] ──► Spools valid EMF/XPS bytes to Windows spooler with exact mm geometry
[ Gate 6: Zero Citizen PII Audit ] ─────► Zero citizen numbers or raw identity images written to disk or logs
```

---

## 2. End-to-End Acceptance Test Scripts

### Test Suite 1: Centralized Crop Engine Acceptance Test
- **Objective:** Verify zoom, pan, fit-to-window, 100% zoom, 8 drag handles, aspect-ratio locking, perspective correction, rotation, numeric adjustment, undo/redo, and cross-workspace state propagation.
- **Step-by-Step Procedure:**
  1. Open PVC Studio Pro and navigate to `Production Studio`.
  2. Click `Open Document (Ctrl+O)` and load high-resolution test scan `Synthetic_Aadhaar_300DPI.png`.
  3. Verify image is automatically fitted to window (`FitToWindow`).
  4. Click `Adjust Crop` to launch the unified crop engine (`InteractiveCropCanvas`).
  5. Test Zoom & Pan:
     - Use mouse scroll-wheel over citizen photo: confirm smooth zoom from $1.0\times$ to $3.5\times$ centered on mouse cursor.
     - Click and drag with middle-mouse or Spacebar+Left-Click: verify smooth panning without stutter.
     - Click `[100% Zoom]`: confirm canvas renders 1 native image pixel to 1 screen DIP.
     - Click `[Fit to Window]`: confirm canvas instantly snaps back to full page overview.
  6. Test Drag Handles & Aspect Ratio Lock:
     - Verify 8 handles (4 corner handles + 4 edge handles) are clearly visible.
     - Drag top-right corner handle: verify bounding box resizes smoothly while strictly maintaining the CR80 aspect ratio ($1.5858$).
     - Move the crop box by dragging from its center: confirm coordinates update smoothly.
  7. Test Numeric & Angle Adjustments:
     - Type exact numeric values into millimeter boxes ($X: 20.0\text{ mm}, Y: 220.0\text{ mm}, W: 85.6\text{ mm}, H: 54.0\text{ mm}$): confirm visual handles snap immediately.
     - Click `[Rotate 90°]`: confirm image and crop box rotate clockwise.
  8. Test Perspective Quad Warp:
     - Toggle `[Perspective Mode ☑]`: confirm 4 corner pins appear. Drag top-left pin to simulate keystone distortion correction.
  9. Test Undo / Redo:
     - Click `[Undo]` (or Ctrl+Z): confirm previous crop rectangle is restored.
  10. Test Commit & Cross-Workspace Observation:
      - Click `[Apply Crop]`.
      - Confirm the extracted card is normalized to exactly $2022 \times 1275\text{ px}$ ($85.60 \times 53.98\text{ mm}$ @ 600 DPI).
      - Navigate to `Document Desk` and `Crop Studio`: confirm the identical cropped card is immediately displayed with matching dimensions without reloading.
- **Pass Criteria:** Zero pixel jumping, aspect ratio strictly maintained, identical state reflected across all workspaces.

---

### Test Suite 2: Adaptive Aadhaar Auto-Extraction & Alignment Acceptance Test
- **Objective:** Verify multi-factor Aadhaar detection, boundary refinement, low-confidence fallback, dual-card extraction, and manual fine-tuning.
- **Step-by-Step Procedure:**
  1. Load password-protected test e-Aadhaar PDF.
  2. Verify in-place password banner appears with text field and show/hide toggle.
  3. Enter password and press Enter: verify document decrypts cleanly.
  4. Observe auto-detection badge: must display `UIDAI E-AADHAAR • AUTO-DETECTED (Confidence >95%)`.
  5. Inspect the center physical sheet canvas: confirm Front Card and Back Card appear mounted side-by-side on the A4 portrait sheet.
  6. Inspect Front Card: citizen photo, name, DOB, and gender must be 100% inside the card boundary with zero edge clipping.
  7. Inspect Back Card: address block and secure QR code must be fully visible with zero truncation.
  8. **Low-Confidence Test:** Load a intentionally skewed / non-standard scanned Aadhaar ($<85\%$ confidence).
     - Confirm software **does not silently crop wrong coordinates**.
     - Confirm amber warning displays: `⚠️ Low Detection Confidence (68%) — Please Confirm Card Bounds`.
     - Confirm crop drawer opens automatically with calibrated guides.
     - Nudge crop box $2\text{ mm}$ down and click `[Confirm & Apply]`.
     - Click `[Save as Custom Profile]`: save as `Shop_Scanner_Aadhaar`.
  9. Click `Rotate Cards 90°`: confirm both cards rotate clockwise and sheet layout recalculates.
  10. Click `Swap Faces`: confirm Front and Back card positions exchange on the physical sheet.
- **Pass Criteria:** Both sides extracted accurately without manual ratio adjustments; zero aspect-ratio squishing; zero silent miscrops on low confidence.

---

### Test Suite 3: True Physical Print & Spooling Acceptance Test
- **Objective:** Verify that on-screen sheet preview, exported PDF, and physical print spooling are mathematically identical, with full 7-stage spooler verification.
- **Step-by-Step Procedure:**
  1. In `Production Studio`, select layout: `A4 Single Side (Side-by-Side)`.
  2. Verify on-screen physical sheet displays full A4 paper ($210 \times 297\text{ mm}$) fitted to window with zero scrolling required.
  3. Toggle `Cut Marks ☑`: confirm 0.2 mm hairline crop marks appear at the four corners of each card.
  4. Toggle `Mirror Mode ☑`: confirm cards flip horizontally (essential for thermal dragon sheet lamination).
  5. Click `Nudge Offset [►]` (+0.1 mm X): confirm live millimeter readout updates to `X: +0.1 mm`.
  6. Click `Export PDF`: save as `A4_Print_Verification.pdf`.
  7. Open `A4_Print_Verification.pdf` in a vector viewer: measure card positions. Front Card must be at $X=15.1\text{ mm}$, $Y=20.0\text{ mm}$; Back Card must be at $X=109.5\text{ mm}$, $Y=20.0\text{ mm}$.
  8. Select target printer `Microsoft Print to PDF` and click `START PRINTING`.
  9. Verify print job lifecycle:
     - Status displays `Submitted to Windows Spooler...`
     - Status updates to `Spool Accepted by Windows Spooler`
     - Status updates to `Completed (Print Job Finished)`
  10. Check Windows print queue: confirm bytes were received and processed.
- **Pass Criteria:** Sub-millimeter parity between screen preview, exported PDF points, and print spooler raster; genuine Win32 spooler tracking.

---

### Test Suite 4: Passport Photo Studio & Biometric Engine Acceptance Test
- **Objective:** Verify ICAO 9303 compliance, clean GrabCut background removal without bottom scalloping, interactive face positioning, and genuine print spooling.
- **Step-by-Step Procedure:**
  1. Navigate to `Passport Photo Studio`.
  2. Click `Import Portrait` and load customer test portrait `Portrait_Raw.jpg`.
  3. Verify automatic face detection places the subject's crown and chin within the ICAO guidelines ($70\%–80\%$ head coverage).
  4. Inspect the bottom edge of the subject's shirt: **the bottom border must be completely straight and clean, with zero wavy scallops or undulating edge artifacts**.
  5. Click `Studio Blue`: confirm background replaces cleanly behind hair strands without blue haloing.
  6. Click and drag portrait in preview: confirm operator can intuitively nudge face alignment and zoom head size.
  7. Select layout: `4x6 Photo Paper (8-Up)`.
  8. Verify 8-up sheet composer renders 8 identical $35 \times 45\text{ mm}$ photos with dashed cut guides.
  9. Click `PRINT PHOTO SHEET`.
  10. Open Windows Print Queue: **confirm a real print job named `Passport_Photo_Sheet_8UP` is queued and spooled**.
  11. Navigate to `Ledger`: confirm transaction is recorded for ₹50.00 with payment method.
- **Pass Criteria:** Zero wavy artifacts at bottom, real print job dispatched to Windows queue, financial ledger updated.

---

### Test Suite 5: Business Ledger, Corrections & Test Mode Acceptance Test
- **Objective:** Verify full transaction lifecycle, separate payment entries, auditable edits, void/cancellations, and complete Test Mode isolation.
- **Step-by-Step Procedure:**
  1. **Test Mode Total Sandbox Isolation Check:**
     - Toggle `Test Mode` in application header.
     - Verify permanent orange warning banner appears: `⚠️ TEST MODE ACTIVE — SIMULATION SANDBOX ONLY`.
     - Print 2 test cards for ₹140.00.
     - Open `JobLedgerView`: verify 2 test transactions appear with prefix `TEST-0001`.
     - Click `Reset Test Sandbox`: confirm test records are wiped to 0.
     - Toggle back to `Production Mode` (confirming safety prompt).
     - Open `JobLedgerView`: **confirm production database contains 0 test records and is completely uncorrupted**.
  2. **Auditable Edit Check (Production Mode):**
     - Record a production job for ₹70.00 (`INV-20261119-0001`).
     - Click `[Edit]` on `INV-20261119-0001`.
     - Change Unit Price to ₹50.00, enter mandatory reason: "Promotional customer coupon". Click Save.
     - Confirm table displays Net Total: ₹50.00.
     - Click `[Audit History]`: verify audit log records: `TotalAmount changed from 70.00 to 50.00 | Reason: Promotional customer coupon | Operator: Admin`.
  3. **Multi-Payment & Partial Payment Check:**
     - Create transaction for ₹140.00.
     - Record Payment 1: ₹100.00 via `CASH`.
     - Confirm status is `PARTIAL` and `Balance Due` is ₹40.00.
     - Later, record Payment 2: ₹40.00 via `UPI` with Ref `UPI-TXN-984214`.
     - Confirm status flips to `PAID` and Balance Due becomes ₹0.00.
     - Check Daily Closing Summary: Cash Drawer must show ₹100.00; UPI Digital must show ₹40.00.
  4. **Void / Cancellation Check:**
     - Select an erroneous transaction and click `[Void]`.
     - Select reason: `PRINTER_HARDWARE_FAILURE`. Click Confirm.
     - Confirm status displays `VOID`.
     - Check Daily Revenue Summary: confirm voided amount is excluded from daily cash total while retaining the invoice sequence.
- **Pass Criteria:** Zero test data leakage into production, full audit trail on edits, multi-payment math verified, daily cash totals perfectly reconciled.
