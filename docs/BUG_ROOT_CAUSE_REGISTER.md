# PVC Studio Pro — Bug Root Cause Register
**Forensic Engineering Analysis of Primary Functional & Visual Defects**

- **Date:** 2026-11-19
- **Auditor:** Senior Windows Desktop Systems & Print Engine Architect
- **Target Repository:** `PVCStudioPro`

---

## 1. Defect Category B: Aadhaar Front/Back Extraction Failures

### Symptom 1: Incorrect Back-Side Detection & Inaccurate Bounding Coordinates
- **Observed Behavior:**
  When loading official UIDAI e-Aadhaar PDFs, the front side is often cropped with text truncated, and the back side address/QR code is offset or partially missing.
- **Root Cause Analysis in Source Code:**
  In `src/PVCStudioPro.Core/Services/Implementations/CardDetectionService.cs` (lines 28–32):
  ```csharp
  // Assume standard A4 page aspect ratio to deduce DPI
  // A4 height is 297 mm
  double deducedDpi = (pageBitmap.Height * MmPerInch) / 297.0;
  if (deducedDpi < 72 || deducedDpi > 1200)
  {
      deducedDpi = 300.0;
  }
  ```
  1. **Blind Height Assumption:** The engine calculates DPI by assuming `pageBitmap.Height` corresponds to exactly $297\text{ mm}$ (standard A4 height). When a citizen downloads an e-Aadhaar PDF from the UIDAI portal, the PDF page geometry in points is $595.276 \times 841.890\text{ pt}$ ($210 \times 297\text{ mm}$). However, when rendered via PDFium, depending on PDF render settings, print scaling, or if the PDF was saved with non-standard crop-boxes, the height may vary. If an image scan of the card area is imported (e.g., $1500\text{ px}$ high), `deducedDpi` is calculated as $(1500 \times 25.4) / 297 = 128.28\text{ DPI}$.
  2. **Coordinate Drift:** At $128.28\text{ DPI}$, `fy` is calculated as $(219.71 \times 128.28) / 25.4 = 1109\text{ px}$. The hardcoded coordinate slice looks at $Y=1109\text{ px}$ instead of the bottom of the card, slicing empty white space.
  3. **Zero Computer Vision Contour Detection:** Lines 195–228 do not execute OpenCV edge detection or find contours. They perform a raw, blind array slice:
     ```csharp
     var frontRect = new SKRectI(fx, fy, fx + cw, fy + ch);
     pageBitmap.ExtractSubset(frontBitmap, frontRect);
     ```
     If the citizen's document has a $5\text{ mm}$ margin shift from the printer driver, the hardcoded slice cuts directly through citizen names and photos.

### Symptom 2: Aspect-Ratio Distortion & Destructive Normalization
- **Observed Behavior:**
  Text and portrait photos on extracted cards appear horizontally stretched or vertically squished.
- **Root Cause Analysis in Source Code:**
  In `CardDetectionService.cs` (lines 34–37 and 230–252):
  ```csharp
  double frontWMm = 88.48;
  double frontHMm = 56.30;
  // ...
  int targetW = 2022; // 85.60 mm @ 600 DPI
  int targetH = 1275; // 53.98 mm @ 600 DPI
  canvas.DrawBitmap(frontBitmap, new SKRect(0, 0, targetW, targetH), ...);
  ```
  1. The crop rectangle cuts an area of $88.48 \times 56.30\text{ mm}$ (Aspect Ratio: $\mathbf{1.5715}$).
  2. The target CR80 card dimension is $85.60 \times 53.98\text{ mm}$ (Aspect Ratio: $\mathbf{1.5858}$).
  3. The resampler stretches the source rectangle non-uniformly into the destination rectangle without letterboxing or uniform aspect scaling, introducing distortion into barcodes, photos, and micro-text.

### Symptom 3: Layout Variance Across UIDAI Generations
- **Observed Behavior:**
  Older Aadhaar PDFs (pre-2018), Baal Aadhaar (Child), mAadhaar, and Masked Aadhaar fail detection.
- **Root Cause:**
  - Modern e-Aadhaar places the card at the bottom ($Y \approx 219.7\text{ mm}$).
  - Baal Aadhaar (Child Aadhaar) places the card at $Y = 204.05\text{ mm}$ (higher by $15.6\text{ mm}$).
  - mAadhaar places the card at $Y = 163.83\text{ mm}$ (higher by $55.8\text{ mm}$).
  - Masked Aadhaar includes a top security banner that displaces vertical positioning.
  Because `CardDetectionService` only applies one set of coordinates for `DocumentKind.Aadhaar`, all non-modern formats fail.

