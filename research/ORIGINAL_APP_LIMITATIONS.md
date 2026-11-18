# Original Application Limitations — Real PVC Card Pro

**Target Application:** Real PVC Card Pro  
**Analysis Date:** 2026-11-18  
**Author:** Senior Windows Desktop Software Architect  

---

## 1. Architectural & Engineering Limitations

### 1.1 Inflexible Coordinate Hardcoding
- **Vulnerability:** Real PVC Card Pro uses fixed pixel coordinates relative to the top-left corner of the first PDF page.
- **Consequence:** In India, government portals regularly alter page geometry. For example, when UIDAI updated the Aadhaar layout in 2023 with revised advisory warnings, cards cropped with older Real PVC Card Pro versions lost bottom address lines or the Aadhaar number. Operators were forced to halt operations until the vendor delivered an update.

### 1.2 Rasterization Degradation (Lack of Vector Rendering)
- **Problem:** To process documents, the software converts vector PDFs into intermediate GDI+ raster bitmaps (often 150 or 200 DPI to avoid memory bloat in 32-bit WinForms).
- **Result:** Small Tamil/English micro-text, QR codes, and fine lines become blurry when printed on high-resolution inkjet printers (5760×1440 optimized DPI on Epson L805/L8050). Barcode scanners and mAadhaar scanner apps frequently fail to read the printed QR code.

### 1.3 Primitive Background Replacement
- **Mechanism:** Employs standard RGB Euclidean distance / chromakey thresholding from an clicked background pixel.
- **Flaws:**
  - Cannot handle natural shadows, gradients, or uneven lighting typical of customer photos taken against shop walls.
  - Leaves green/blue color bleed or "halo" fringes around customer hair.
  - Chops off ears, spectacles, or hair tips.
  - Requires manual touch-up in Photoshop, destroying the utility's promised speed.

### 1.4 Scanned Document Incompetence
- **Failure Mode:** If a customer brings a photocopy, a mobile camera snapshot, or a scanned Aadhaar/PAN PDF instead of the original digital vector PDF, Real PVC Card Pro cannot detect card boundaries, deskew tilted edges, or normalize perspective distortion.

---

## 2. Operational & Business Limitations

### 2.1 The ₹500 Annual Subscription Lockout
- **Pain Point:** The software locks itself down after 365 days using an obfuscated hardware GUID.
- **Disruption:** If renewal fails or the vendor's WhatsApp helpline is delayed, an e-Sevai operator cannot print cards during peak morning hours, losing business and customer goodwill.

### 2.2 Complete Absence of Business Management Tools
- **Missing Infrastructure:**
  - No database of daily prints.
  - No record of customer transactions, revenue, or outstanding payments.
  - No print job receipt printing for citizens.
  - No repeat-print capability if a card gets scratched or misfeeds in the printer tray.
  - Operators are forced to maintain a separate physical notebook or Excel sheet.

### 2.3 Single Language (No Tamil Support)
- **Language Barrier:** Despite wide usage in Tamil Nadu, the entire interface is in English with technical jargon. e-Sevai staff who are more comfortable in Tamil struggle with calibration terms ("Offset", "DPI", "Tolerance", "Margin").

---

## 3. Privacy, Security & Legal Non-Compliance

### 3.1 DPDP Act 2023 & UIDAI Violations
- **Unencrypted Temporary Files:** Decrypted e-Aadhaar bitmaps containing full 12-digit unmasked Aadhaar numbers, biometric photos, and full residential addresses are written to Windows `%TEMP%` or application subfolders without encryption or immediate shredding.
- **Risk:** Any malware, local user, or technician servicing the PC can recover hundreds of citizen identity documents, creating severe regulatory liability for the e-Sevai centre.
- **No Masked Aadhaar Safeguard:** Lacks a reliable automated tool to generate legally compliant "Masked Aadhaar" cards (showing only `XXXX-XXXX-1234`) as recommended by UIDAI for non-statutory purposes.
