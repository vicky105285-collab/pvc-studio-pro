# Test Execution Report — PVC Studio Pro

**Release Target:** Release Candidate 1 (RC1)  
**Date:** 2026-11-18  
**Framework:** .NET 8.0 LTS (x64)  
**Test Runner:** xUnit.net v2.5.3 + VSTest 17.11.2  
**Test Results:** **37 Total, 37 Passed, 0 Failed, 0 Skipped (100% Pass Rate)**  
**Execution Duration:** 1.93 seconds  

---

## 1. Summary of Test Suites

| Test Fixture Class | Area Covered | Tests Run | Passed | Skipped | Failed |
|---|---|---|---|---|---|
| `PdfServiceTests` | PDF Inspection, Encryption, Passwords, Size Limits, 600 DPI Vector Rendering, CR80 Crop | 7 | 7 | 0 | 0 |
| `TemplateServiceTests` | 10 Indian ID Templates, Unknown Rejection, Low-Confidence Confirmation, Schema Validation | 18 | 18 | 0 | 0 |
| `VisionServiceTests` | OpenCV GrabCut Background Removal, 35x45mm Passport Framing, 4x6" 8-up Photo Grid, Card Deskew | 5 | 5 | 0 | 0 |
| `PrintServiceTests` | Epson 2-Card Tray Geometry, Dragon Sheet Mirror Mode, Millimeter Calibration Sheet PNG Export | 3 | 3 | 0 | 0 |
| `SecurityServiceTests` | Memory Zeroization, Native Buffer Wiping, Zero PII Validation | 2 | 2 | 0 | 0 |
| `JobLedgerServiceTests` | Daily Cash & UPI Reconciliation, Multi-Threaded Concurrency (10 threads), Zero-PII Schema Verification | 3 | 3 | 0 | 0 |
| **TOTAL** | | **37** | **37** | **0** | **0** |

---

## 2. Key Verified Behaviors & Security Assertions

### A. Template Matching & Safety Gate
- **Zero Silent Fallback:** Verified that random, non-government text (e.g. utility bills) returns `IsSuccess = false`, `Template = null`, and `MatchReason = "Document keywords did not match any supported Indian Government identity template."`.
- **10 Agnostic Templates:** Verified that distinct keywords match each individual template without overlap or misclassification:
  1. `aadhaar_v2024`
  2. `pan_nsdl`
  3. `pan_utiitsl`
  4. `voter_epic_v2`
  5. `ayushman_pmjay`
  6. `tn_smart_ration`
  7. `parivahan_dl`
  8. `parivahan_rc`
  9. `eshram_card`
  10. `abha_health_id`
- **Ambiguity Guard:** Verified that documents with only single or weak keywords set `RequiresOperatorConfirmation = true` with confidence score $< 0.70$.
- **Validation Engine:** Confirmed that templates with negative dimensions, empty IDs, or missing front crops are strictly rejected.

### B. PDF Processing & DoS Protection
- **Resource Bounds:** Enforced max stream size of 150 MB and max page count of 50. Verified that oversized streams throw `PdfSecurityException`.
- **Encryption Handshake:** Verified that encrypted documents without passwords trigger `PdfPasswordRequiredException`.
- **Cancellation:** Verified that pre-cancelled tokens immediately terminate document rendering.
- **Dimensional Fidelity:** 600 DPI vector rendering confirmed to produce exact CR80 pixel dimensions ($2022 \times 1275$ px) with zero blur or raster artifacts.

### C. Photo Studio & Computer Vision
- **GrabCut Matting:** Dual-color synthetic portrait test confirmed that outer background pixels are replaced by target studio background color while center foreground pixels retain original color.
- **Passport Sizing:** Output verified at standard $35 \times 45\text{ mm}$ ($827 \times 1063\text{ px}$ at 600 DPI) with 0.2mm boundary hairline.
- **Sheet Composition:** 8-up photo layout verified on standard 4x6" photo paper ($3600 \times 2400\text{ px}$ at 600 DPI) with cut guidelines.

### D. Zero-PII Ledger & Concurrency
- **Zero Identity Storage:** Schema inspection of SQLite `print_jobs` table verified the total absence of customer names, Aadhaar numbers, phone numbers, addresses, photos, or biometric fields. Only operational billing fields exist (`job_id`, `document_type`, `total_amount`, `payment_mode`, `created_at`).
- **Concurrent Writes:** 10 simultaneous threads executing `RecordJobAsync` under SQLite WAL mode completed without `database is locked` exceptions, recording 10 jobs and calculating exact financial totals.
