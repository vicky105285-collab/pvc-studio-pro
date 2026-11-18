# Original Application Feature Matrix — Real PVC Card Pro

| Feature Category | Specific Capability | Implementation in Real PVC Card Pro | Status / Maturity |
|---|---|---|---|
| **Document Support** | e-Aadhaar (UIDAI) | Hardcoded crop coordinates for 2020-2024 layouts | Supported (Breaks on UIDAI layout changes) |
| | PAN Card (NSDL) | Fixed bounding box extraction of bottom card | Supported |
| | PAN Card (UTIITSL) | Fixed bounding box extraction | Supported |
| | Voter ID (e-EPIC) | Horizontal & vertical layout extraction | Supported |
| | Ayushman Bharat (PM-JAY) | National Health Authority card layout | Supported |
| | Driving Licence (Parivahan) | Fixed coordinate Parivahan portal crop | Partial (Fails on some state Parivahan variants) |
| | Vehicle RC Smart Card | Parivahan RC smart card format | Partial |
| | e-Shram Card | Ministry of Labour e-Shram layout | Supported |
| | ABHA Health Card | National Digital Health Mission card | Supported |
| | Jan Aadhaar | Rajasthan Jan Aadhaar layout | Supported |
| | Smart Ration Card | Tamil Nadu / general smart ration card layout | Partial (Static crop) |
| | Custom State Schemes | User-defined custom government schemes | **NOT SUPPORTED** |
| **PDF Processing** | Password-Protected PDF | Interactive password prompt | Supported |
| | Auto Password Detection | Derivation from name/DOB | Minimal (Hints only, manual entry required) |
| | Vector Text Extraction | Direct vector rendering | **NO** (Rasterizes entire page to low-res bitmap) |
| | Optical Character Recognition | Scanned PDF OCR fallback | **NOT SUPPORTED** |
| | Dynamic Border Detection | CV-based contour edge detection | **NOT SUPPORTED** (Fixed coordinates only) |
| | Perspective / Skew Fix | Scanned document deskewing | **NOT SUPPORTED** |
| **Photo Studio** | Passport Size (35×45 mm) | Aspect-ratio locked crop box | Supported |
| | Indian Visa / US Visa (50×50 mm) | Aspect-ratio locked crop box | Supported |
| | AI Background Removal | Local neural segmentation | **NO** (Basic RGB flood-fill / chromakey only) |
| | Signature Crop & Clean | Manual rectangle crop with thresholding | Basic |
| | Multi-Up Sheet Layouts | 4×6 (6, 8, 12 photos) and A4 sheets | Supported |
| **Hardware & Printing** | Epson L805 Tray | CD/DVD tray media profile | Supported |
| | Epson L8050 / L18050 | CD/DVD / ID card tray | Supported |
| | Canon G-series Tray | Multi-purpose tray | Supported |
| | CR80 Direct Card Printers | Native CR80 card printers (Zebra, Evolis) | Basic (Uses generic Windows driver fallback) |
| | Mirror / Dragon Sheet | Inverted print mode for laminating sheets | Supported |
| | Front/Back Duplex Flip | Manual flip alignment guides | Basic |
| | Calibration Wizard | X/Y offset numeric sliders with test print | Supported |
| **Business & Operations** | Daily Job Register | Tracks print jobs and customer receipts | **NOT SUPPORTED** |
| | Revenue / Counter Register | Daily earnings and card count tracking | **NOT SUPPORTED** |
| | Customer Search & History | Search past jobs or reprint customer cards | **NOT SUPPORTED** |
| | Language Localization | English & Tamil UI | **NO** (English only, non-standard UI controls) |
| | Data Security & DPDP Compliance | Auto-shredding temp files, masked Aadhaar | **NO** (Plain temporary image files left on disk) |
| | Offline Licensing | Permanent local operation without renewal | **NO** (Mandatory ₹500 annual renewal lock) |
