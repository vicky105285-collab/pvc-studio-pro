# Print Pipeline Architecture & Unified Layout Subsystem

**Date:** 2026-11-19  
**Branch:** `competitor-feature-parity`  
**Namespace:** `PVCStudioPro.Core.Services` & `PVCStudioPro.Core.Models`  

---

## 1. Unified Subsystem Architecture

To eliminate inconsistent coordinates between Document Desk, Card Studio, Crop Studio, Page Designer, and Print Preview, all document rendering and layout operations are unified under the `IPhysicalPageRenderService` and `PageLayoutModel`.

```
                    ┌───────────────────────────────┐
                    │      Universal File Ingest    │
                    │   (PDF / JPG / PNG / Office)  │
                    └──────────────┬────────────────┘
                                   │
                    ┌──────────────▼────────────────┐
                    │   Document Inspection & OCR   │
                    │   - UIDAI Decryption          │
                    │   - Multi-Factor Recognition  │
                    │   - Dual Card Extraction      │
                    └──────────────┬────────────────┘
                                   │
        ┌──────────────────────────┴──────────────────────────┐
        ▼                                                     ▼
┌──────────────────────────────┐              ┌──────────────────────────────┐
│  Crop Studio / Rectifier     │              │  Document Page Designer      │
│  - Perspective Homography    │              │  - Multi-Card Layout Canvas  │
│  - CR80 Aspect Lock          │              │  - Rulers, Guides, Align     │
│  - Color Adjustments         │              │  - Editable Project Format   │
└──────────────┬───────────────┘              └──────────────┬───────────────┘
               │                                             │
               └──────────────────────┬──────────────────────┘
                                      │
                       ┌──────────────▼──────────────┐
                       │      PageLayoutModel        │
                       │  Single Source of Truth     │
                       └──────────────┬──────────────┘
                                      │
               ┌──────────────────────┼──────────────────────┐
               ▼                      ▼                      ▼
     ┌──────────────────┐   ┌──────────────────┐   ┌──────────────────┐
     │ True Print       │   │ 600 DPI PDF/PNG  │   │ Windows Print    │
     │ Preview Raster   │   │ Export Document  │   │ Spooler (GDI/XPS)│
     └──────────────────┘   └──────────────────┘   └──────────────────┘
```

---

## 2. Shared Data Models

### `PageLayoutModel`
- `string Title`
- `double PageWidthMm`
- `double PageHeightMm`
- `double MarginLeftMm`, `MarginTopMm`, `MarginRightMm`, `MarginBottomMm`
- `double OffsetXMm`, `OffsetYMm`
- `int TargetDpi` (Default: 600)
- `bool MirrorMode`
- `List<PageLayoutItem> Items`

### `PageLayoutItem`
- `string Id`
- `LayoutItemKind Kind` (`FrontCard`, `BackCard`, `PassportPhoto`, `Image`, `Text`)
- `double XPositionMm`, `YPositionMm`
- `double WidthMm`, `HeightMm`
- `double RotationDegrees`
- `bool LockAspectRatio`
- `bool IsLocked`
- `int ZIndex`
- `SKBitmap? Bitmap`
- `string? TextContent`

---

## 3. Physical Page Renderer (`IPhysicalPageRenderService`)

Provides three core methods sharing exact identical drawing logic:
1. `SKBitmap RenderLayoutToBitmap(PageLayoutModel model, int dpi = 300)`
2. `byte[] ExportLayoutToPdf(PageLayoutModel model)`
3. `void RenderToPrintCanvas(PageLayoutModel model, SKCanvas canvas, int targetDpi)`
