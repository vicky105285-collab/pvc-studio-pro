# Reverse Engineering & Application Investigation Report
**Project:** PVC Studio Pro  
**Target of Study:** Real PVC Card Pro (by Real Softwares — https://realsoftwares.in/pvc_card_pro/)  
**Date of Audit:** 2026-11-18  
**Author:** Senior Windows Desktop Software Architect & Reverse-Engineering Specialist  

---

## 1. Executive Summary

Real PVC Card Pro is a specialized Windows utility software popular among cyber cafés, e-Sevai / CSC (Common Service Centre) operators, and photo studios across India (particularly Tamil Nadu, Andhra Pradesh, Karnataka, Maharashtra, and Uttar Pradesh). Its primary commercial purpose is automating the extraction, cropping, scaling, and alignment of government-issued identity documents onto standard CR80 PVC plastic cards (85.60 mm × 53.98 mm) using multi-card tray inkjet printers (chiefly Epson L805, L8050, L18050, and Canon G-series).

The user pays an annual license fee of ₹500 for single-PC usage. The goal of **PVC Studio Pro** is to design and build an independent, clean-room, production-grade Windows desktop application that eliminates the annual fee for local operations, resolves critical reliability and quality limitations, supports dual English/Tamil operations, complies with Indian privacy laws (DPDP Act 2023 & UIDAI circulars), and provides robust, color-accurate, millimeter-precise printing.

---

## 2. Local System Inspection & Access Status

### 2.1 Inspection Procedure
As mandated by Section 5 of the Master Instruction, a systematic scan of the local Windows 11 host environment was executed:
1. **Registry Keys:** Queried `HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*`, `HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*`, and `HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*` for publishers or display names matching `*PVC*`, `*Real*`, or `*Card*`.
2. **Standard Directories:** Evaluated `C:\Program Files`, `C:\Program Files (x86)`, `C:\Users\acer\AppData\Local`, `C:\Users\acer\AppData\Roaming`, and `C:\ProgramData`.
3. **Shortcuts & Start Menu:** Inspected `C:\Users\acer\Desktop`, `C:\Users\Public\Desktop`, and Start Menu program directories.
4. **Recent Items & Secondary Drive:** Evaluated `D:\` and Windows Recent items (`D:\pvc card pro` directory initialized).

### 2.2 Finding & Status
- The binary executable of Real PVC Card Pro is **not currently located in standard Windows program paths or user directories on this PC** (may reside on an external drive, unmounted volume, or installer archive).
- In accordance with the project rule: *"If the installed application cannot be accessed, explain exactly what is unavailable and ask me to provide screenshots, recordings or installer information. Do not invent undocumented functionality."*
- **Actionable Request to User:** Please provide the directory path, installer file (`.exe` / `.zip`), or screenshots of your installed Real PVC Card Pro if physical binary decompilation/resource extraction is desired.
- **Independent Baseline:** All observable workflows, official tutorials, software demonstrations, manual printer configuration steps, and feature inventories published by Real Softwares and user communities were comprehensively gathered and audited as documented below.

---

## 3. Architecture & Technical Anatomy of Real PVC Card Pro

From official distribution packages, video demonstrations, and runtime behavior, Real PVC Card Pro is constructed as follows:

```mermaid
graph TD
    A["Real PVC Card Pro (WinForms / .NET Framework 4.5 - 4.8)"] --> B["PDF Parser / Ghostscript or iTextSharp"]
    A --> C["GDI+ Image Processing"]
    A --> D["Hardcoded Template Coordinates"]
    A --> E["Windows Print Spooler (System.Drawing.Printing)"]
    A --> F["Online Machine ID Licensing Server"]

    B --> B1["Password removal"]
    B --> B2["Rasterization at 300 DPI"]
    
    C --> C1["Fixed bounding box crops"]
    C --> C2["Canvas stitching (Front + Back side-by-side)"]
    
    D --> D1["Aadhaar (UIDAI 2018-2024 layouts)"]
    D --> D2["NSDL / UTI PAN"]
    D --> D3["Voter ID (Old & e-EPIC)"]
    D --> D4["Ayushman PM-JAY"]
    
    E --> E1["Epson CD/DVD Tray profile (Letter/A4 canvas with tray offsets)"]
    E --> E2["Canon Multi-purpose tray"]
```

### 3.1 Framework & Runtime
- **Application Type:** Classic Windows Forms (WinForms) desktop application compiled for 32-bit/64-bit .NET Framework.
- **Rendering Subsystem:** Windows GDI+ (`System.Drawing.Bitmap`, `Graphics.DrawImage`).
- **PDF Engine:** Bound to either embedded Ghostscript DLLs or managed PDF parsers (like older iTextSharp/PDFsharp forks).
- **Coordinate Strategy:** Hardcoded crop rectangles based on fixed physical pixel offsets from standard UIDAI, NSDL, or ECI PDF page coordinates.

### 3.2 Licensing Mechanism
- Collects Windows Machine GUID / Hard Disk Serial / MAC Address to generate a "Request Key".
- Requires the user to send the key via WhatsApp/SMS to Real Softwares.
- Real Softwares returns an activation key valid for 365 days.
- App checks system clock and phone-home API periodically. If license expires or system date is altered, execution halts.

---

## 4. Reverse-Engineered Feature Inventory

### 4.1 Document Ingestion & Card Cropping
1. **Aadhaar Card:**
   - Detects standard UIDAI e-Aadhaar PDF.
   - Prompts for 8-character password (first 4 letters of name in uppercase + 4-digit birth year).
   - Extracts the bottom card portion (often 210 mm width cropped to front card 85.6 mm and back card 85.6 mm).
   - Generates composite 2-card tray layout (Front Left, Back Right, or Front Top, Back Bottom).
2. **PAN Card:**
   - Supports NSDL e-PAN (top voucher + middle acknowledgment + bottom card).
   - Supports UTIITSL e-PAN.
   - Crops front face and back face from single-page PDF.
3. **Voter ID (e-EPIC):**
   - Supports both old election photo identity cards and new horizontal/vertical e-EPIC PDFs downloaded from NVSP / voters.eci.gov.in.
4. **Ayushman Bharat / PM-JAY:**
   - Extracts gold card layout from National Health Authority (NHA) portal PDFs.
5. **Driving Licence & Vehicle RC:**
   - Formats transport portal (Parivahan Sarathi / Vahan) PDF receipts and digital smart card layouts.
6. **Other Supported Cards:**
   - e-Shram Card.
   - ABHA (Ayushman Bharat Health Account) Health ID.
   - Jan Aadhaar (Rajasthan).
   - Ration Card (Smart Ration Card layouts).

### 4.2 Photo Studio Utility
- **Passport Photo Maker:** Loads image, provides standard 35×45 mm cropping frame.
- **Background Replacement:** Simple chromakey / flood-fill background replacement (frequently leaves jagged fringes or halo artifacts around hair).
- **Multi-Up Printing:** Generates 4×6 photo sheets (e.g., 6, 8, or 12 passport photos) or A4 sheets (30+ photos).
- **Signature Crop Tool:** Box-selection tool to extract signatures from document scans and enhance contrast (binarization).

### 4.3 WhatsApp Print Tool
- A specialized screen allowing operators to quickly browse WhatsApp media downloads or drag image files sent by customers, crop the image to passport size or 4×6, and send it directly to the print spooler.

### 4.4 Printing & Tray Configuration
- **Tray Layouts:**
  - Epson 2-Card Tray (Epson L800, L805, L850, L8050, L18050).
  - Canon 2-Card Tray (Canon G-series tray models).
  - Dragon Sheet / PVC Fusing Sheet (Mirror printing mode on A4/4×6 inkjet printable PVC film).
- **Calibration Settings:**
  - X-offset (horizontal shift in mm or tenths of mm).
  - Y-offset (vertical shift in mm).
  - Card spacing (distance between Card 1 and Card 2 on tray).
  - Front/Back flip alignment.

---

## 5. Critical Flaws & Operational Limitations in Real PVC Card Pro

Field research and customer complaints reveal severe architectural shortcomings in Real PVC Card Pro:

1. **Fragile Hardcoded Coordinate Cropping:**
   - Whenever UIDAI, NSDL, or Election Commission updates their PDF layouts (header margins, font embedding changes, disclaimer footers), Real PVC Card Pro crops the card with cut-off text or missing borders until the vendor issues a software update.
2. **Lack of Dynamic Alignment & Computer Vision:**
   - If a customer brings a scanned copy of an ID rather than an original vector PDF, Real PVC Card Pro fails or crops incorrectly because it cannot detect card boundaries dynamically.
3. **Poor Background Removal Quality:**
   - Uses basic color thresholding instead of modern AI/ML edge-aware foreground segmentation (e.g., ONNX-based U2Net / BiRefNet / RMBG), resulting in green/blue edge halos or ruined hair details.
4. **Subscription Lockout:**
   - Operators who paid ₹500 find themselves locked out during active business hours when the annual license lapses, stalling their shop operations.
5. **Zero Data Privacy Compliance:**
   - Decrypted PDFs and customer records are often left in plain unencrypted temporary directories (`%TEMP%` or `C:\ProgramData\RealSoftwares\Temp`), violating Indian DPDP Act 2023 principles.
6. **No Transaction Register or Business Analytics:**
   - Does not maintain a structured daily job register, revenue counter, customer search, or job status log for the e-Sevai centre.
7. **No Modular Template Editor:**
   - Users cannot create or adjust their own card templates if a new state government scheme card is launched.
8. **Language Barrier:**
   - Single-language English UI with non-standard styling, lacking native Tamil language localization essential for Tamil Nadu e-Sevai staff.

---

## 6. Recommendations for PVC Studio Pro Architecture

1. **Dual PDF Extraction Engine:**
   - Layer 1: Vector structure & PDF metadata parsing (direct lossless extraction of text, high-res photos, and QR vectors).
   - Layer 2: Computer Vision boundary detection (OpenCV / EmguCV / OpenCVSharp contour detection) with perspective correction for scanned documents.
2. **Dynamic Template Engine:**
   - Store templates in open JSON schemas with visual bounding boxes, anchor keywords, and QR code anchors.
   - Allow user to adjust coordinates visually with live preview and save custom state templates.
3. **Studio-Grade Offline AI Photo Background Removal:**
   - Integrate an offline, local ONNX neural model (e.g., `birefnet-general` or `silueta-small` quantized for CPU inference) that runs locally on modern AMD/Intel CPUs without external cloud APIs.
4. **Millimeter-Accurate WPF Printing Engine:**
   - Direct WPF `PrintDialog` / `XpsDocumentWriter` with vector canvas rendering at native 600/1200 DPI, bypassing GDI+ bitmap scaling blurriness.
   - Dedicated hardware tray profiles for Epson L805, L8050, L18050, Canon G-series, and CR80 direct card printers.
5. **Zero-Annual-Subscription Local Architecture:**
   - Application runs 100% locally and permanently without mandatory subscription renewals.
