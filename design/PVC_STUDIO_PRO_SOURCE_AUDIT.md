# PVC STUDIO PRO — DEEP SOURCE CODE & ARCHITECTURE AUDIT

**Branch:** `competitor-feature-parity`  
**Date:** 2026-11-19  
**Auditor:** Senior Windows Desktop Software Architect  

---

## 1. Audit Findings Summary

| Finding ID | Component / File | Severity | Issue / Root Cause | Observable Impact | Recommended Correction |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **AUD-01** | `CropStudioView.xaml` | **CRITICAL** | Raw pixel display inside unbound `ScrollViewer`. Lack of Matrix/ScaleTransform viewport architecture. | Image opens massively magnified. Operator cannot see or manipulate the full image. | Implement dedicated `ZoomableCanvas` / `MatrixTransform` with initial "Fit to Window", mouse-wheel zoom centred at cursor, and Spacebar/drag pan. |
| **AUD-02** | `CropStudioView.xaml` & `CropStudioViewModel.cs` | **CRITICAL** | Crop box represented only as static text readouts. No on-canvas interactive drag handles. | Operator cannot interactively position, drag, or resize the crop rectangle on the image. | Implement interactive `CropOverlayCanvas` with 8 resize handles (4 corners + 4 edges) and 4-point quadrilateral handles with real-time coordinate mapping. |
| **AUD-03** | `CardDetectionService.cs` & `TemplateService.cs` | **CRITICAL** | Calibrated Aadhaar Y offset was hardcoded to $208.0\text{ mm}$ instead of verified $219.7\text{ mm}$ ($2595\text{ px}$ at 300 DPI). | Aadhaar card extracted in Document Desk has incorrect vertical alignment (clips bottom or top). | Update calibration to match verified `Real.db` benchmarks: Front $(17.8, 219.7, 88.5, 56.3\text{ mm})$, Back $(110.1, 219.7, 88.5, 56.3\text{ mm})$ with edge snap. |
| **AUD-04** | `ThemeTokens.xaml`, `LightTheme.xaml`, `DarkTheme.xaml`, and Views | **CRITICAL** | Hardcoded hex colors (`#FEF3C7`, `#B45309`, `#1E232A`, `#F0FDF4`, `#166534`, `#374151`) mixed directly in XAML user controls. | Dark mode elements appear inside Light mode screens; invisible buttons, poor text contrast, unreadable badges in Light theme. | Unify under a strict semantic design token system (`SemanticBrush` tokens). Eliminate local hardcoded hex overrides. |
| **AUD-05** | `CropStudioViewModel.cs` & `ManualCropService.cs` | **HIGH** | Missing professional image adjustments (Brightness, Contrast, Saturation, Sharpness). | Faded citizen photos from older Aadhaar letters cannot be tuned before printing on PVC. | Implement a non-destructive image adjustment pipeline (ColorMatrix + SkiaSharp convolution) with live preview and reset. |
| **AUD-06** | `PrintPreviewService.cs` | **HIGH** | Tray slot positions used approximate coordinates rather than factory `EPSONTrayConfig` specs. | Virtual tray preview did not align perfectly with physical Epson tray slot cutouts. | Calibrate Epson tray slots to exact verified coordinates: Slot 1 $(120, 29, 339, 213)$, Slot 2 $(120, 263, 339, 213)$. |
| **AUD-07** | `DocumentDeskViewModel.cs` | **MEDIUM** | Unknown or non-standard documents previously lacked a 1-click fallback to Crop Studio with pre-loaded aspect ratio. | Scanned certificates and WhatsApp card photos caused confusion when automatic detection returned `GenericId`. | Automatically offer seamless 1-click transition to Crop Studio with CR80 aspect lock pre-selected. |

---

## 2. Detailed Technical Root Causes & Correction Architecture

### AUD-01 & AUD-02: Crop Studio Viewport & Interactive Canvas Rebuild
**Problem:** In `CropStudioView.xaml`:
```xml
<ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Auto">
    <Image Source="{Binding SourceImage}" Stretch="Uniform" Margin="8"/>
</ScrollViewer>
```
When a high-resolution $2480 \times 3508$ A4 document or a 12 MP phone camera image is loaded, WPF's layout calculates the natural unconstrained size inside the scrollviewer. The user sees a tiny fraction of the top-left corner at 100% DPI scaling with massive scrollbars. The operator cannot zoom out to see the entire card, and there are no draggable handles on the image canvas.

