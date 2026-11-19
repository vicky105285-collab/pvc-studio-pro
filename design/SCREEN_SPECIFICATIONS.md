# Screen Specifications & Functional Architecture — PVC Studio Pro

**Target Version:** 1.0.0-redesign  
**Document Type:** Functional Architecture & Workspace Blueprint  

---

## Workspace Inventory & Functional Specifications

### 1. Dashboard (`DashboardView`)
- **Purpose:** Daily command center for the e-Sevai operator upon launch.
- **Components:**
  - **KPI Status Ribbon:** 4 responsive metric tiles: Today's Cards Printed, Today's Photo Sheets, Today's Total Revenue (₹), Cash vs. UPI Payment Distribution.
  - **Quick Production Actions:** One-click shortcuts to *Print ID Card*, *Generate Passport Photos*, *Batch Print*, and *Calibrate Tray*.
  - **Hardware Connection Tiles:** Real-time indicator of discovered Windows printers, active tray profile, and readiness status.
  - **Recent Activity Stream:** Last 8 transactions with reprint shortcut and time elapsed.
  - **System Notice Banner:** Displays calibration recommendations, backup status, or unverified hardware warnings.

---

### 2. Card Studio (`CardPrintView`)
- **Purpose:** Core single-card production workspace for government identity and smart cards.
- **Components:**
  - **Top Action Bar:** Drag-and-drop input zone, file picker, interactive password unlock dialog for encrypted e-Aadhaar PDFs, UIDAI first-8-digits mask toggle, and real-time template confidence badge.
  - **Interactive Visual Stage:**
    - Dual CR80 canvas ($85.60 \times 53.98\text{ mm}$ at 600 DPI vector) showing Front Face (Slot 1) and Back Face (Slot 2).
    - 1mm bleed guideline visualization.
    - Front/Back flip view, 90° rotation controls, and zoom/pan navigator.
    - UIDAI masked redaction preview showing exact pixel coverage.
  - **Production Inspector (Right Pane):**
    - Printer device picker (populated dynamically from Windows spooler).
    - Active Tray & Attachment selector (Aftermarket PVC Tray vs Printer-Supplied Attachment).
    - Copy quantity, card pricing (₹), and payment mode (Cash/UPI).
    - Millimeter calibration fine-tuning (Offset X & Offset Y).
    - Dragon Sheet Thermal Lamination Mirror Mode toggle.
    - Primary **PRINT PVC CARD** action button with pre-flight check validation.

---

### 3. Photo Studio (`PhotoStudioView`)
- **Purpose:** Professional biometric passport and visa photo production.
- **Components:**
  - **Image Loader & Stage:** Load customer portrait, 90° lossless rotation, crop adjustments.
  - **Biometric Face Guides:** Overlay showing standard ICAO / Indian passport guidelines (crown line, eye level, chin base).
  - **Classical GrabCut Segmentation Engine:**
    - Real offline background removal without cloud dependencies.
    - Studio background replacement: Pure White, Studio Blue (`#4169E1`), Neutral Grey (`#E2E8F0`), or custom color picker.
    - Feathered alpha edge matting.
  - **Color & Exposure Controls:** Brightness and contrast adjustment sliders.
  - **Sheet Layout Generator:**
    - 4x6" Sheet (8 photos) with cut marks.
    - A4 Sheet (16 or 32 photos) with margin spacing.
    - Single photo export ($35 \times 45\text{ mm}$ at 600 DPI).
  - **Print & Export:** Direct Windows spooler print or PNG file export.

---

### 4. Document Processing / Deskew (`DocumentProcessingView`)
- **Purpose:** Correct and extract cards from physical scanned or photographed documents.
- **Components:**
  - Open scanned PDF or photographed image.
  - Automatic 4-corner card edge detection via OpenCV contour analysis.
  - Interactive manual corner adjustment pins.
  - Perspective warp rectification to exact CR80 physical proportions.
  - Brightness/contrast enhancement to clean up smartphone photo shadows.
  - Direct handoff to Card Studio for printing.

---

### 5. Batch Processing (`BatchProcessingView`)
- **Purpose:** High-volume queue processing for schools, companies, or bulk citizen registrations.
- **Components:**
  - Drag-and-drop folder or multi-file selection.
  - Batch table showing filename, detected template, confidence score, and status.
  - Bulk template assignment and operator verification flags.
  - Batch job queue with progress bar, pause/resume, and cancellation.
  - Error reporting table for password-protected or corrupt documents.
  - Spool summary report.

---

### 6. Print Center (`PrintCenterView`)
- **Purpose:** Spooler monitoring, queue management, and reprint center.
- **Components:**
  - Active Windows printer spooler queue monitor.
  - Real-time job status (Spooling, Printing, Error, Offline).
  - In-memory ephemeral reprint buffer (15-minute quick reprint for tray jams).
  - Printer queue purge / cancel job command.

