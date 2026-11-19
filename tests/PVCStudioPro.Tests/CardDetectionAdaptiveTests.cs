using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using SkiaSharp;
using Xunit;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.Tests
{
    public class CardDetectionAdaptiveTests
    {
        private readonly TemplateService _templateService;
        private readonly DocumentTypeDetectionService _detectionService;
        private readonly CardDetectionService _cardDetection;

        public CardDetectionAdaptiveTests()
        {
            _templateService = new TemplateService();
            _detectionService = new DocumentTypeDetectionService(_templateService);
            _cardDetection = new CardDetectionService(_templateService, _detectionService);
        }

        private static SKBitmap CreateSyntheticA4Bitmap(int dpi = 300)
        {
            // A4 at given DPI: 210 x 297 mm
            int widthPx = (int)Math.Round((210.0 * dpi) / 25.4);
            int heightPx = (int)Math.Round((297.0 * dpi) / 25.4);

            var bitmap = new SKBitmap(widthPx, heightPx, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            return bitmap;
        }

        private static void DrawSyntheticCardCutlines(
            SKBitmap pageBitmap,
            int leftPx,
            int topPx,
            int widthPx,
            int heightPx)
        {
            using var canvas = new SKCanvas(pageBitmap);
            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke
            };

            // Draw a distinct rectangular cutline contour
            canvas.DrawRect(new SKRect(leftPx, topPx, leftPx + widthPx, topPx + heightPx), paint);

            // Fill card interior with slight off-white so it's detectable as a card area
            using var fillPaint = new SKPaint
            {
                Color = new SKColor(245, 247, 250),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(new SKRect(leftPx + 2, topPx + 2, leftPx + widthPx - 2, topPx + heightPx - 2), fillPaint);
        }

        [Fact]
        public async Task DetectAndExtractAdaptive_ModernEAadhaar_ShouldYieldHighConfidenceAndDualCards()
        {
            using var page = CreateSyntheticA4Bitmap(300);
            string modernAadhaarText = "Unique Identification Authority of India UIDAI Government of India " +
                                       "Enrolment No: 1234/56789/10111 Mera Aadhaar, Meri Pehchan " +
                                       "Proof of identity, not of citizenship help@uidai.gov.in 1947";

            var result = await _cardDetection.DetectAndExtractAdaptiveAsync(
                page,
                extractedText: modernAadhaarText,
                pageWidthMm: 210.0,
                pageHeightMm: 297.0);

            result.DetectedKind.Should().Be(DocumentKind.Aadhaar);
            result.AadhaarGen.Should().Be(AadhaarGeneration.Modern2022Plus);
            result.Confidence.IsHighConfidence.Should().BeTrue();
            result.Confidence.ConfidencePercent.Should().BeGreaterThanOrEqualTo(85);
            result.RequiresManualConfirmation.Should().BeFalse();

            result.FrontCard.Should().NotBeNull();
            result.FrontCard!.Width.Should().Be(2022);
            result.FrontCard!.Height.Should().Be(1275);

            result.BackCard.Should().NotBeNull();
            result.BackCard!.Width.Should().Be(2022);
            result.BackCard!.Height.Should().Be(1275);
        }

        [Fact]
        public async Task DetectAndExtractAdaptive_BaalAadhaar_ShouldMatchChildAadhaarKeywords()
        {
            using var page = CreateSyntheticA4Bitmap(300);
            string baalAadhaarText = "Unique Identification Authority of India UIDAI " +
                                     "Aadhaar is valid till child turns 5 years old Baal Aadhaar Blue Aadhaar " +
                                     "Mera Aadhaar, Meri Pehchan 1947";

            var result = await _cardDetection.DetectAndExtractAdaptiveAsync(
                page,
                extractedText: baalAadhaarText,
                pageWidthMm: 210.0,
                pageHeightMm: 297.0);

            result.DetectedKind.Should().Be(DocumentKind.Aadhaar);
            result.Confidence.ConfidencePercent.Should().BeGreaterThanOrEqualTo(85);
            result.FrontCard.Should().NotBeNull();
            result.FrontRect.Should().NotBeNull();
        }

        [Fact]
        public async Task DetectAndExtractAdaptive_MAadhaar_ShouldMatchMobileAadhaarFormat()
        {
            using var page = CreateSyntheticA4Bitmap(300);
            string mAadhaarText = "UIDAI myAadhaar Official Portal " +
                                  "Unique Identification Authority of India " +
                                  "Download Aadhaar Virtual ID VID: 9123 4567 8901 2345 " +
                                  "Mera Aadhaar, Meri Pehchan";

            var result = await _cardDetection.DetectAndExtractAdaptiveAsync(
                page,
                extractedText: mAadhaarText,
                pageWidthMm: 210.0,
                pageHeightMm: 297.0);

            result.DetectedKind.Should().Be(DocumentKind.Aadhaar);
            result.Confidence.IsHighConfidence.Should().BeTrue();
            result.FrontCard.Should().NotBeNull();
            result.BackCard.Should().NotBeNull();
        }

        [Fact]
        public async Task DetectAndExtractAdaptive_ShiftedScan_ShouldRefineBoundsViaOpenCv()
        {
            using var page = CreateSyntheticA4Bitmap(300);

            // Calibrated Aadhaar Front at 300 DPI is approx X=210, Y=2595, W=1045, H=665.
            // Introduce a +40 px shift (3.4 mm scanner margin shift)
            int shiftedX = 250;
            int shiftedY = 2635;
            int cardW = 1045;
            int cardH = 665;

            DrawSyntheticCardCutlines(page, shiftedX, shiftedY, cardW, cardH);

            var seedRect = new SKRectI(210, 2595, 210 + cardW, 2595 + cardH);

            var refinedRect = await _cardDetection.RefineCropBoundsWithOpenCvAsync(page, seedRect, searchWindowMm: 15.0, dpi: 300.0);

            // Refined bounds should snap to the actual shifted cutline rectangle
            refinedRect.Left.Should().BeCloseTo(shiftedX, 10);
            refinedRect.Top.Should().BeCloseTo(shiftedY, 10);
            refinedRect.Width.Should().BeCloseTo(cardW, 10);
            refinedRect.Height.Should().BeCloseTo(cardH, 10);
        }

        [Fact]
        public async Task DetectAndExtractAdaptive_CorruptedOrEmptyText_ShouldTriggerLowConfidenceGracefulFallback()
        {
            // Create a non-standard page size (not A4) with zero text to test low confidence
            var degradedBitmap = new SKBitmap(800, 800, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(degradedBitmap))
            {
                canvas.Clear(SKColors.Gray);
            }

            var result = await _cardDetection.DetectAndExtractAdaptiveAsync(
                degradedBitmap,
                extractedText: string.Empty,
                pageWidthMm: 100.0,
                pageHeightMm: 100.0);

            result.Confidence.OverallConfidence.Should().BeLessThan(0.85);
            result.Confidence.IsHighConfidence.Should().BeFalse();
            result.RequiresManualConfirmation.Should().BeTrue();
            result.Confidence.Summary.Should().Contain("Low Confidence");

            degradedBitmap.Dispose();
        }

        [Fact]
        public async Task RectifyCardPerspective_RotatedQuad_ShouldProduceTargetCR80Bitmap()
        {
            using var testBitmap = new SKBitmap(1200, 800, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(testBitmap))
            {
                canvas.Clear(SKColors.White);
                using var p = new SKPaint { Color = SKColors.Blue };
                canvas.DrawRect(100, 100, 800, 500, p);
            }

            // Define slightly skewed quadrilateral points
            var quad = new QuadrilateralPoints(
                new SKPoint(105, 100),
                new SKPoint(895, 115),
                new SKPoint(890, 605),
                new SKPoint(100, 590)
            );

            var rectified = await _cardDetection.RectifyCardPerspectiveAsync(testBitmap, quad, 2022, 1275);

            rectified.Should().NotBeNull();
            rectified.Width.Should().Be(2022);
            rectified.Height.Should().Be(1275);
            rectified.Dispose();
        }

        [Fact]
        public void DetectionConfidenceScore_ShouldComputePropertiesCorrectly()
        {
            var high = new DetectionConfidenceScore(0.92, 1.0, 0.95, 1.0, 0.85);
            high.ConfidencePercent.Should().Be(92);
            high.IsHighConfidence.Should().BeTrue();
            high.Summary.Should().Be("92% High Confidence");

            var low = new DetectionConfidenceScore(0.73, 0.8, 0.6, 0.7, 0.75);
            low.ConfidencePercent.Should().Be(73);
            low.IsHighConfidence.Should().BeFalse();
            low.Summary.Should().Be("73% Low Confidence");
        }

        [Fact]
        public void ProductionWorkspace_WhenLowConfidence_ShouldDisplayAmberBannerAndAutoOpenCropDrawer()
        {
            var vm = CreateWorkspaceViewModel();

            // Simulate low confidence detection applied
            var lowScore = new DetectionConfidenceScore(0.72, 0.7, 0.5, 0.8, 0.65);
            var frontSeedRect = new SKRectI(210, 2595, 1255, 3260);

            // Invoke ApplyDetectionConfidence via reflection
            var method = typeof(PVCStudioPro.App.ViewModels.ProductionWorkspaceViewModel)
                .GetMethod("ApplyDetectionConfidence", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method.Should().NotBeNull();
            method!.Invoke(vm, new object?[] { lowScore, frontSeedRect });

            vm.IsLowConfidenceBannerVisible.Should().BeTrue();
            vm.DetectionConfidencePercent.Should().Be(72);
            vm.IsCropDrawerOpen.Should().BeTrue();
            vm.CropX.Should().Be(210);
            vm.CropY.Should().Be(2595);
            vm.CropWidth.Should().Be(1045);
            vm.CropHeight.Should().Be(665);

            // Dismiss banner
            vm.DismissLowConfidenceBanner();
            vm.IsLowConfidenceBannerVisible.Should().BeFalse();
        }

        [Fact]
        public void ProductionWorkspace_WhenHighConfidence_ShouldNotDisplayAmberBanner()
        {
            var vm = CreateWorkspaceViewModel();

            var highScore = new DetectionConfidenceScore(0.96, 1.0, 1.0, 1.0, 0.85);
            var frontSeedRect = new SKRectI(210, 2595, 1255, 3260);

            var method = typeof(PVCStudioPro.App.ViewModels.ProductionWorkspaceViewModel)
                .GetMethod("ApplyDetectionConfidence", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method.Should().NotBeNull();
            method!.Invoke(vm, new object?[] { highScore, frontSeedRect });

            vm.IsLowConfidenceBannerVisible.Should().BeFalse();
            vm.DetectionConfidencePercent.Should().Be(96);
            vm.DetectionConfidenceSummary.Should().Be("96% High Confidence");
        }

        private static PVCStudioPro.App.ViewModels.ProductionWorkspaceViewModel CreateWorkspaceViewModel()
        {
            var importService = new DocumentImportService();
            var pdfService = new PdfService();
            var templateService = new TemplateService();
            var detectionService = new DocumentTypeDetectionService(templateService);
            var cardDetection = new CardDetectionService(templateService, detectionService);
            var cropService = new ManualCropService();
            var renderService = new PhysicalPageRenderService();
            var printerDiscovery = new PrinterDiscoveryService();
            var printService = new PrintService();
            var dbPath = Path.Combine(Path.GetTempPath(), $"pvc_test_{Guid.NewGuid():N}.db");
            var jobLedger = new JobLedgerService(dbPath);
            var sessionManager = new DocumentSessionManager();

            return new PVCStudioPro.App.ViewModels.ProductionWorkspaceViewModel(
                importService,
                pdfService,
                detectionService,
                cardDetection,
                cropService,
                renderService,
                printerDiscovery,
                printService,
                jobLedger,
                templateService,
                sessionManager);
        }
    }
}
