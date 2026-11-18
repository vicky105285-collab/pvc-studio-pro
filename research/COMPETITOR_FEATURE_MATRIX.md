# Comprehensive Competitor Feature Matrix

| Capability / Module | Real PVC Card Pro | PVC2Print | Print Perfect AI | Easy Crop | IDGenius Pro | PVC Studio Pro (Our Target) |
|---|---|---|---|---|---|---|
| **Core Government Cards** |
| Aadhaar (e-Aadhaar PDF) | Yes (Static) | Yes | Yes (AI crop) | Yes (Static) | Yes | **Yes (Vector + Adaptive CV + Masking)** |
| PAN Card (NSDL & UTI) | Yes | Yes | Yes | Yes | Yes | **Yes (Lossless Vector + Barcode scan)** |
| Voter ID (e-EPIC) | Yes | Yes | Yes | Yes | Yes | **Yes (Horizontal & Vertical)** |
| Ayushman Bharat (PM-JAY) | Yes | Yes | Yes | Yes | Yes | **Yes (Gold layout & State variants)** |
| Parivahan DL / Vehicle RC | Partial | Yes | Yes | Partial | Yes | **Yes (Multi-state templates)** |
| Tamil Nadu Smart Ration Card | Partial | No | No | No | No | **Yes (Native TN PDS Template)** |
| Other State Schemes (e-Shram, ABHA, Jan Aadhaar) | Yes | Yes | Yes | Minimal | Partial | **Yes (Extensible JSON Template Registry)** |
| Scanned / Photo ID Cards | No | No | Partial | No | No | **Yes (OpenCV Boundary & Deskew)** |
| **PDF & Vision Processing** |
| Password Removal & Hinting | Manual entry | Auto/Manual | Auto/Manual | Manual | Manual | **Auto-Hint + Password Vault + Pattern Guess** |
| Lossless Vector Rendering | No (GDI+ bitmap) | Partial | Yes | No | Yes | **Yes (High-Res 600-1200 DPI PDFium Vector)** |
| Dynamic Contour Edge Detection | No | No | Yes | No | No | **Yes (Integrated OpenCVSharp)** |
| Perspective & Skew Correction | No | No | Partial | No | No | **Yes (4-point Quad Perspective Warp)** |
| **Photo Studio & Editing** |
| Passport Dimensions (35×45mm, Visa) | Yes | Yes | Yes | No | Yes | **Yes (Comprehensive Global & Indian Presets)** |
| AI Background Removal (Local Offline) | No (RGB keying) | Cloud API | Yes (Bundled) | No | No | **Yes (Local ONNX Neural Segmentation - No Cloud)**|
| Signature Extraction & Binarization | Basic | No | Basic | No | Yes | **Yes (Adaptive Otsu / Sauvola Thresholding)** |
| Multi-Up Photo Sheet Layouts | 4×6, A4 | 4×6 | 4×6, A4 | No | A4 | **Yes (4×6, 5×7, A4, Custom Multi-Up with Cutlines)**|
| **Hardware & Printing Integration** |
| Epson L805 Tray Profile | Yes | Yes | Yes | Yes | Yes | **Yes (Pre-calibrated 2-card offset)** |
| Epson L8050 / L18050 Tray Profile | Yes | Yes | Yes | Partial | Yes | **Yes (Pre-calibrated native slot offsets)** |
| Canon G-Series Tray Profile | Yes | Yes | Yes | Partial | Yes | **Yes (Pre-calibrated Disc/ID profile)** |
| CR80 Direct Card Printers (Zebra, Evolis) | Generic | Yes | Generic | No | Yes | **Yes (Native Edge-to-Edge CR80 Paper Size)** |
| Dragon Sheet (Mirror Inverted Mode) | Yes | Yes | Yes | Yes | Yes | **Yes (1-Click Mirror Flip for Dragon/Lamination)**|
| Dual-Sided Flip Guide / Duplex | Basic | Yes | Basic | Basic | Yes | **Yes (Interactive Visual Step-by-Step Flip Guide)**|
| Interactive Calibration Wizard | Yes (Sliders) | Yes | Yes | Basic | Yes | **Yes (Visual Interactive Calibration with Ruler)** |
| **Business & Workflow Modules** |
| Customer Transaction Register | No | No | No | No | Partial | **Yes (Local SQLite Register with Date & Amounts)**|
| Daily Revenue & Card Analytics | No | No | No | No | No | **Yes (Daily/Monthly Summary Dashboard)** |
| Customer Receipt Generation | No | No | No | No | Yes | **Yes (Thermal Receipt / A4 / WhatsApp PDF Receipt)**|
| Repeat Print & Reprint History | No | No | No | No | No | **Yes (1-Click Reprint with Retention Rules)** |
| Dual Language UI (English + Tamil) | No (Eng only) | No | No | No | No | **Yes (Full English & Native Tamil Localization)**|
| WhatsApp Desktop Fast Import | Basic | No | No | No | No | **Yes (Watched Folder + Drag-and-Drop Ingestion)** |
| **Security & Privacy** |
| 100% Offline Local Processing | Partial | No (Needs Web)| Yes | Yes | Yes | **Yes (Zero Cloud Dependency, Strict Local)** |
| Masked Aadhaar Generation | Manual | No | No | No | No | **Yes (1-Click Compliant Masking: XXXX-XXXX-1234)**|
| Automatic Temp File Shredding | No (Leaves files)| No | No | No | No | **Yes (DoD/NIST In-Memory Shredding on Job Exit)** |
| DPDP Act 2023 Compliance Architecture| No | No | No | No | No | **Yes (Purpose Limitation, Encrypted Retained DB)**|
| **Commercial Licensing** |
| Pricing Model | ₹500/year | ₹500-1000/yr | ₹1,500-2,500 | ₹499-999 | ₹2,000+ | **Free Local Core / One-Time Lifetime License** |
| Sudden Renewal Lockout | High risk | High risk | No | No | No | **Zero Risk (Permanent Local Offline Operation)** |