---

### 7. Printer & Tray Manager (`PrinterTrayManagerView`)
- **Purpose:** Multi-printer hardware configuration and independent attachment profiles.
- **Components:**
  - Dynamic discovery of all Windows installed printers (Epson, Canon, HP, Fargo, etc.).
  - **Multi-Tray Attachment Profile Architecture:**
    - Support multiple profiles per physical printer (e.g. Epson L805 with Aftermarket Tray A vs Printer-Supplied Attachment B).
    - Dedicated profile storage for Epson L805, L8050, L18050, Canon G-series, and Direct CR80.
    - Configurable canvas dimensions, slot positions, feed directions, and printable area margins.
    - Profile duplicate, export, and import tools.
    - Physical verification status flag (`Physically Verified` vs `Software Profile Ready`).

---

### 8. Calibration Studio (`CalibrationView`)
- **Purpose:** Precise millimeter alignment and registration correction.
- **Components:**
  - Step-by-step visual calibration guide with caliper illustrations.
  - Printable millimeter calibration target with crosshairs, edge rulers, and CR80 nominal boundaries.
  - Digital caliper discrepancy calculators ($\Delta X = X_{\text{meas}} - X_{\text{nom}}$, $\Delta Y = Y_{\text{meas}} - Y_{\text{nom}}$).
  - Fine adjustment sliders with real-time numeric readouts.
  - Profile save and reset to factory defaults.

---

### 9. Template Manager (`TemplateManagerView`)
- **Purpose:** Card template inspection, crop tuning, and custom document format creation.
- **Components:**
  - Visual catalog of all 10 Indian Government ID formats (Aadhaar, PAN NSDL/UTIITSL, Voter, Ayushman, TN Ration, DL, RC, e-Shram, ABHA).
  - Interactive card coordinate editor (Front X/Y/W/H and Back X/Y/W/H in millimeters).
  - Keyword anchor editor for automatic identification rules.
  - Create Custom CR80 Template wizard (for local student IDs, club cards, employee badges).
  - Template validation tester and JSON import/export.

---

### 10. Business Profile (`BusinessProfileView`)
- **Purpose:** Shop identity, commercial invoice setup, and operational defaults.
- **Components:**
  - **Business Identity:** Shop name, Operator name, Business type, Logo path, Full address, District, State, PIN code, Phone, Email, Website.
  - **Billing & Invoicing:** Currency (₹ INR), Receipt numbering prefix, Next invoice number, Default payment modes (Cash / UPI / Split), Tax configuration, Custom receipt footer.
  - **Hardware Defaults:** Default printer, default tray profile, default card price (₹), default photo sheet price (₹).
  - **Receipt Paper Sizing:** Toggle between 58mm Thermal, 80mm Thermal, and A4 Invoice.
  - Save status indicators and unsaved changes confirmation dialog.

---

### 11. Transactions & Ledger (`BusinessLedgerView`)
- **Purpose:** Operational audit log and instant thermal receipt reprint.
- **Components:**
  - Date-range filter (Today, Yesterday, Last 7 Days, This Month, Custom Range).
  - Live search by Job Number or Document Type.
  - Full financial table: Job Number, Time, Document Type, Quantity, Amount (₹), Payment Mode, Printer, Status.
  - One-click 58mm/80mm Thermal Receipt Generator.
  - Export to CSV / Excel spreadsheet.
  - Strict Zero-Citizen-PII Guarantee (schema verified).

---

### 12. Reports & Analytics (`ReportsAnalyticsView`)
- **Purpose:** Business performance, volume trends, and revenue intelligence.
- **Components:**
  - Monthly revenue cards and card volume summaries.
  - Payment mode breakdown (Cash vs UPI percentage split).
  - Most popular document categories (Aadhaar, PAN, Voter, Driving Licence).
  - Peak business hours analysis.

---

### 13. Backup & Restore (`BackupRestoreView`)
- **Purpose:** Disaster recovery and local data protection.
- **Components:**
  - One-click manual backup of SQLite database and profile configurations to local zip/folder.
  - Automated backup schedule (Daily on close / Weekly).
  - Backup history list with file size and timestamp.
  - Safe restore preview showing transaction count before restoring.
  - Automatic pre-restore safety copy to prevent accidental database overwriting.

---

### 14. Settings & Preferences (`SettingsView`)
- **Purpose:** Application environment and personal preferences.
- **Components:**
  - **Theme Selection:** Modern Light Theme, Dark Theme, or Match Windows System Theme.
  - **Language Selector:** English and Tamil (`தமிழ்`).
  - **UI Scaling:** 100%, 125%, 150% dynamic interface scale override.
  - **Storage Paths:** Custom directory for calibration sheets, exported photos, and receipts.
  - **Diagnostics & About:** Version, license, dependency SBOM, and system information.
