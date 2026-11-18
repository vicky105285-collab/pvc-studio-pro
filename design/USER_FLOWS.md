# User Flows & Operator Interaction Paths — PVC Studio Pro

**Target Audience:** Commercial e-Sevai Maiyam & Cyber Café Operators  
**Primary Goal:** Complete common customer print jobs in under 20 seconds with zero errors  
**Date:** 2026-11-18  

---

## 1. High-Velocity Flow: e-Aadhaar PDF to Dual-Card Tray Print

```mermaid
flowchart TD
    Start(["Citizen Hands Over e-Aadhaar PDF / WhatsApp"]) --> Drop["Drag & Drop PDF into PVC Studio Pro (or Ctrl+O)"]
    Drop --> Detect{"Encrypted PDF?"}
    Detect -- Yes --> Pwd["Password Prompt appears with auto-hint (e.g. VIGN1990)"]
    Pwd --> Decrypt["Instant In-Memory Decryption (PDFium <15ms)"]
    Detect -- No --> Decrypt
    Decrypt --> AutoCrop["Template Engine extracts Front & Back at 600 DPI"]
    AutoCrop --> Preview["Preview Canvas displays Front & Back with CR80 rounded corners"]
    Preview --> MaskCheck{"Customer requests Masked Aadhaar?"}
    MaskCheck -- Yes --> Mask["1-Click Mask Toggle (XXXX XXXX 1234 applied)"]
    MaskCheck -- No --> CheckTray
    Mask --> CheckTray
    CheckTray["Driver Pre-Flight: Validates 'CD/DVD Tray' matches Epson L805"]
    CheckTray --> PrintBtn["Click 'PRINT NOW' (or Ctrl+P)"]
    PrintBtn --> Spool["Spooler job dispatched directly to tray"]
    Spool --> AutoShred["Zero-PII memory buffer shredded via SecureZeroMemory"]
    Spool --> LogLedger["Record transaction: e-Aadhaar PVC, ₹70 Cash committed to SQLite"]
    LogLedger --> Done(["Citizen Receives Finished Card & Optional Receipt"])
```

**Target Execution Time:** **12 to 18 seconds** total operator interaction.

---

## 2. High-Velocity Flow: Smartphone Camera Photo of Physical Card

```mermaid
flowchart TD
    Start(["Citizen brings smartphone photo or photocopy of old card"]) --> Load["Import photo into Computer Vision Deskewer"]
    Load --> AutoContour["OpenCV detects 4 corners of tilted card (<32ms)"]
    AutoContour --> Deskew["4-Point Perspective Warp aligns to 85.60 x 53.98 mm"]
    Deskew --> UserVerify{"Operator confirms alignment on preview canvas"}
    UserVerify -- Minor drift --> DragHandles["Operator adjusts interactive corner handles on screen"]
    UserVerify -- Perfect --> SelectTray["Select target printer & tray"]
    DragHandles --> SelectTray
    SelectTray --> Print["Print to physical PVC card"]
```

---

## 3. High-Velocity Flow: Passport Photo Studio with Local AI Matting

```mermaid
flowchart TD
    Start(["Customer requires 8 Passport Photos on 4x6 sheet"]) --> Load["Import photo (camera snapshot or WhatsApp)"]
    Load --> Frame["Crop box locks to 35x45 mm (Indian Passport 70-80% face)"]
    Frame --> RemoveBG["Click 'AI White Background' (ONNX local CPU matting ~1.5s)"]
    RemoveBG --> TouchUp{"Hair/collar check"}
    TouchUp -- Minor halo --> Brush["Quick manual eraser/brush touch-up"]
    TouchUp -- Clean --> SelectSheet["Select '4x6 Sheet (8 Photos)' with 0.5pt cut-lines"]
    Brush --> SelectSheet
    SelectSheet --> PrintSheet["Print on Epson Photo Paper / Rear Feeder"]
    PrintSheet --> Done(["Customer receives photo sheet ready for scissor cut"])
```