---

## 2. Defect Category C: Passport Photo Processing Failures

### Symptom 1: Unwanted Wavy / Scalloped Pattern Along Bottom Edge
- **Observed Behavior:**
  When generating a passport photo with background removal, the bottom edge of the subject's shirt/chest exhibits an irregular, undulating wavy pattern.
- **Root Cause Analysis in Source Code:**
  In `src/PVCStudioPro.Core/Services/Implementations/VisionService.cs` (lines 92–115):
  ```csharp
  // 2. Define foreground ROI (bounding subject, leaving 5% outer margin as definite background)
  int marginX = Math.Max(2, (int)(bgr.Width * 0.05));
  int marginY = Math.Max(2, (int)(bgr.Height * 0.05));
  var rect = new Rect(marginX, marginY, bgr.Width - (2 * marginX), bgr.Height - (2 * marginY));

  // 3. Initialize GrabCut masks and models
  using var mask = new Mat(bgr.Size(), MatType.CV_8UC1, new Scalar((int)GrabCutClasses.BGD));
  // ...
  Cv2.GrabCut(bgr, mask, rect, bgdModel, fgdModel, iterations, GrabCutModes.InitWithRect);
  ```
  1. **The 5% Bottom Margin Trap:** The code defines the foreground ROI by subtracting $5\%$ from all four sides. In a portrait photo, **the subject's chest and shoulders extend all the way to the bottom edge of the frame**.
  2. By excluding the bottom $5\%$ of the image from the bounding rect, GrabCut marks the bottom $5\%$ as **Definite Background (`BGD = 0`)**.
  3. During graph-cut energy minimization, the algorithm attempts to separate the foreground clothing from the artificially forced background boundary at the bottom, creating an irregular, jagged cutline across the chest.
  4. At lines 114–115:
     ```csharp
     Cv2.GaussianBlur(fgMask, featheredMask, new Size(3, 3), 0.5);
     ```
     The Gaussian blur softens this jagged cut into a **distinct, unsightly wavy border** along the bottom of the passport photograph.
  5. **Correct Physical Fix:** The bottom margin must be zero (`rect.Height = bgr.Height - marginY`), and the bottom edge pixels where the subject exits the frame must be initialized as `PR_FGD` (Probable Foreground), never `BGD`.

### Symptom 2: Inability to Reposition or Re-Scale the Face Within Frame
- **Observed Behavior:**
  The operator cannot move or resize the face to align with the ICAO 9303 biometric guidelines (crown limit, eye level, chin base).
- **Root Cause:**
  In `PhotoStudioViewModel.cs`, `SinglePhotoPreview` is rendered statically via `Stretch="Uniform"`. There are no interactive pan/zoom adorner controls or manual crop boundaries. The user is locked to an automated center-fit calculation in `VisionService.cs` (lines 163–168).

### Symptom 3: Complete Print Spooler Disconnect
- **Observed Behavior:**
  Clicking "PRINT PHOTO SHEET" displays a success banner, but no print job appears in the Windows queue.
- **Root Cause:**
  `PhotoStudioViewModel.PrintPhotoSheetAsync` (lines 250–284) instantiates a database record and updates `StatusMessage`. **It never calls `_printService.SpoolPageAsync` or any printing API.**

---

## 3. Defect Category D: Physical Print Layout & Geometry Discrepancies

### Symptom: Discrepancy Between On-Screen Preview, PDF Export, and Hardware Spooling
- **Root Cause Analysis:**
  There are three separate rendering engines computing card positions:
  1. `ProductionWorkspaceViewModel`: Computes placement using an internal switch statement and SkiaSharp.
  2. `PrintPreviewApprovalViewModel`: Computes placement via `IPrintPreviewService.GenerateVirtualTrayPreviewAsync`.
  3. `PhotoStudioViewModel`: Computes placement via `VisionService.ComposePassportPhotoSheet` with hardcoded cell gaps.
  Because these three engines do not share a single source of truth for physical margins, a layout verified in one screen does not match output in another.

---

## 4. Defect Category F: Ledger Rigidity & Zero Test Isolation

### Symptom: Inability to Correct Financial Records & Live Production Contamination
- **Root Cause Analysis:**
  In `src/PVCStudioPro.Core/Services/Implementations/JobLedgerService.cs`:
  1. Table `print_jobs` has no fields for `is_void`, `cancellation_reason`, `discount_amount`, `balance_due`, or `original_job_id`.
  2. The service contains no SQL `UPDATE` statements for jobs.
  3. The connection string is hardcoded to `pvc_studio.db` across all application executions. There is no concept of a separate test database or in-memory test provider.
