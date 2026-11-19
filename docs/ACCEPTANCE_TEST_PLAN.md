# PVC Studio Pro — Acceptance Test Plan & Quality Gates
**Definitive Verification Protocol: Step-by-Step Acceptance Criteria & Quality Gates**

- **Date:** 2026-11-19
- **Auditor:** Senior Windows Desktop Systems & Print Engine Architect
- **Quality Mandate:** Unit tests alone do not establish completion. Every feature requires demonstrable end-to-end acceptance testing.

---

## 1. Master Acceptance Gates

Before any phase or release candidate is declared complete, it must pass all six quality gates:

```
[ Gate 1: Zero Code Regressions ] ────► 100% Passing Unit & Integration Tests (dotnet test)
[ Gate 2: Clean Self-Contained Build ] ─► Compiles standalone win-x64 with bundled runtime (No external .NET prompts)
[ Gate 3: Live Process Verification ] ──► Launches with working set <250 MB; zero startup exceptions
[ Gate 4: Interactive Acceptance Test ] ─► Passes all 6 End-to-End Manual Workflow Scripts below
[ Gate 5: Hardware Print Spool Test ] ──► Spools valid EMF/XPS bytes to Windows spooler with exact mm geometry
[ Gate 6: Zero Citizen PII Audit ] ─────► Zero citizen numbers or raw identity images written to disk or logs
```

---

## 2. End-to-End Acceptance Test Scripts

### Test Suite 1: Interactive Crop Engine Acceptance Test
- **Objective:** Verify interactive zoom, pan, aspect lock, sub-pixel cropping, and cross-workspace state propagation.
- **Test Procedure:**
  1. Open PVC Studio Pro and navigate to `Production Studio`.
  2. Click `Open Document (Ctrl+O)` and load high-resolution test scan `Synthetic_Aadhaar_300DPI.png`.
  3. Verify image is automatically fitted to window (`FitToWindow`).
  4. Click `Adjust Crop` to launch the crop engine.
  5. Use mouse scroll-wheel over citizen photo: confirm smooth zoom from $1.0\times$ to $3.5\times$ centered on mouse cursor.
  6. Click and drag the canvas: verify smooth panning without stutter.
  7. Drag top-right crop handle: verify boundary resizes smoothly while strictly maintaining the CR80 aspect ratio ($1.5858$).
  8. Click `Apply Crop`.
  9. Confirm the extracted card is normalized to exactly $2022 \times 1275\text{ px}$ ($85.60 \times 53.98\text{ mm}$ @ 600 DPI).
  10. Navigate to `Document Desk` and `Card Studio`: confirm the identical cropped card is immediately displayed with matching dimensions.
- **Pass Criteria:** Zero pixel jumping, aspect ratio strictly maintained, identical state reflected across all workspaces.

---

### Test Suite 2: Aadhaar Auto-Extraction & Alignment Acceptance Test
- **Objective:** Verify multi-factor Aadhaar detection, dual-card extraction, and manual fine-tuning.
- **Test Procedure:**
  1. Load password-protected test e-Aadhaar PDF.
  2. Verify in-place password banner appears with text field and show/hide toggle.
  3. Enter password and press Enter: verify document decrypts cleanly.
  4. Observe auto-detection badge: must display `UIDAI E-AADHAAR • AUTO-DETECTED (Confidence >95%)`.
  5. Inspect the center physical sheet canvas: confirm Front Card and Back Card appear mounted side-by-side on the A4 portrait sheet.
  6. Inspect Front Card: citizen photo, name, DOB, and gender must be 100% inside the card boundary with zero edge clipping.
  7. Inspect Back Card: address block and secure QR code must be fully visible with zero truncation.
  8. Click `Rotate Cards 90°`: confirm both cards rotate clockwise and sheet layout recalculates.
  9. Click `Swap Faces`: confirm Front and Back card positions exchange on the physical sheet.
- **Pass Criteria:** Both sides extracted accurately without manual ratio adjustments; zero aspect-ratio squishing.

---

