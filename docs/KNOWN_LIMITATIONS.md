# Known Limitations & Engineering Boundaries — PVC Studio Pro

**Version:** 1.0.0-rc1  
**Date:** 2026-11-18  

This document details known operational boundaries, design trade-offs, and technical limitations in PVC Studio Pro.

---

## 1. Document Extraction & OCR Boundaries

1. **Scanned / Photographed Flat PDFs:**
   - Automatic template matching relies on embedded vector text extracted via PDFium (`Docnet.Core`).
   - If a customer brings a PDF that is purely a flat scanned raster image without an OCR text layer, automatic template identification will return `Unknown / Unsupported Document`.
   - **Operator Workaround:** Use the **Deskew & Scan** tab to photograph or crop the physical card directly using automatic 4-corner perspective rectification.

2. **UIDAI Format Variations:**
   - Government agencies periodically adjust the font size, margins, or orientation of downloaded e-Aadhaar PDFs.
   - While PVC Studio Pro accommodates standard 2023–2026 e-Aadhaar layouts, any non-standard regional portal variation may trigger the low-confidence warning: `Requires Operator Confirmation`.
   - Operators can manually verify crop bounds in the interactive preview before committing ink to PVC.

---

## 2. Photo Studio & Computer Vision Boundaries

1. **CPU GrabCut Performance on Large Images:**
   - Background segmentation utilizes OpenCV's `Cv2.GrabCut` algorithm executed entirely on the local AMD CPU without cloud dependence.
   - For standard portrait photos ($1000 \times 1000$ to $2000 \times 2000$ px), processing completes in $300\text{–}600\text{ ms}$.
   - For oversized camera raw inputs ($6000 \times 4000$ px), processing may take $2.5\text{–}4.0\text{ seconds}$. Inputs are automatically downsampled to $1200\text{ px}$ max dimension prior to segmentation for optimal operator responsiveness.

2. **Low-Contrast Foreground/Background:**
   - GrabCut relies on color Gaussian Mixture Models (GMM) to separate foreground and background.
   - If a customer wearing a pure white shirt is photographed against a pure white wall under harsh flash, boundary ambiguity may occur around shoulder edges.
   - **Best Practice for e-Sevai Operators:** Use a solid blue, green, or textured grey backdrop behind the subject for instant, flawless matting.

---

## 3. Printer Hardware & Mechanical Tolerances

1. **Inkjet Roller Feed Variance:**
   - Desktop inkjet card trays are pushed into the printer via mechanical feed rollers. Rollers inherently have mechanical tolerances of $\pm 0.3\text{–}0.5\text{ mm}$ depending on roller wear, dust, and tray cleanliness.
   - To prevent white slivers on edges, card templates include a standard $1.0\text{ mm}$ full-bleed margin beyond the nominal CR80 die-cut edge ($85.60 \times 53.98\text{ mm}$).

2. **Printer Queue Polling:**
   - Windows Print Spooler (`winspool.drv`) reports asynchronous printer status. While spool errors and paper/tray out errors are trapped, Windows does not report real-time ink droplet deposition. A job reported as "Sent to Spooler" is physically in the printer's hardware buffer.

---

## 4. Privacy & Volatile Memory Boundaries

1. **Garbage Collection & Managed Buffers:**
   - Best-effort zeroization is implemented in `SecurityService.SecureWipeArray` and `SecureWipeMemory`.
   - However, in .NET managed execution, objects copied before zeroization by the runtime GC during heap compaction cannot be bound by user-mode applications.
   - To minimize exposure, PDF byte buffers are pinned during critical extraction and zeroized immediately upon completion.
   - Zero customer identity data (names, Aadhaar numbers, photos, biometrics) is persisted to disk, the SQLite database, or application logs.

2. **Single-Seat SQLite Concurrency:**
   - The job ledger uses SQLite with Write-Ahead Logging (WAL) and semaphore locks. It is designed for single-PC e-Sevai kiosk operation. Concurrent write tests verify zero database locks up to 10 concurrent threads, but it is not intended for network-shared multi-workstation database access over SMB shares.
