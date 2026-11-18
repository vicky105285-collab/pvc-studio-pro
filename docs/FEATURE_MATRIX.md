# Master Feature Matrix & Implementation Inventory — PVC Studio Pro

| Module ID | Feature Name | Description | Source / Origin | Target Release Phase | Implementation Priority |
|---|---|---|---|---|---|
| **GOV-01** | e-Aadhaar Vector Crop | High-res 600 DPI vector cropping of Front and Back faces | Real PVC Card Pro & Market | Phase 6 (Core) | **P0 (Critical)** |
| **GOV-02** | Aadhaar Masking Tool | 1-Click compliant mask over first 8 digits (`XXXX-XXXX-1234`) | Indian DPDP / UIDAI | Phase 6 (Core) | **P0 (Critical)** |
| **GOV-03** | PAN Card Crop (NSDL/UTI) | Vector cropping and binarization of PAN vouchers | Real PVC Card Pro & Market | Phase 6 (Core) | **P0 (Critical)** |
| **GOV-04** | Voter ID (e-EPIC) | Extraction of horizontal and vertical e-EPIC formats | Real PVC Card Pro & Market | Phase 6 (Core) | **P0 (Critical)** |
| **GOV-05** | Ayushman Bharat (PM-JAY) | National Health Authority gold card formatting | Real PVC Card Pro & Market | Phase 6 (Core) | **P0 (Critical)** |
| **GOV-06** | Parivahan Driving Licence | Multi-state smart driving licence card generation | Real PVC Card Pro & Market | Phase 6 (Core) | **P1 (High)** |
| **GOV-07** | Vehicle Registration (RC) | Smart vehicle registration certificate formatting | Real PVC Card Pro & Market | Phase 6 (Core) | **P1 (High)** |
| **GOV-08** | TN Smart Ration Card | Tamil Nadu e-PDS smart family card layout | **New e-Sevai Feature** | Phase 6 (Core) | **P0 (Critical)** |
| **GOV-09** | Central Scheme Cards | e-Shram, ABHA Health ID, Jan Aadhaar, Farmer ID | Market Competitors | Phase 7 (Advanced) | **P1 (High)** |
| **GOV-10** | Scanned Card CV Deskew | 4-point perspective warp and deskewing for paper scans | **New AI/CV Innovation** | Phase 7 (Advanced) | **P1 (High)** |
| **PRN-01** | Epson L805 Tray Engine | Exact CD/DVD 2-card slot layout with hardware offsets | Real PVC Card Pro | Phase 6 (Core) | **P0 (Critical)** |
| **PRN-02** | Epson L8050 / L18050 | Native ID card tray profile for modern EcoTank printers | Market Competitors | Phase 6 (Core) | **P0 (Critical)** |
| **PRN-03** | Canon G-Series Multi-Tray | Disc / ID card tray layout for Canon MegaTank G-series | Market Competitors | Phase 6 (Core) | **P0 (Critical)** |
| **PRN-04** | CR80 Direct Card Printers | Native edge-to-edge CR80 feed hopper profile | Market Competitors | Phase 6 (Core) | **P1 (High)** |
| **PRN-05** | Dragon Sheet / Mirror Mode | 1-Click horizontal mirror inversion for PVC fusing | Real PVC Card Pro | Phase 6 (Core) | **P0 (Critical)** |
| **PRN-06** | Interactive Calibration Wizard | Printed millimeter test pattern with live offset adjustment | Real PVC Card Pro (Enhanced) | Phase 6 (Core) | **P0 (Critical)** |
| **PRN-07** | Dual-Sided Flip Guide | Interactive visual guide for manual flip tray printing | **New e-Sevai Feature** | Phase 6 (Core) | **P1 (High)** |
| **PHO-01** | Passport Cropping Presets | Indian Passport (35×45mm), Visa (50×50mm), Stamp size | Real PVC Card Pro | Phase 7 (Advanced) | **P0 (Critical)** |
| **PHO-02** | Local AI Background Removal | Zero-cloud ONNX neural edge matting with color fill | **New AI Innovation** | Phase 7 (Advanced) | **P0 (Critical)** |
| **PHO-03** | Signature Binarization | Clean signature extraction with adaptive thresholding | Real PVC Card Pro (Enhanced) | Phase 7 (Advanced) | **P1 (High)** |
| **PHO-04** | Multi-Up Sheet Compositor | 4×6 (6, 8, 12 photos) and A4 sheets with cut-lines | Real PVC Card Pro | Phase 7 (Advanced) | **P0 (Critical)** |
| **DSG-01** | Blank Card Designer | Front & back CR80 canvas with text, images, shapes | IDGenius Pro | Phase 7 (Advanced) | **P1 (High)** |
| **DSG-02** | Barcode & QR Generator | Generates Code 128, QR Code, and DataMatrix | IDGenius Pro | Phase 7 (Advanced) | **P1 (High)** |
| **BAT-01** | Excel / CSV Batch Import | Batch generation of student/employee PVC cards | IDGenius Pro | Phase 7 (Advanced) | **P2 (Medium)** |
| **WHA-01** | WhatsApp Ingestion Watcher | Watched folder for fast drag-and-drop WhatsApp photos | Real PVC Card Pro | Phase 7 (Advanced) | **P1 (High)** |
| **BIZ-01** | Daily Job Register | Embedded SQLite transaction log for e-Sevai prints | **New e-Sevai Feature** | Phase 7 (Advanced) | **P0 (Critical)** |
| **BIZ-02** | Revenue Analytics Dashboard | Daily card count, cash intake, and service summaries | **New e-Sevai Feature** | Phase 7 (Advanced) | **P1 (High)** |
| **BIZ-03** | Thermal Receipt Generator | 58mm/80mm POS receipt printing for citizen billing | **New e-Sevai Feature** | Phase 7 (Advanced) | **P1 (High)** |
| **BIZ-04** | 1-Click Reprint Manager | Instant reprint from local job history with retention rules | **New e-Sevai Feature** | Phase 7 (Advanced) | **P1 (High)** |
| **SEC-01** | 100% Offline Architecture | Zero cloud communication; zero telemetry | Core Requirement | Phase 6 (Core) | **P0 (Critical)** |
| **SEC-02** | In-Memory PDF Shredding | Memory stream decryption; zero unencrypted temp files | Indian DPDP Act 2023 | Phase 6 (Core) | **P0 (Critical)** |
| **SEC-03** | DPAPI Database Encryption | Windows DPAPI encryption of citizen PII at rest | Indian DPDP Act 2023 | Phase 6 (Core) | **P0 (Critical)** |
| **SEC-04** | Backup & Restore Wizard | 1-Click encrypted local archive of templates and ledger | Core Requirement | Phase 7 (Advanced) | **P1 (High)** |
| **UI-01** | Windows 11 Fluent UI | Mica/Acrylic backdrops, NavigationView, Fluent Icons | Core Requirement | Phase 5 (UX) | **P0 (Critical)** |
| **UI-02** | Bilingual English & Tamil | Instant runtime language toggle (English / தமிழ்) | **New e-Sevai Feature** | Phase 5 (UX) | **P0 (Critical)** |
| **UI-03** | High-Contrast & Dark Mode | WCAG 2.1 AA compliant dark, light, and high-contrast | Accessibility Skill | Phase 5 (UX) | **P1 (High)** |
