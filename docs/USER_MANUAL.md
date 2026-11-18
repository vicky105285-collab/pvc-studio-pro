# PVC Studio Pro — Comprehensive Operator Manual & User Guide
## பயனர் வழிகாட்டி மற்றும் செயல்பாட்டுக் கையேடு

**Product Name:** PVC Studio Pro  
**Version:** 1.0.0  
**Target Audience:** e-Sevai Maiyam Operators, Commercial PVC Print Centers, Photo Studios  
**Primary Region:** Tamil Nadu & Pan-India  
**Languages:** English & தமிழ் (Tamil)  
**Date:** 2026-11-18  

---

## 1. Introduction / அறிமுகம்

### English
**PVC Studio Pro** is an independent, 100% offline Windows desktop application designed specifically for commercial e-Sevai centres and print shops. It eliminates recurring annual software subscriptions while providing higher print resolution (600 DPI vector rendering), automated alignment for Indian identity cards, UIDAI-compliant privacy masking, and local business transaction tracking.

### தமிழ்
**பிவிசி ஸ்டுடியோ ப்ரோ** என்பது இ-சேவை மையங்கள் மற்றும் அச்சு நிலையங்களுக்காக பிரத்யேகமாக உருவாக்கப்பட்ட முழுமையான ஆஃப்லைன் விண்டோஸ் மென்பொருளாகும். இது வருடாந்திர கட்டணங்கள் ஏதுமின்றி, 600 DPI உயர் தரத்தில் இந்திய அரசு அடையாள அட்டைகளை (ஆதார், பான், வாக்காளர் அட்டை, ரேஷன் அட்டை போன்றவை) பிவிசி அட்டைகளில் அச்சிட உதவுகிறது.

---

## 2. Supported Documents / ஆதரிக்கப்படும் ஆவணங்கள்

The system automatically recognizes and crops the following 10 document formats:
1. **e-Aadhaar (UIDAI):** Standard PDF with QR code and address block.
2. **NSDL / UTIITSL PAN Card:** Front and back cropping with signature preservation.
3. **e-EPIC (Voter ID - Election Commission of India):** Bilingual voter cards.
4. **Ayushman Bharat (PM-JAY / ABHA):** National health cards with photo and ABHA ID.
5. **Tamil Nadu Smart Ration Card (TNPDS):** Family head photo, NFSA quota, QR code.
6. **Parivahan Driving Licence (DL):** Standard transport card format.
7. **Parivahan Registration Certificate (RC):** Vehicle smart card format.
8. **e-Shram Card:** Ministry of Labour worker identity.
9. **ABHA Health ID Card:** National Digital Health Mission card.
10. **Custom CR80 Identity Cards:** School, college, and staff identity cards.

---

## 3. Step-by-Step Operator Workflows / செயல்பாட்டு முறைகள்

### Workflow 1: Standard Card Printing (அட்டை அச்சிடுதல்)
1. **Load Document:** Press `Ctrl+O` or click **Open Document** and select the customer's PDF file.
2. **Auto-Detection:** The software automatically detects the document type and displays the front and back faces in the central CR80 canvas.
3. **Aadhaar Masking (Optional):** Check **Mask Aadhaar (XXXX-XXXX-1234)** to automatically redact the first 8 digits of the Aadhaar number in accordance with UIDAI regulations.
4. **Select Printer & Tray:** Choose your printer (e.g., *Epson L805*) and tray type (*Epson 2-Card Tray*).
5. **Print:** Press `Ctrl+P` or click **PRINT NOW**. The job is rendered at native 600 DPI and spooled directly to the Windows print queue.
6. **Accounting:** The transaction amount is automatically logged in the Business Ledger.

### Workflow 2: Hardware Millimeter Calibration (அச்சுப்பொறி அளவீடு)
1. Navigate to **Printer Setup & Calibration** from the left sidebar.
2. Insert your tray loaded with two test cards into the printer.
3. Click **Print Test Calibration Sheet**.
4. Examine the printed millimeter rulers and corner crosshairs.
5. If the print is shifted (e.g., 0.5 mm to the left), adjust the **Offset X** slider by `+0.50 mm`.
6. Click **Save Calibration Profile**. All future print jobs will apply this physical offset automatically.

### Workflow 3: Passport Photo Studio (பாஸ்போர்ட் புகைப்பட அரங்கு)
1. Load a customer photograph.
2. Toggle **Remove Background** if the customer requires a clean white or passport-blue studio background.
3. Select your sheet layout (e.g., **8 Photos on 4x6" Glossy Paper**).
4. Click **Print Photo Sheet**. Photos will print with fine dashed cutting guidelines.

### Workflow 4: Daily Business Ledger & Receipts (கணக்குப் பதிவேடு)
1. Open **Business Ledger** from the sidebar.
2. Review today's metrics:
   - Total Cards Printed
   - Total Cash Revenue (₹)
   - Aadhaar count vs. PAN count
3. Click **Print 58mm/80mm Receipt** next to any completed job to issue a physical thermal receipt to the customer.

---

## 4. Tray Compatibility Guide / தட்டு வழிகாட்டி

| Printer Model | Recommended Tray Type | Paper Size Setting in Driver | Target Slots |
|---|---|---|---|
| **Epson L805** | `EPSON_2CARD_TRAY` | CD/DVD Tray (140 x 297 mm) | 2 CR80 Cards (Top & Bottom) |
| **Epson L8050** | `EPSON_2CARD_TRAY` | CD/DVD Tray (140 x 297 mm) | 2 CR80 Cards (Top & Bottom) |
| **Epson L18050** | `EPSON_2CARD_TRAY` | CD/DVD Tray (140 x 297 mm) | 2 CR80 Cards (Top & Bottom) |
| **Canon G570 / G670** | `CANON_MULTI_TRAY` | Multi-Purpose Disc Tray | 2 CR80 Cards |
| **Direct PVC (Zebra/Evolis)** | `CR80_DIRECT` | CR80 / ISO 7810 ID-1 | 1 Card (Direct Feed) |
| **Lamination Heat Press** | `DRAGON_SHEET_A4` | Plain A4 (210 x 297 mm) | 10 Cards (Mirror Mode) |

---

## 5. Privacy, Security & Legal Compliance / பாதுகாப்பு விதிகள்

- **Zero Citizen PII Retention:** No customer names, photos, or Aadhaar numbers are stored on disk or in the database.
- **Volatile Memory Sanitization:** All decrypted PDF byte streams and uncompressed bitmaps in memory are zeroized using Win32 `SecureZeroMemory` after rendering.
- **DPDP Act 2023 & UIDAI Guidelines:** Fully compliant with Indian data protection laws. No telemetry or external server communication occurs.

---

## 6. Technical Support & Karur Centre Operations

For local support and template configuration inquiries:
- **e-Sevai Operations Center:** Karur, Tamil Nadu, India
- **Application Directory:** `%LocalAppData%\Programs\PVC Studio Pro\`
- **Local Ledger Database:** `%LocalAppData%\PVCStudioPro\data\pvc_studio.db`
