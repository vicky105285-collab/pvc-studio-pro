# ADR-003: Selection of OpenCVSharp4 and Local ONNX Runtime for Computer Vision & AI

**Status:** Accepted  
**Date:** 2026-11-18  
**Deciders:** Senior Windows Desktop Software Architect & Computer Vision Lead  

---

## Context
Citizens frequently bring smartphone photos or flatbed scans of physical documents (voter cards, ration cards, old laminated cards) rather than digital PDFs. Competitor software fails completely on these inputs.
Furthermore, the Passport Photo Studio requires background removal. Existing software uses primitive color-tolerance flood-filling that creates jagged, unprofessional edges with color halos around hair.

## Decision
We select:
1. **OpenCVSharp4:** For classical computer vision (contour extraction, quad boundary detection, perspective warping, deskewing, and signature binarization).
2. **Microsoft.ML.OnnxRuntime with Quantized Portrait Model:** For studio-grade, offline AI background removal.

## Rationale
1. **Automated Document Rectification (OpenCV):**
   - Using Canny edge detection and `Cv2.ApproxPolyDP`, the vision service identifies the 4 corners of a tilted or skewed card on a flatbed or photo background.
   - `Cv2.WarpPerspective` transforms the distorted quadrilateral into a standardized 85.60 × 53.98 mm rectangular card.
2. **Local AI Portrait Segmentation (ONNX):**
   - Cloud background removal APIs (e.g. remove.bg) charge ₹10 to ₹25 per image and violate data privacy by transmitting citizen portraits to third-party servers.
   - An optimized, quantized ONNX segmentation model (`birefnet` or `silueta`) executes 100% locally on modern AMD/Intel multi-core CPUs in ~1.5 seconds.
   - Achieves hair-level alpha matting, replacing backgrounds with solid white or blue with zero halos.

## Consequences
- Requires bundling native OpenCV and ONNX runtime DLLs and an ONNX model file (~35 MB).
- Elevates PVC Studio Pro from a basic cropping tool to a professional AI-powered desktop studio.
