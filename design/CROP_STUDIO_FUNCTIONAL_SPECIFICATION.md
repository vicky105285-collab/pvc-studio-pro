# CROP STUDIO — COMPLETE FUNCTIONAL & ARCHITECTURAL SPECIFICATION

**Component:** `PVCStudioPro.App.ViewModels.CropStudioViewModel` & `PVCStudioPro.App.Views.CropStudioView`  
**Target:** Branch `competitor-feature-parity`  
**Author:** Senior Windows Desktop Software Architect & Image-Processing Specialist  

---

## 1. Architectural Overview & Viewport Engine

The primary root cause of the previous Crop Studio defect was the lack of a proper 2D viewport transform pipeline. High-resolution scanned documents (e.g. $2480 \times 3508$ pixels at 300 DPI or $4960 \times 7016$ pixels at 600 DPI) were placed directly in a `ScrollViewer` without transform constraints, causing immediate 100% pixel magnification.

The new Crop Studio architecture implements a dedicated **2-Layer Viewport & Interaction Pipeline**:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        VIEWPORT CONTAINER                              │
│                                                                        │
│   ┌────────────────────────────────────────────────────────────────┐   │
│   │                    TRANSFORMED LAYER                           │   │
│   │      MatrixTransform: [Scale(ZoomFactor), Translate(PanX, PanY)]│   │
│   │                                                                │   │
│   │   ┌───────────────────────────┐  ┌──────────────────────────┐  │   │
│   │   │     Source Image Layer    │  │   Interactive Adorner    │  │   │
│   │   │ (Underlying Image Pixels) │  │  - 8 Draggable Handles   │  │   │
│   │   │                           │  │  - Dimmed Outside Mask   │  │   │
│   │   │                           │  │  - Rule-of-Thirds Grid   │  │   │
│   │   └───────────────────────────┘  └──────────────────────────┘  │   │
│   └────────────────────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Mathematical Coordinate Transformations

Let $(X_{img}, Y_{img})$ be a coordinate in native image pixel space ($0 \le X_{img} < W_{orig}$, $0 \le Y_{img} < H_{orig}$).  
Let $(X_{screen}, Y_{screen})$ be the coordinate on the WPF screen element.  
Let $S = \text{ZoomFactor}$ and $(T_x, T_y) = (\text{PanOffsetX}, \text{PanOffsetY})$.

### 2.1 Forward Mapping (Image $\to$ Screen):
$$X_{screen} = (X_{img} \cdot S) + T_x$$
$$Y_{screen} = (Y_{img} \cdot S) + T_y$$

### 2.2 Inverse Mapping (Screen $\to$ Image):
$$X_{img} = \frac{X_{screen} - T_x}{S}$$
$$Y_{img} = \frac{Y_{screen} - T_y}{S}$$

### 2.3 Mouse-Wheel Zoom Centered at Cursor:
When the user rotates the mouse wheel at screen position $(C_x, C_y)$ with scale multiplier $k$:
1. Determine the image point under the cursor:
   $$P_x = \frac{C_x - T_x}{S_{old}}, \quad P_y = \frac{C_y - T_y}{S_{old}}$$
2. Compute new scale:
   $$S_{new} = \text{Clamp}(S_{old} \cdot k, 0.10, 5.0)$$
3. Adjust translation so $(P_x, P_y)$ remains fixed under $(C_x, C_y)$:
   $$T_{x, new} = C_x - (P_x \cdot S_{new})$$
   $$T_{y, new} = C_y - (P_y \cdot S_{new})$$

### 2.4 Initial "Fit to Window" Calculation:
When any image of dimension $(W, H)$ is loaded into viewport $(V_w, V_h)$:
$$S_{initial} = \min\left(\frac{V_w - 40}{W}, \frac{V_h - 40}{H}\right)$$
$$T_{x, initial} = \frac{V_w - (W \cdot S_{initial})}{2}$$
$$T_{y, initial} = \frac{V_h - (H \cdot S_{initial})}{2}$$
This guarantees that **every image initially opens completely visible, centered, and never excessively magnified**.

---

## 3. Interactive Crop Adorner & Handle System

The crop selection supports 8 interactive handles:
- **Corner Handles:** `TopLeft`, `TopRight`, `BottomRight`, `BottomLeft` (adjusts width and height simultaneously while respecting aspect ratio lock if active).
- **Edge Handles:** `Top`, `Bottom`, `Left`, `Right` (adjusts one dimension).
- **Body Area:** Dragging inside the crop box moves the entire selection without altering width or height.
- **Outside Dimming:** The unselected region of the image is masked with a semi-transparent black overlay (`#80000000`).
- **Aspect Ratio Lock Presets:**
  - **CR80 Standard:** Ratio $= 85.60 / 53.98 \approx 1.5857$
  - **Passport Photo:** Ratio $= 35.0 / 45.0 \approx 0.7778$
  - **Stamp Photo:** Ratio $= 25.0 / 30.0 \approx 0.8333$
  - **A4 Document:** Ratio $= 210.0 / 297.0 \approx 0.7071$
  - **Freeform:** Unconstrained ratio.

---

## 4. Perspective Quadrilateral Warp Mode

When an operator imports an angled photo of a physical card taken with a smartphone camera:
1. Operator toggles **"Perspective Mode"**.
2. 4 draggable corner handles appear on the four physical corners of the card.
3. OpenCvSharp's `Cv2.GetPerspectiveTransform` and `Cv2.WarpPerspective` compute the homography matrix:
   $$\begin{bmatrix} x' \\ y' \\ 1 \end{bmatrix} \sim \mathbf{H} \begin{bmatrix} x \\ y \\ 1 \end{bmatrix}$$
   mapping the quadrilateral to a crisp, straightened $2022 \times 1275\text{ px}$ CR80 card at 600 DPI.

---

## 5. Non-Destructive Image Enhancement Pipeline

Before outputting to PVC printing, faded photos or dark government scans can be adjusted:
- **Brightness:** $[-100, +100]$ (default 0)
- **Contrast:** $[0.5, 2.0]$ (default 1.0)
- **Saturation:** $[0.0, 2.0]$ (default 1.0, 0 = pure grayscale)
- **Sharpness:** $[0, 5]$ (Laplacian / unsharp masking)
- **Auto-Enhance:** Automatic histogram equalization for balanced tone and contrast.
- **Undo / Redo / Reset:** Instant reversion to original state.

---

## 6. Output & Routing

The cropped and adjusted result can be dispatched via 1 click:
- **"Set as Front Card Face"** $\to$ Transferred to Document Desk / Card Studio Front Slot.
- **"Set as Back Card Face"** $\to$ Transferred to Document Desk / Card Studio Back Slot.
- **"Send to Photo Studio"** $\to$ Transferred to Passport Photo Maker grid.
- **"Save Image as File"** $\to$ Exports 600 DPI PNG or JPEG.
