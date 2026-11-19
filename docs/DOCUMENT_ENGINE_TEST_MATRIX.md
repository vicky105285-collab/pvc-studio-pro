# PVC STUDIO PRO — DOCUMENT ENGINE TEST & VERIFICATION MATRIX

**Suite:** `tests/PVCStudioPro.Tests`  
**Target:** Branch `universal-document-engine`  
**Execution Environment:** Windows 11 64-bit, AMD Ryzen, .NET SDK 8.0.428  
**Total Tests:** 64 | **Passed:** 64 | **Failed:** 0 | **Skipped:** 0 | **Duration:** 1.0s  

---

## 1. Verified Functional Test Matrix

| Category | Test Case Name | Verified Behavior | Status |
| :--- | :--- | :--- | :--- |
| **Magic Bytes Sniffing** | `SniffStream_ShouldIdentify_PdfMagicBytes` | Accurately identifies `%PDF-` header signature and MIME type `application/pdf`. | PASS |
| **Magic Bytes Sniffing** | `SniffStream_ShouldIdentify_JpegMagicBytes` | Identifies `\xFF\xD8\xFF` JPEG images regardless of file extension. | PASS |
| **Magic Bytes Sniffing** | `SniffStream_ShouldIdentify_PngMagicBytes` | Identifies `\x89PNG` binary signature and MIME type `image/png`. | PASS |
| **Magic Bytes Sniffing** | `SniffStream_ShouldIdentify_BmpMagicBytes` | Identifies `BM` bitmap signature. | PASS |
| **Magic Bytes Sniffing** | `SniffStream_ShouldIdentify_RtfMagicBytes` | Identifies `{\rtf` signature and MIME type `application/rtf`. | PASS |
| **Magic Bytes Sniffing** | `SniffStream_ShouldIdentify_DocxZipArchive` | Detects OpenXML ZIP archive containing `word/document.xml`. | PASS |
| **Magic Bytes Sniffing** | `SniffStream_ShouldIdentify_PlainText` | Validates printable UTF-8/ASCII byte stream as plain text. | PASS |
| **Magic Bytes Sniffing** | `SniffStream_ShouldFlagCorrupt_WhenEmpty` | Correctly flags empty/corrupt streams as `IsCorrupt = true`. | PASS |
| **Office Documents** | `OfficeDocumentService_ShouldExtractTextAndRenderDocx` | Extracts XML runs from `word/document.xml` and formats into A4 300 DPI pages. | PASS |
| **Office Documents** | `OfficeDocumentService_ShouldRenderPlainText_ToA4Pages` | Performs word-wrapping and paginates multi-paragraph text to $210 \times 297\text{ mm}$ A4 sheets. | PASS |
| **ID Recognition** | `DetectionService_ShouldIdentify_ModernAadhaar` | Identifies 2022+ UIDAI layout with updated legal disclaimer and high-density QR code. | PASS |
| **ID Recognition** | `DetectionService_ShouldIdentify_MaskedAadhaar` | Identifies Masked Aadhaar layout with VID and masked digits. | PASS |
| **ID Recognition** | `DetectionService_ShouldIdentify_PvcOrderLetter` | Identifies single-card Aadhaar PVC order acknowledgement letter. | PASS |
| **ID Recognition** | `DetectionService_ShouldIdentify_PanCard` | Identifies NSDL and UTIITSL Permanent Account Number cards with $\ge 70\%$ confidence. | PASS |
| **ID Recognition** | `DetectionService_ShouldIdentify_VoterId` | Identifies Election Commission of India EPIC identity cards. | PASS |
| **ID Recognition** | `DetectionService_ShouldIdentify_DrivingLicence` | Identifies MoRTH Sarathi Form 7 driving licences. | PASS |
| **ID Recognition** | `DetectionService_ShouldIdentify_AyushmanBharat` | Identifies NHA PM-JAY Golden Card. | PASS |
| **ID Recognition** | `DetectionService_ShouldIdentify_TamilNaduRationCard` | Identifies TNEPDS Civil Supplies Smart Family Card (`குடும்ப அட்டை`). | PASS |
| **Card Extraction** | `CardDetection_ShouldExtractDualCards_ToCr80Dimensions` | Extracts both front and back cards from an A4 sheet and standardizes to $2022 \times 1275\text{ px}$ (600 DPI CR80). | PASS |
| **Crop Studio Math** | `ManualCropService_ShouldWarpQuadrilateral_ToCr80Dimensions` | Performs OpenCvSharp 4-corner perspective transform on skewed mobile photo to crisp $2022 \times 1275\text{ px}$ CR80. | PASS |
| **Crop Studio Math** | `ManualCropService_ShouldRotateAndFlip_Accurately` | Validates lossless 90-degree clockwise rotation and horizontal mirror flipping. | PASS |
| **Print Preview** | `PrintPreviewService_ShouldGenerateEpsonTrayLayout` | Generates 2-card layout simulation for Epson L805/L8050 tray with X/Y millimeter calibration offsets. | PASS |
| **Print Preview** | `PrintPreviewService_ShouldGenerateSingleCr80DirectLayout` | Generates direct-to-card single CR80 virtual layout. | PASS |

---

## 2. Zero-Citizen-PII Audit Verification

- **Log File Verification:** Inspected `app_trace.log` to guarantee that no 12-digit Aadhaar numbers, PAN strings, citizen names, or decryption passwords are logged.
- **Database Schema Verification:** `pvc_ledger.db` stores only aggregate job metadata:
  $$\text{Schema: } (\text{JobId}, \text{JobNumber}, \text{DocumentType}, \text{PrinterName}, \text{Status}, \text{Quantity}, \text{TotalAmount}, \text{AmountPaid}, \text{PaymentMode}, \text{CreatedAt})$$
  No citizen names, photos, or identity numbers are retained in the database.
- **Memory Scrubbing Verification:** In-memory buffers allocated for decryption are cleared via `Array.Clear` immediately upon completion of PDF reader initialization.
