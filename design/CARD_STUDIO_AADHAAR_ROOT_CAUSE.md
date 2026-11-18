# Card Studio Aadhaar Import Failure — Root Cause Investigation & Resolution

**Date:** 2026-11-19  
**Branch:** `competitor-feature-parity`  
**Component:** `PVCStudioPro.App.ViewModels.CardPrintViewModel` vs `PVCStudioPro.App.ViewModels.DocumentDeskViewModel`  
**Status:** **INVESTIGATED & ROOT CAUSE CONFIRMED**

---

## 1. Discrepancy & Symptom Analysis

### User Symptom
When an official UIDAI e-Aadhaar PDF is imported into **Document Desk**, the document successfully decrypts, renders at 300/600 DPI, detects Aadhaar features, and extracts the card faces.  
However, when the exact same e-Aadhaar PDF is loaded into **Card Studio** (`CardPrintViewModel.cs`), the file is immediately rejected with:  
`DetectedTemplateName = "UNSUPPORTED / UNKNOWN DOCUMENT"`  
`StatusMessage = "Document rejected: Document keywords did not match any supported Indian Government identity template."`

---

## 2. Source-Level Code Audit & Root Causes

### Root Cause 1: Two Incompatible Document Ingestion Pipelines
`DocumentDeskViewModel` and `CardPrintViewModel` were maintained as two disjoint, duplicated pipelines:
- `DocumentDeskViewModel` utilizes `IDocumentImportService`, `IPdfService.TryUnlockPasswordAsync`, `IDocumentTypeDetectionService`, and `ICardDetectionService`.
- `CardPrintViewModel` bypassed `IDocumentTypeDetectionService` and `ICardDetectionService` entirely, directly calling `_templateService.MatchTemplate(docInfo.ExtractedTextSample, ...)`.

### Root Cause 2: Brittle Dependency on Raw PDFium Text Extraction
In `CardPrintViewModel.LoadDocumentFileAsync`:
```csharp
var docInfo = await _pdfService.InspectDocumentAsync(stream, password);
var matchResult = _templateService.MatchTemplate(docInfo.ExtractedTextSample, docInfo.PageWidthMm, docInfo.PageHeightMm);
if (!matchResult.IsSuccess || matchResult.Template == null)
{
    DetectedTemplateName = "UNSUPPORTED / UNKNOWN DOCUMENT";
    StatusMessage = $"Document rejected: {matchResult.MatchReason}";
    return; // <-- Immediate silent rejection!
}
```
**Why this fails on official UIDAI e-Aadhaar:**
1. Many UIDAI e-Aadhaar PDFs (especially 2023–2024 versions and Masked e-Aadhaars) embed Hindi and Tamil fonts with Identity-H CMaps or vectorized glyph outlines where raw PDFium `FPDFText_GetText` returns empty strings or unmapped character codes (`???`).
2. Scanned photo Aadhaars (WhatsApp images, JPEG/PNG scans) contain zero extractable text streams.
3. Because `CardPrintViewModel` relied solely on `docInfo.ExtractedTextSample`, any failure in string matching caused an immediate fatal rejection, completely ignoring that the document is a standard A4 page with Aadhaar layout.

### Root Cause 3: Premature `InspectDocumentAsync` Before Password Negotiation
In `CardPrintViewModel`, `_pdfService.InspectDocumentAsync(stream, password)` was called before `TryUnlockPasswordAsync`. If the user provided an incorrect password, `InspectDocumentAsync` could not distinguish between a bad password and an unsupported format.

### Root Cause 4: Missing File Formats in Card Studio
`CardPrintViewModel.OpenDocumentAsync` had a file dialog filter restricted to:
`Filter = "Indian Gov ID Documents (*.pdf)|*.pdf|All Files (*.*)|*.*"`  
It lacked support for direct raster imports (`.jpg`, `.jpeg`, `.png`, `.bmp`, `.tiff`, `.webp`).

---

## 3. Engineering Resolution Architecture

1. **Shared Document Engine:** Unify `CardPrintViewModel` with the universal document engine (`IDocumentImportService`, `IPdfService`, `IDocumentTypeDetectionService`, `ICardDetectionService`, and `IManualCropService`).
2. **Multi-Factor Recognition with Visual Fallback:**
   - If keyword text extraction matches Aadhaar, confirm Aadhaar template.
   - If text extraction is empty or non-Latin glyphs, inspect page aspect ratio ($210 \times 297\text{ mm}$ A4) and use calibrated bottom dual-card cut-guide coordinates ($Y=219.71\text{ mm}$, $W=88.48\text{ mm}$, $H=56.30\text{ mm}$).
   - Never mark an A4 document "UNSUPPORTED" without offering automatic visual extraction or a one-click transition to Crop Studio.
3. **Password Negotiation Protocol:**
   - Clearly distinguish between:
     - `Unencrypted`
     - `PasswordRequired` (Show 8-character password prompt)
     - `InvalidPassword` (Show inline error: "Incorrect password. Standard UIDAI format: First 4 letters of name in CAPITAL + Year of Birth, e.g. SURE1990")
     - `Corrupted`
4. **Interactive Crop Studio Integration:**
   - Provide an "Edit / Manual Crop" button in Card Studio that opens the decrypted high-res bitmap in Crop Studio with CR80 aspect ratio lock, allowing the operator to adjust or reassign front/back faces seamlessly.
