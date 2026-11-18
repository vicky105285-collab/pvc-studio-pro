# Screen Specifications & Control Anatomy — PVC Studio Pro

**Target Application:** Windows 11 Desktop (WPF + WPF UI)  
**Layout Model:** Left Sidebar Navigation (260px) + Main Interactive Stage + Right Quick Actions  
**Date:** 2026-11-18  

---

## 1. Primary Navigation Structure

The application features a modern Windows 11 `NavigationView` sidebar:
1. **Dashboard** (அறிக்கைப் பலகை) — Instant service shortcuts, daily card counter, revenue summary.
2. **Card Printing** (அட்டை அச்சிடுதல்) — Core document cropping and printing workspace.
3. **Photo Studio** (புகைப்பட அரங்கு) — Passport photo creation, AI background removal, photo sheets.
4. **Card Designer** (அட்டை வடிவமைப்பு) — Custom CR80 blank card designer with barcodes and QR codes.
5. **Batch Processing** (மொத்த அச்சிடுதல்) — Excel/CSV import for school and corporate cards.
6. **WhatsApp Watcher** (வாட்ஸ்அப் பதிவிறக்கம்) — Monitored downloads folder for 1-click customer photo import.
7. **Business Ledger** (கணக்குப் பதிவேடு) — Daily cash transactions, job history, thermal receipts.
8. **Printer Setup & Calibration** (அச்சுப்பொறி அமைவு) — Dynamic discovery, tray profiles, millimeter calibration.
9. **Settings & Language** (அமைப்புகள்) — Theme toggle (Dark/Light), language toggle (English/Tamil), retention rules.

---

## 2. Screen 1: Card Printing Studio (The Core Workspace)

```
+--------------------------------------------------------------------------------------------------+
| PVC Studio Pro - [Card Printing Studio]                                  [Tamil / English] [_][O][X] |
+--------------------------------------------------------------------------------------------------+
| [=] Menu    |  [ Open Document (Ctrl+O) ]  [ Mask Aadhaar: [x] ]  [ Tamil Ration ] [ Reset ]     |
+-------------+------------------------------------------------------------------------------------+
| NAVIGATION  |                                                                                    |
|             |  +---------------------------+       +---------------------------+                 |
| [D] Dash    |  | FRONT FACE (Card 1)       |       | BACK FACE (Card 2)        |  PRINTER CONFIG |
| [*] Cards   |  |                           |       |                           |                 |
| [P] Photo   |  | [PHOTO]   விக்னேஷ் ஏ      |       |  முகவரி: எண் 12, காந்தி   |  Printer:       |
| [C] Design  |  |           Vignesh A       |       |  ரோடு, கரூர் - 639001     |  [Epson L805 v] |
| [B] Batch   |  |           DOB: 15/08/1990 |       |                           |                 |
| [W] WhatsApp|  |                           |       |  [QR CODE]                |  Tray:          |
| [L] Ledger  |  | [XXXX XXXX 1234]          |       |  Help: 1947               |  [CD/DVD Trayv] |
| [S] Calib   |  +---------------------------+       +---------------------------+                 |
| [?] Settings|   85.60 mm x 53.98 mm                 85.60 mm x 53.98 mm           Copies: [ 1 ]  |
|             |                                                                     Offset: X:0 Y:0|
|             |  -----------------------------------------------------------------  Price:  ₹ 70   |
|             |  Status: [OK] PDFium 600 DPI Verified | QR Code Decodable: YES                     |
|             |  Media Pre-Flight: Ready for Epson CD/DVD Tray                      [ PRINT NOW ]  |
+-------------+------------------------------------------------------------------------------------+
| Ready | Engine: .NET 10 LTS | Local Offline Execution | 0 Plaintext Retention | e-Sevai Karur     |
+--------------------------------------------------------------------------------------------------+
```

### Controls & States:
- **Central Preview Canvas:** Displays two vector-rendered CR80 cards side-by-side with exact rounded corners (3.18mm) and physical millimeter indicators.
- **Interactive Drag Handles:** If an unusual document is loaded, dragging the corner handles adjusts the bounding crop live on the canvas.
- **Pre-Flight Validation Box:** Displays a prominent green checkmark when the selected driver tray matches the document layout; displays an amber warning banner if paper source mismatch is detected.
- **"PRINT NOW" Action Button:** High-prominence 56px accent button with accelerator key `Ctrl+P`.
