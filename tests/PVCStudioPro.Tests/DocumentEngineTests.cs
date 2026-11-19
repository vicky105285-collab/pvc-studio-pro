using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using SkiaSharp;
using Xunit;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.Tests
{
    public class DocumentEngineTests
    {
        private readonly DocumentImportService _importService;
        private readonly OfficeDocumentService _officeService;
        private readonly TemplateService _templateService;
        private readonly DocumentTypeDetectionService _detectionService;
        private readonly CardDetectionService _cardDetection;
        private readonly ManualCropService _cropService;
        private readonly PrintPreviewService _previewService;

        public DocumentEngineTests()
        {
            _importService = new DocumentImportService();
            _officeService = new OfficeDocumentService();
            _templateService = new TemplateService();
            _detectionService = new DocumentTypeDetectionService(_templateService);
            _cardDetection = new CardDetectionService();
            _cropService = new ManualCropService();
            _previewService = new PrintPreviewService();
        }

        #region 1. Magic Bytes Sniffing Tests

        [Fact]
        public void SniffStream_ShouldIdentify_PdfMagicBytes()
        {
            byte[] pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7\nSample content");
            using var ms = new MemoryStream(pdfBytes);

            var result = _importService.SniffStream(ms, "aadhaar_download.pdf");

            result.MagicKind.Should().Be(FileMagicKind.Pdf);
            result.MimeType.Should().Be("application/pdf");
            result.IsCorrupt.Should().BeFalse();
        }

        [Fact]
        public void SniffStream_ShouldIdentify_JpegMagicBytes()
        {
            byte[] jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
            using var ms = new MemoryStream(jpegBytes);

            var result = _importService.SniffStream(ms, "camera_photo.jpg");

            result.MagicKind.Should().Be(FileMagicKind.Jpeg);
            result.MimeType.Should().Be("image/jpeg");
        }

        [Fact]
        public void SniffStream_ShouldIdentify_PngMagicBytes()
        {
            byte[] pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
            using var ms = new MemoryStream(pngBytes);

            var result = _importService.SniffStream(ms, "scanned_card.png");

            result.MagicKind.Should().Be(FileMagicKind.Png);
            result.MimeType.Should().Be("image/png");
        }

        [Fact]
        public void SniffStream_ShouldIdentify_BmpMagicBytes()
        {
            byte[] bmpBytes = new byte[] { 0x42, 0x4D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            using var ms = new MemoryStream(bmpBytes);

            var result = _importService.SniffStream(ms, "scan.bmp");

            result.MagicKind.Should().Be(FileMagicKind.Bmp);
            result.MimeType.Should().Be("image/bmp");
        }

        [Fact]
        public void SniffStream_ShouldIdentify_RtfMagicBytes()
        {
            byte[] rtfBytes = Encoding.ASCII.GetBytes("{\\rtf1\\ansi\\deff0 Sample document}");
            using var ms = new MemoryStream(rtfBytes);

            var result = _importService.SniffStream(ms, "affidavit.rtf");

            result.MagicKind.Should().Be(FileMagicKind.Rtf);
            result.MimeType.Should().Be("application/rtf");
        }

        [Fact]
        public void SniffStream_ShouldIdentify_DocxZipArchive()
        {
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry("word/document.xml");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:t>Tamil Nadu e-Sevai Petition</w:t></w:p></w:body></w:document>");
            }
            ms.Position = 0;

            var result = _importService.SniffStream(ms, "petition.docx");

            result.MagicKind.Should().Be(FileMagicKind.Docx);
            result.MimeType.Should().Contain("wordprocessingml");
        }

        [Fact]
        public void SniffStream_ShouldIdentify_PlainText()
        {
            byte[] txtBytes = Encoding.UTF8.GetBytes("Application for Smart Ration Card\nDistrict: Karur\nTaluk: Aravakurichi");
            using var ms = new MemoryStream(txtBytes);

            var result = _importService.SniffStream(ms, "notes.txt");

            result.MagicKind.Should().Be(FileMagicKind.Text);
            result.MimeType.Should().Be("text/plain");
        }

        [Fact]
        public void SniffStream_ShouldFlagCorrupt_WhenEmpty()
        {
            using var ms = new MemoryStream(new byte[0]);

            var result = _importService.SniffStream(ms, "empty.pdf");

            result.IsCorrupt.Should().BeTrue();
            result.MagicKind.Should().Be(FileMagicKind.Unknown);
        }

        #endregion

        #region 2. Office Document Service Tests

        [Fact]
        public async Task OfficeDocumentService_ShouldExtractTextAndRenderDocx()
        {
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry("word/document.xml");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:t>Government of Tamil Nadu</w:t></w:p><w:p><w:t>Revenue and Disaster Management Department</w:t></w:p></w:body></w:document>");
            }
            ms.Position = 0;

            string text = await _officeService.ExtractTextAsync(ms, FileMagicKind.Docx);
            text.Should().Contain("Government of Tamil Nadu");
            text.Should().Contain("Revenue and Disaster Management Department");

            ms.Position = 0;
            var pages = await _officeService.RenderOfficeDocumentAsync(ms, FileMagicKind.Docx, 150);
            pages.Should().NotBeEmpty();
            pages[0].Width.Should().BeGreaterThan(1000); // ~1240 px at 150 DPI A4
            pages[0].Height.Should().BeGreaterThan(1500); // ~1754 px at 150 DPI A4
        }

        [Fact]
        public async Task OfficeDocumentService_ShouldRenderPlainText_ToA4Pages()
        {
            string longText = new string('A', 500) + "\n" + new string('B', 500);
            byte[] bytes = Encoding.UTF8.GetBytes(longText);
            using var ms = new MemoryStream(bytes);

            var pages = await _officeService.RenderOfficeDocumentAsync(ms, FileMagicKind.Text, 150);

            pages.Should().NotBeEmpty();
            pages[0].Width.Should().Be(1240); // 210mm * 150 / 25.4
            pages[0].Height.Should().Be(1754); // 297mm * 150 / 25.4
        }

        #endregion

        #region 3. Document Type & Multi-Generation Detection Tests

        [Fact]
        public async Task DetectionService_ShouldIdentify_ModernAadhaar()
        {
            string sampleText = "Unique Identification Authority of India Government of India Enrolment No: 1234/56789/10111 " +
                                "To Suresh Kumar DOB: 15/08/1985 VID: 9123 4567 8901 2345 " +
                                "Aadhaar is a proof of identity, not of citizenship. Help: help@uidai.gov.in 1947";

            var result = await _detectionService.DetectDocumentTypeAsync(null, sampleText);

            result.DetectedKind.Should().Be(DocumentKind.Aadhaar);
            result.AadhaarGen.Should().Be(AadhaarGeneration.Modern2022Plus);
            result.DisplayName.Should().Contain("Modern e-Aadhaar");
            result.Confidence.Should().BeGreaterThan(0.80);
        }

        [Fact]
        public async Task DetectionService_ShouldIdentify_MaskedAadhaar()
        {
            string sampleText = "Unique Identification Authority of India UIDAI Enrolment No: 1111/22222/33333 " +
                                "XXXX XXXX 1234 VID: 9876 5432 1098 7654 Masked Aadhaar download";

            var result = await _detectionService.DetectDocumentTypeAsync(null, sampleText);

            result.DetectedKind.Should().Be(DocumentKind.Aadhaar);
            result.AadhaarGen.Should().Be(AadhaarGeneration.Masked2018To2022);
            result.DisplayName.Should().Contain("Masked e-Aadhaar");
        }

        [Fact]
        public async Task DetectionService_ShouldIdentify_PvcOrderLetter()
        {
            string sampleText = "Unique Identification Authority of India Order Aadhaar PVC Card Acknowledgement Slip " +
                                "SRN: S12345678901 Aadhaar Number: XXXX XXXX 5678";

            var result = await _detectionService.DetectDocumentTypeAsync(null, sampleText);

            result.DetectedKind.Should().Be(DocumentKind.Aadhaar);
            result.AadhaarGen.Should().Be(AadhaarGeneration.PvcOrderLetter);
        }

        [Fact]
        public async Task DetectionService_ShouldIdentify_PanCard()
        {
            string sampleText = "INCOME TAX DEPARTMENT GOVT. OF INDIA Permanent Account Number Card PAN: ABCDE1234F Father's Name: RAMASAMY";

            var result = await _detectionService.DetectDocumentTypeAsync(null, sampleText);

            result.DetectedKind.Should().Be(DocumentKind.Pan);
            result.DisplayName.Should().Contain("PAN Card");
            result.Confidence.Should().BeGreaterThan(0.70);
        }

        [Fact]
        public async Task DetectionService_ShouldIdentify_VoterId()
        {
            string sampleText = "ELECTION COMMISSION OF INDIA ELECTOR'S PHOTO IDENTITY CARD EPIC NO: TN/01/123/456789 BHARAT NIRVACHAN AAYOG";

            var result = await _detectionService.DetectDocumentTypeAsync(null, sampleText);

            result.DetectedKind.Should().Be(DocumentKind.VoterId);
            result.DisplayName.Should().Contain("Voter ID");
        }

        [Fact]
        public async Task DetectionService_ShouldIdentify_DrivingLicence()
        {
            string sampleText = "UNION OF INDIA DRIVING LICENCE FORM 7 Transport Department Licensing Authority Sarathi DL NO: TN-47-2020-0012345";

            var result = await _detectionService.DetectDocumentTypeAsync(null, sampleText);

            result.DetectedKind.Should().Be(DocumentKind.DrivingLicence);
            result.DisplayName.Should().Contain("Driving Licence");
        }

        [Fact]
        public async Task DetectionService_ShouldIdentify_AyushmanBharat()
        {
            string sampleText = "Ayushman Bharat Pradhan Mantri Jan Arogya Yojana PM-JAY Golden Card ABHA ID: 12-3456-7890-1234";

            var result = await _detectionService.DetectDocumentTypeAsync(null, sampleText);

            result.DetectedKind.Should().Be(DocumentKind.AyushmanBharat);
            result.DisplayName.Should().Contain("Ayushman Bharat");
        }

        [Fact]
        public async Task DetectionService_ShouldIdentify_TamilNaduRationCard()
        {
            string sampleText = "Civil Supplies and Consumer Protection Department Government of Tamil Nadu Smart Card TNEPDS குடும்ப அட்டை 33/G/1234567";

            var result = await _detectionService.DetectDocumentTypeAsync(null, sampleText);

            result.DetectedKind.Should().Be(DocumentKind.TamilNaduRation);
            result.DisplayName.Should().Contain("Tamil Nadu Smart Family Card");
        }

        #endregion

        #region 4. Card Detection & Crop Studio Mathematics Tests

        [Fact]
        public async Task CardDetection_ShouldExtractDualCards_ToCr80Dimensions()
        {
            // Synthesize an A4 image at 150 DPI (1240 x 1754 px)
            using var a4Bitmap = new SKBitmap(1240, 1754, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(a4Bitmap))
            {
                canvas.Clear(SKColors.White);
                // Draw simulated front and back card rectangles at bottom
                using var p = new SKPaint { Color = SKColors.Navy };
                canvas.DrawRect(new SKRect(100, 1200, 600, 1500), p);
                canvas.DrawRect(new SKRect(650, 1200, 1150, 1500), p);
            }

            var result = await _cardDetection.DetectAndExtractDualCardsAsync(a4Bitmap, DocumentKind.Aadhaar, AadhaarGeneration.Modern2022Plus);

            result.Front.Should().NotBeNull();
            result.Back.Should().NotBeNull();
            // CR80 standard 600 DPI target resolution: 2022 x 1275 px
            result.Front!.Width.Should().Be(2022);
            result.Front!.Height.Should().Be(1275);
            result.Back!.Width.Should().Be(2022);
            result.Back!.Height.Should().Be(1275);
        }

        [Fact]
        public void ManualCropService_ShouldWarpQuadrilateral_ToCr80Dimensions()
        {
            using var source = new SKBitmap(1000, 1000, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(source))
            {
                canvas.Clear(SKColors.LightGray);
                using var p = new SKPaint { Color = SKColors.Blue };
                canvas.DrawRect(new SKRect(100, 100, 800, 600), p);
            }

            // Quadrilateral points representing a skewed mobile phone capture
            var quad = new QuadrilateralPoints(
                new SKPoint(120, 130), // Top-Left skewed
                new SKPoint(780, 110), // Top-Right skewed
                new SKPoint(810, 590), // Bottom-Right skewed
                new SKPoint(110, 610)  // Bottom-Left skewed
            );

            var cropped = _cropService.CropPerspective(source, quad, CropAspectRatio.Cr80Card, 600);

            cropped.Should().NotBeNull();
            cropped.Width.Should().Be(2022); // 85.60 mm * 600 / 25.4
            cropped.Height.Should().Be(1275); // 53.98 mm * 600 / 25.4
        }

        [Fact]
        public void ManualCropService_ShouldRotateAndFlip_Accurately()
        {
            using var bmp = new SKBitmap(200, 100, SKColorType.Bgra8888, SKAlphaType.Premul);
            var rotated = _cropService.RotateBitmap(bmp, 90);

            rotated.Width.Should().Be(100);
            rotated.Height.Should().Be(200);

            var flipped = _cropService.FlipHorizontal(bmp);
            flipped.Width.Should().Be(200);
            flipped.Height.Should().Be(100);
        }

        #endregion

        #region 5. Print Preview & Virtual Tray Simulation Tests

        [Fact]
        public async Task PrintPreviewService_ShouldGenerateEpsonTrayLayout()
        {
            using var front = new SKBitmap(2022, 1275, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var back = new SKBitmap(2022, 1275, SKColorType.Bgra8888, SKAlphaType.Premul);

            var config = new PrintPreviewConfig
            {
                MediaKind = TargetMediaKind.Epson2CardTray,
                OffsetXMm = 1.0,
                OffsetYMm = -0.5,
                Copies = 1
            };

            var render = await _previewService.GeneratePreviewAsync(front, back, config);

            render.PreviewBitmap.Should().NotBeNull();
            render.CanvasWidthMm.Should().Be(130.0);
            render.CanvasHeightMm.Should().Be(220.0);
            render.Placements.Should().HaveCount(2);

            // Slot 1 front with 1.0mm offset
            render.Placements[0].XPositionMm.Should().Be(22.2 + 1.0);
            render.Placements[0].YPositionMm.Should().Be(18.0 - 0.5);

            // Slot 2 back with 1.0mm offset
            render.Placements[1].XPositionMm.Should().Be(22.2 + 1.0);
            render.Placements[1].YPositionMm.Should().Be(110.0 - 0.5);
        }

        [Fact]
        public async Task PrintPreviewService_ShouldGenerateSingleCr80DirectLayout()
        {
            using var front = new SKBitmap(2022, 1275, SKColorType.Bgra8888, SKAlphaType.Premul);

            var config = new PrintPreviewConfig
            {
                MediaKind = TargetMediaKind.SingleCardCR80,
                OffsetXMm = 0.0,
                OffsetYMm = 0.0
            };

            var render = await _previewService.GeneratePreviewAsync(front, null, config);

            render.PreviewBitmap.Should().NotBeNull();
            render.CanvasWidthMm.Should().Be(85.60);
            render.CanvasHeightMm.Should().Be(53.98);
            render.Placements.Should().HaveCount(1);
        }

        #endregion
    }
}
