# ADR-002: Selection of Google PDFium for Vector PDF Processing

**Status:** Accepted  
**Date:** 2026-11-18  
**Deciders:** Senior Windows Desktop Software Architect  

---

## Context
Commercial PVC printing requires rendering vector PDFs (e-Aadhaar, PAN, Voter ID, Ayushman Bharat) at high physical resolution (600 to 1200 DPI) to ensure micro-text, regional language fonts (Tamil, Hindi, Telugu), and dense QR codes print with razor-sharp clarity. Furthermore, the engine must handle 128-bit and 256-bit AES password-protected PDFs in memory without writing decrypted documents to disk.

We evaluated:
1. **Ghostscript:** GPL/AGPL licensed (commercial license is prohibitively expensive; GPL creates viral open-source obligations).
2. **iText 7/8:** AGPL licensed with strict commercial copyleft clauses.
3. **PDFsharp:** Open-source, but lacks robust password-handling for modern Indian government PDFs and lacks native high-performance rasterizers.
4. **Google PDFium (Chromium's PDF Engine):** Open-source under Apache 2.0 / BSD license.

## Decision
We select **Google PDFium** (integrated via `PDFiumSharp` and native 64-bit binaries).

## Rationale
1. **Permissive Commercial Licensing:** Apache 2.0 license permits commercial use, redistribution, and bundling without viral code disclosure requirements.
2. **Superior Rendering Performance:** PDFium is written in highly optimized C++ and powers Google Chrome. It renders complex vector layouts in tens of milliseconds.
3. **Arbitrary Target DPI:** PDFium allows rendering the exact bounding box at any arbitrary scale factor or DPI (e.g. 600 DPI = 23.62 pixels/mm), guaranteeing 100% scan rate for printed QR codes.
4. **In-Memory Security:** Decrypts password-protected PDFs directly from `byte[]` or `Stream` in RAM, ensuring compliance with DPDP Act 2023.

## Consequences
- Requires bundling 64-bit native `pdfium.dll` (~15 MB) with the application installer.
- Fast, reliable PDF processing immune to third-party cloud service outages.