### Test Suite 3: True Physical Print & Spooling Acceptance Test
- **Objective:** Verify that on-screen sheet preview, exported PDF, and physical print spooling are mathematically identical.
- **Test Procedure:**
  1. In `Production Studio`, select layout: `A4 Single Side (Side-by-Side)`.
  2. Verify on-screen physical sheet displays full A4 paper ($210 \times 297\text{ mm}$) fitted to window with zero scrolling required.
  3. Toggle `Cut Marks ☑`: confirm 0.2 mm hairline crop marks appear at the four corners of each card.
  4. Toggle `Mirror Mode ☑`: confirm cards flip horizontally (essential for thermal dragon sheet lamination).
  5. Click `Nudge Offset [►]` (+0.1 mm X): confirm live millimeter readout updates to `X: +0.1 mm`.
  6. Click `Export PDF`: save as `A4_Print_Verification.pdf`.
  7. Open `A4_Print_Verification.pdf` in a vector viewer: measure card positions. Front Card must be at $X=15.1\text{ mm}$, $Y=20.0\text{ mm}$; Back Card must be at $X=109.5\text{ mm}$, $Y=20.0\text{ mm}$.
  8. Select target printer `Microsoft Print to PDF` and click `START PRINTING`.
  9. Verify print job spools cleanly and entry is recorded in the financial ledger.
- **Pass Criteria:** Sub-millimeter parity between screen preview, exported PDF points, and print spooler raster.

---

### Test Suite 4: Passport Photo Studio & Biometric Engine Acceptance Test
- **Objective:** Verify ICAO 9303 compliance, clean GrabCut background removal without bottom scalloping, and genuine print spooling.
- **Test Procedure:**
  1. Navigate to `Passport Photo Studio`.
  2. Click `Import Portrait` and load customer test portrait `Portrait_Raw.jpg`.
  3. Verify automatic face detection places the subject's crown and chin within the ICAO guidelines.
  4. Inspect the bottom edge of the subject's shirt: **the bottom border must be completely straight and clean, with zero wavy scallops or undulating edge artifacts**.
  5. Click `Studio Blue`: confirm background replaces cleanly behind hair strands without blue haloing.
  6. Click and drag portrait in preview: confirm operator can intuitively nudge face alignment.
  7. Select layout: `4x6 Photo Paper (8-Up)`.
  8. Verify 8-up sheet composer renders 8 identical $35 \times 45\text{ mm}$ photos with dashed cut guides.
  9. Click `PRINT PHOTO SHEET`.
  10. Open Windows Print Queue: **confirm a real print job named `Passport_Photo_Sheet_8UP` is queued**.
  11. Navigate to `Ledger`: confirm transaction is recorded for ₹50.00.
- **Pass Criteria:** Zero wavy artifacts at bottom, real print job dispatched to Windows queue, financial ledger updated.

---

### Test Suite 5: Business Ledger, Corrections & Test Mode Acceptance Test
- **Objective:** Verify full transaction lifecycle, auditable edits, void/cancellations, and complete Test Mode isolation.
- **Test Procedure:**
  1. **Test Mode Isolation Check:**
     - Toggle `Test Mode` in application header.
     - Verify prominent orange warning banner appears: `⚠️ TEST MODE ACTIVE`.
     - Print 2 test cards for ₹140.00.
     - Open `JobLedgerView`: verify 2 test transactions appear.
     - Click `Reset Test Data`: confirm test records are wiped to 0.
     - Toggle back to `Production Mode`.
     - Open `JobLedgerView`: **confirm production database contains 0 test records and is completely uncorrupted**.
  2. **Auditable Edit Check (Production Mode):**
     - Record a production job for ₹70.00 (`INV-20261119-0001`).
     - Click `[Edit]` on `INV-20261119-0001`.
     - Change Fee to ₹50.00, enter reason: "Discount coupon applied". Click Save.
     - Confirm table displays ₹50.00.
     - Click `[Audit History]`: verify audit log records: `TotalAmount changed from 70.00 to 50.00 | Reason: Discount coupon applied | Operator: Admin`.
  3. **Partial Payment Check:**
     - Create transaction for ₹100.00, Amount Paid: ₹60.00.
     - Confirm status is `PARTIAL` and `Balance Due` is ₹40.00.
     - Click `[Add Payment]`: add ₹40.00 via UPI. Confirm status flips to `PAID` and Balance Due becomes ₹0.00.
  4. **Void / Cancellation Check:**
     - Select a transaction and click `[Void]`.
     - Select reason: `PRINTER_HARDWARE_FAILURE`. Click Confirm.
     - Confirm status displays `VOID` in red strike-through.
     - Check Daily Revenue Summary: confirm voided amount is excluded from daily cash total.
- **Pass Criteria:** Zero test data leakage into production, full audit trail on edits, accurate balance tracking, daily cash totals perfectly reconciled.
