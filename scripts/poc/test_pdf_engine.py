import os
import sys
import time
import io
import qrcode
import cv2
import numpy as np
from playwright.sync_api import sync_playwright
import pypdfium2 as pdfium
from pypdf import PdfReader, PdfWriter
from PIL import Image

def generate_synthetic_card():
    os.makedirs("samples", exist_ok=True)
    
    # 1. Generate QR Code
    qr = qrcode.QRCode(
        version=4,
        error_correction=qrcode.constants.ERROR_CORRECT_M,
        box_size=10,
        border=1,
    )
    qr_payload = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><PrintLetterBarcodeData uid=\"XXXXXXXX1234\" name=\"Vignesh A\" gender=\"M\" yob=\"1990\" co=\"S/O Arumugam\" loc=\"Gandhi Road\" vtc=\"Karur\" po=\"Karur\" dist=\"Karur\" state=\"Tamil Nadu\" pc=\"639001\"/>"
    qr.add_data(qr_payload)
    qr.make(fit=True)
    qr_img = qr.make_image(fill_color="black", back_color="white")
    qr_path = os.path.abspath("samples/test_qr.png")
    qr_img.save(qr_path)
    
    # 2. HTML template for exact CR80 (85.60 mm x 53.98 mm)
    html_content = f"""<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<style>
    @page {{
        size: 85.6mm 53.98mm;
        margin: 0;
    }}
    body {{
        margin: 0;
        padding: 0;
        width: 85.6mm;
        height: 53.98mm;
        font-family: 'Segoe UI', 'Nirmala UI', sans-serif;
        background: #fff;
        box-sizing: border-box;
        overflow: hidden;
        position: relative;
    }}
    .card-border {{
        width: 83.6mm;
        height: 51.98mm;
        margin: 1mm;
        border: 0.5pt solid #c0392b;
        border-radius: 3.18mm;
        box-sizing: border-box;
        padding: 1.5mm;
        display: flex;
        flex-direction: column;
        justify-content: space-between;
    }}
    .header {{
        border-bottom: 0.75pt solid #c0392b;
        padding-bottom: 0.8mm;
        display: flex;
        justify-content: space-between;
        align-items: center;
    }}
    .header-text {{
        font-size: 6pt;
        font-weight: bold;
        color: #2c3e50;
    }}
    .header-sub {{
        font-size: 4.5pt;
        color: #7f8c8d;
    }}
    .content {{
        display: flex;
        gap: 2mm;
        align-items: center;
        flex: 1;
        padding: 1mm 0;
    }}
    .photo-box {{
        width: 18mm;
        height: 22mm;
        border: 0.5pt solid #95a5a6;
        background: #ecf0f1;
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 5pt;
        color: #7f8c8d;
        flex-shrink: 0;
    }}
    .details {{
        flex: 1;
        font-size: 5pt;
        line-height: 1.35;
        color: #2c3e50;
    }}
    .name-ta {{
        font-size: 6.5pt;
        font-weight: bold;
        color: #c0392b;
    }}
    .name-en {{
        font-size: 5.5pt;
        font-weight: bold;
    }}
    .qr-box {{
        width: 20mm;
        height: 20mm;
        flex-shrink: 0;
    }}
    .qr-box img {{
        width: 100%;
        height: 100%;
        object-fit: contain;
    }}
    .footer {{
        border-top: 0.5pt solid #bdc3c7;
        padding-top: 0.8mm;
        display: flex;
        justify-content: space-between;
        align-items: center;
    }}
    .uid-num {{
        font-size: 7.5pt;
        font-weight: bold;
        letter-spacing: 0.8pt;
        color: #2c3e50;
    }}
    .micro-text {{
        font-size: 3.5pt;
        color: #95a5a6;
    }}
</style>
</head>
<body>
    <div class="card-border">
        <div class="header">
            <div>
                <div class="header-text">தமிழ்நாடு இ-சேவை அடையாள அட்டை</div>
                <div class="header-sub">Government of Tamil Nadu - e-Sevai Identity</div>
            </div>
            <div style="font-size: 5pt; font-weight: bold; color: #27ae60;">CR80 VERIFIED</div>
        </div>
        <div class="content">
            <div class="photo-box">PHOTO<br>35x45</div>
            <div class="details">
                <div class="name-ta">விக்னேஷ் ஏ</div>
                <div class="name-en">Vignesh A</div>
                <div>பிறந்த தேதி / DOB: 15/08/1990</div>
                <div>பாலினம் / Gender: ஆண் / Male</div>
                <div style="margin-top: 0.8mm;">முகவரி: எண் 12, காந்தி ரோடு,<br>கரூர், தமிழ்நாடு - 639001.</div>
            </div>
            <div class="qr-box">
                <img src="file:///{qr_path.replace(os.sep, '/')}" />
            </div>
        </div>
        <div class="footer">
            <div class="uid-num">XXXX XXXX 1234</div>
            <div class="micro-text">SECURE IDENTITY CARD - PHYSICAL PVC VERIFICATION SPECIFICATION</div>
        </div>
    </div>
</body>
</html>
"""
    html_path = "samples/test_card.html"
    with open(html_path, "w", encoding="utf-8") as f:
        f.write(html_content)
        
    print(f"[1/4] Generated synthetic HTML card with Tamil text and QR code: {html_path}")
    
    # 3. Render vector PDF via Playwright
    pdf_path = "samples/test_card_vector.pdf"
    with sync_playwright() as p:
        browser = p.chromium.launch()
        page = browser.new_page()
        page.goto(f"file:///{os.path.abspath(html_path).replace(os.sep, '/')}")
        page.pdf(
            path=pdf_path,
            width="85.6mm",
            height="53.98mm",
            print_background=True,
            margin={"top": "0", "bottom": "0", "left": "0", "right": "0"}
        )
        browser.close()
        
    print(f"[2/4] Compiled native vector PDF via Chromium: {pdf_path} ({os.path.getsize(pdf_path)} bytes)")
    
    # 4. Encrypt with password
    encrypted_pdf_path = "samples/test_card_encrypted.pdf"
    reader = PdfReader(pdf_path)
    writer = PdfWriter()
    for page in reader.pages:
        writer.add_page(page)
    writer.encrypt("VIGN1990")
    with open(encrypted_pdf_path, "wb") as f:
        writer.write(f)
        
    print(f"[3/4] Encrypted PDF with 128-bit AES password 'VIGN1990': {encrypted_pdf_path}")
    return encrypted_pdf_path