**Solution Architecture:**
1. **Viewport Coordinate Transformer:**
   A dedicated interactive Viewport Container that maintains:
   - `ZoomFactor` (continuous scale from 10% to 500%, with initial `FitToViewport`)
   - `PanOffsetX`, `PanOffsetY` (mouse-drag translation)
   - Matrix transformation mapping Screen Coordinates $\leftrightarrow$ Image Pixel Coordinates.
2. **Interactive 8-Handle Crop Adorner:**
   An interactive canvas overlay rendered on top of the image:
   - 4 Corner Handles (Top-Left, Top-Right, Bottom-Right, Bottom-Left)
   - 4 Edge Handles (Top, Bottom, Left, Right)
   - Center Drag Area (drags the entire crop box)
   - Semi-transparent darkened mask outside the crop box
   - Rule-of-thirds grid lines
   - Live numeric readouts ($X, Y, W, H$ in pixels and millimeters)
   - Aspect ratio constraint enforcement (CR80 $1.5857$, Passport $0.7778$, Stamp $0.8333$, or Freeform).
3. **Perspective Quad Rectification Mode:**
   Toggleable 4-point quad mode with magnified corner loupes for rectifying camera photos taken at an angle.

---

### AUD-03: Aadhaar Alignment Calibration
**Problem:** Previous hardcoded coordinates in `CardDetectionService.cs` placed the front card at $Y = 208\text{ to }210\text{ mm}$.
Comparing against the verified benchmark from `Real.db` (`AADHAAR`):
$$\text{At } 300\text{ DPI: } X = 210, Y = 2595, W = 1045, H = 665$$
$$\text{In Millimeters: } X = 17.78\text{ mm}, Y = 219.71\text{ mm}, W = 88.48\text{ mm}, H = 56.30\text{ mm}$$
The previous vertical offset of $208.0\text{ mm}$ was $11.71\text{ mm}$ (138 pixels!) too high. This caused the crop box to capture the cutting dashed line and clip the bottom Aadhaar number!

**Solution:**
Update `CardDetectionService` with the exact verified parameters:
- `Front`: $X = 17.78\text{ mm}$, $Y = 219.71\text{ mm}$, $W = 88.48\text{ mm}$, $H = 56.30\text{ mm}$
- `Back`: $X = 110.06\text{ mm}$, $Y = 219.71\text{ mm}$, $W = 88.48\text{ mm}$, $H = 56.30\text{ mm}$
- Automatic horizontal and vertical boundary snapping: searches $\pm 25\text{ pixels}$ for the solid or dashed card outline to achieve sub-millimeter precision.

---

### AUD-04: Theme Token Harmonization
**Problem:** In `DocumentDeskView.xaml` and `PrintPreviewApprovalView.xaml`, styles such as:
```xml
Background="#FEF3C7" BorderBrush="#F59E0B"
Foreground="#92400E"
Background="#1E232A"
```
were written as hardcoded hex literals. In Light theme, `#1E232A` renders as a dark slab with dark text, while `#FEF3C7` clashes with Windows 11 Fluent surfaces.

**Solution:**
Replace all static hex colors with semantic DynamicResource tokens:
- `NeutralSurfaceBrush`
- `CardSurfaceBrush`
- `ElevatedSurfaceBrush`
- `TextPrimaryBrush`
- `TextSecondaryBrush`
- `TextTertiaryBrush`
- `PrimaryAccentBrush`
- `PrimaryAccentHoverBrush`
- `PrimaryAccentPressedBrush`
- `AccentTextBrush`
- `NeutralBorderBrush`
- `DividerBrush`
- `StatusSuccessBgBrush`, `StatusSuccessFgBrush`
- `StatusWarningBgBrush`, `StatusWarningFgBrush`
- `StatusErrorBgBrush`, `StatusErrorFgBrush`
- `StatusInfoBgBrush`, `StatusInfoFgBrush`
- `PreviewCanvasBgBrush`
