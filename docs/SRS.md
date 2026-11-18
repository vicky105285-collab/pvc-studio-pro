# Software Requirements Specification (SRS) — PVC Studio Pro

**Standard:** IEEE 830-1998 Format  
**Version:** 1.0.0-DRAFT  
**Date:** 2026-11-18  

---

## 1. System Requirements & Constraints

### 1.1 Hardware Minimum & Recommended Specifications
- **Operating System:** Windows 10 (Version 1809+) or Windows 11 (64-bit, x64 architecture).
- **Processor:** Dual-core 2.0 GHz (AMD or Intel) minimum; Quad-core 3.0 GHz recommended for rapid AI background removal.
- **RAM:** 4 GB minimum; 8 GB recommended.
- **Storage:** 500 MB free disk space for application binaries and ONNX models.
- **Display:** 1280 × 720 minimum screen resolution; 1920 × 1080 (100% or 125% DPI scaling) recommended.
- **Printers Supported:** Epson L805, L8050, L18050, Canon G-series, standard Windows inkjets, and CR80 thermal card printers.

---

## 2. Functional Requirements (FR)

### FR-01: Document Import & Security
- **FR-01.1:** System shall accept PDF, JPEG, PNG, BMP, TIFF, and WEBP formats.
- **FR-01.2:** System shall prompt for password upon detecting an encrypted PDF.
- **FR-01.3:** System shall provide automatic password hints (e.g. `NAME1990` for Aadhaar, `DDMMYYYY` for PAN).
- **FR-01.4:** Decrypted PDF content shall reside exclusively in RAM memory streams and shall not be persisted in plaintext on disk.

### FR-02: Card Extraction & Rendering
- **FR-02.1:** System shall render vector PDF elements at a user-selected resolution (default 600 DPI, range 300–1200 DPI).
- **FR-02.2:** System shall crop Front and Back faces according to template schemas defined in `templates.json`.
- **FR-02.3:** System shall support 1-click Masked Aadhaar generation obscuring the first 8 digits.
- **FR-02.4:** For scanned documents, system shall execute contour edge detection and 4-point perspective warp to deskew tilted cards.

### FR-03: Printing Engine & Tray Control
- **FR-03.1:** System shall render print jobs directly into the Windows XPS/GDI print spooler.
- **FR-03.2:** System shall provide independent calibration profiles per printer storing X-offset, Y-offset, and Card-gap in millimeters.
- **FR-03.3:** System shall support 1-click horizontal mirror inversion for Dragon Sheet PVC fusing workflows.
- **FR-03.4:** System shall validate selected paper size and tray configuration prior to job dispatch, raising an alert if media type mismatches.

### FR-04: Photo Studio & Local AI Matting
- **FR-04.1:** System shall provide standard crop presets: Indian Passport (35×45 mm), US/Schengen Visa (50×50 mm), Stamp Size (20×25 mm).
- **FR-04.2:** System shall execute local AI background removal using an embedded ONNX model in under 2.5 seconds on CPU.
- **FR-04.3:** System shall generate multi-up printable photo sheets on 4×6 photo paper (6, 8, or 12 copies) and A4 paper with customizable grid margins and cut-lines.

### FR-05: e-Sevai Business Ledger
- **FR-05.1:** System shall automatically record every completed print job in an embedded SQLite database.
- **FR-05.2:** System shall display daily and monthly aggregated summaries of card counts, service types, and revenue.
- **FR-05.3:** System shall generate customizable 58mm/80mm thermal receipts or digital PDF receipts.

### FR-06: Localization & Accessibility
- **FR-06.1:** System shall support instant runtime language switching between English and Tamil.
- **FR-06.2:** All interactive buttons and fields shall support standard keyboard shortcuts (`Ctrl+P`, `Ctrl+O`, `Ctrl+S`, `Tab`, `Enter`, `Esc`).
- **FR-06.3:** Contrast ratios between text and background surfaces shall comply with WCAG 2.1 AA (minimum 4.5:1).

---

## 3. Non-Functional Requirements (NFR)

- **NFR-01 (Performance):** PDF import, parsing, and preview rendering shall take less than 500 milliseconds for standard 1-page identity PDFs.
- **NFR-02 (Reliability):** Application shall operate 100% offline with zero dependency on internet connectivity or external licensing servers.
- **NFR-03 (Security):** Stored customer personal data shall be encrypted using Windows DPAPI. Temporary in-memory buffers shall be zeroized upon job disposal.
- **NFR-04 (Maintainability):** Codebase shall follow clean MVVM separation, dependency injection, and achieve >80% unit test code coverage on business logic.