def benchmark_pdfium_rendering(encrypted_pdf_path):
    print("\n[4/4] Starting Google PDFium Benchmark across 300, 600, and 1200 DPI...")
    
    # Open encrypted document
    t0 = time.perf_counter()
    pdf = pdfium.PdfDocument(encrypted_pdf_path, password="VIGN1990")
    t_open = (time.perf_counter() - t0) * 1000
    print(f"-> Document opened & decrypted in {t_open:.2f} ms. Page count: {len(pdf)}")
    
    page = pdf[0]
    page_w_pt, page_h_pt = page.get_size()
    print(f"-> PDF Page Size in points: {page_w_pt:.2f}pt x {page_h_pt:.2f}pt")
    # Convert points to mm (1 pt = 25.4 / 72 mm = 0.352778 mm)
    w_mm = page_w_pt * 25.4 / 72.0
    h_mm = page_h_pt * 25.4 / 72.0
    print(f"-> Physical Dimensions: {w_mm:.2f} mm x {h_mm:.2f} mm (Standard CR80 target is 85.60 mm x 53.98 mm)")
    
    dpis = [300, 600, 1200]
    results = []
    
    for dpi in dpis:
        scale = dpi / 72.0
        t_start = time.perf_counter()
        bitmap = page.render(scale=scale)
        t_render = (time.perf_counter() - t_start) * 1000
        
        pil_img = bitmap.to_pil()
        out_path = f"samples/rendered_{dpi}dpi.png"
        pil_img.save(out_path)
        
        width_px, height_px = pil_img.size
        raw_bytes = width_px * height_px * 3 # 24-bit RGB
        png_size = os.path.getsize(out_path)
        
        # Test QR code scan using OpenCV
        cv_img = cv2.imread(out_path)
        detector = cv2.QRCodeDetector()
        val, pts, _ = detector.detectAndDecode(cv_img)
        qr_scanned = bool(val and "Vignesh" in val)
        
        results.append({
            "dpi": dpi,
            "render_ms": t_render,
            "width_px": width_px,
            "height_px": height_px,
            "raw_mb": raw_bytes / (1024 * 1024),
            "png_kb": png_size / 1024,
            "qr_scanned": qr_scanned
        })
        print(f"\n--- DPI: {dpi} ---")
        print(f"Resolution: {width_px} x {height_px} px")
        print(f"Render Time: {t_render:.2f} ms")
        print(f"Raw Bitmap RAM: {raw_bytes / (1024 * 1024):.2f} MB")
        print(f"Compressed PNG Size: {png_size / 1024:.1f} KB")
        print(f"QR Code Scan Successful: {qr_scanned}")
        
    return results

if __name__ == "__main__":
    enc_path = generate_synthetic_card()
    benchmark_pdfium_rendering(enc_path)
