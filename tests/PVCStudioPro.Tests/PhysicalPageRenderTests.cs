using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SkiaSharp;
using Xunit;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.Tests
{
    public class PhysicalPageRenderTests
    {
        private readonly PhysicalPageLayoutEngine _layoutEngine;
        private readonly PhysicalPageRenderService _renderService;
        private readonly PrintService _printService;

        public PhysicalPageRenderTests()
        {
            _layoutEngine = new PhysicalPageLayoutEngine();
            _renderService = new PhysicalPageRenderService();
            _printService = new PrintService(_renderService);
        }

        [Theory]
        [InlineData(150)]
        [InlineData(300)]
        [InlineData(600)]
        [InlineData(1200)]
        public void MillimeterPixelRoundtrip_AtMultipleDpis_ShouldBeAccurateWithinOneSubMillimeter(int dpi)
        {
            double[] testMillimeters = { 0.0, 15.0, 53.98, 85.60, 109.4, 210.0, 297.0 };

            foreach (var mm in testMillimeters)
            {
                var (pxX, pxY) = _layoutEngine.MillimetersToPixels(mm, mm, dpi);
                var (roundtripX, roundtripY) = _layoutEngine.PixelsToMillimeters(pxX, pxY, dpi);

                roundtripX.Should().BeApproximately(mm, 0.001, $"X roundtrip at {dpi} DPI for {mm} mm should be exact");
                roundtripY.Should().BeApproximately(mm, 0.001, $"Y roundtrip at {dpi} DPI for {mm} mm should be exact");
            }
        }

        [Fact]
        public void SingleSideA4_LayoutGeometry_ShouldMatchStandardCompetitorArrangement()
        {
            using var frontBmp = new SKBitmap(2022, 1275);
            using var backBmp = new SKBitmap(2022, 1275);

            var layout = _layoutEngine.CreateLayout(
                SheetLayoutMode.SingleSideA4,
                frontBmp,
                backBmp,
                showCutMarks: true,
                showFoldLine: false,
                mirrorMode: false,
                offsetXMm: 0.0,
                offsetYMm: 0.0,
                targetDpi: 600);

            layout.Preset.Should().Be(PaperPreset.A4);
            layout.PageWidthMm.Should().Be(210.0);
            layout.PageHeightMm.Should().Be(297.0);
            layout.Items.Should().HaveCount(2);

            var front = layout.Items.First(i => i.Kind == LayoutItemKind.FrontCard);
            front.XPositionMm.Should().Be(15.0);
            front.YPositionMm.Should().Be(15.0);
            front.WidthMm.Should().Be(85.60);
            front.HeightMm.Should().Be(53.98);

            var back = layout.Items.First(i => i.Kind == LayoutItemKind.BackCard);
            back.XPositionMm.Should().Be(109.4);
            back.YPositionMm.Should().Be(15.0);
            back.WidthMm.Should().Be(85.60);
            back.HeightMm.Should().Be(53.98);

            // Verify symmetry:
            // Left margin = 15.0 mm
            // Right margin = 210.0 - (109.4 + 85.60) = 210.0 - 195.0 = 15.0 mm
            double rightMargin = layout.PageWidthMm - (back.XPositionMm + back.WidthMm);
            rightMargin.Should().BeApproximately(15.0, 0.001);
        }

        [Fact]
        public void CenterFoldA4_LayoutGeometry_ShouldCenterCardsAndEnableFoldLine()
        {
            using var frontBmp = new SKBitmap(2022, 1275);
            using var backBmp = new SKBitmap(2022, 1275);

            var layout = _layoutEngine.CreateLayout(
                SheetLayoutMode.CenterFoldA4,
                frontBmp,
                backBmp,
                showCutMarks: true,
                showFoldLine: true);

            layout.ShowFoldLine.Should().BeTrue();
            layout.Items.Should().HaveCount(2);

            double expectedCenteredX = (210.0 - 85.60) / 2.0; // 62.20 mm
            var front = layout.Items.First(i => i.Kind == LayoutItemKind.FrontCard);
            front.XPositionMm.Should().BeApproximately(expectedCenteredX, 0.01);
            front.YPositionMm.Should().Be(15.0);

            var back = layout.Items.First(i => i.Kind == LayoutItemKind.BackCard);
            back.XPositionMm.Should().BeApproximately(expectedCenteredX, 0.01);
            back.YPositionMm.Should().Be(15.0 + 53.98);
            back.RotationDegrees.Should().Be(180.0); // Upside-down so folding aligns correctly
        }

        [Fact]
        public void ExportLayoutToPdf_ShouldPreserveMillimeterPoints()
        {
            using var frontBmp = new SKBitmap(2022, 1275);
            using var backBmp = new SKBitmap(2022, 1275);

            var layout = _layoutEngine.CreateLayout(
                SheetLayoutMode.SingleSideA4,
                frontBmp,
                backBmp);

            byte[] pdfBytes = _renderService.ExportLayoutToPdf(layout);
            pdfBytes.Should().NotBeNull();
            pdfBytes.Length.Should().BeGreaterThan(500);

            // PDF points for 210 x 297 mm
            double expectedPtsW = 210.0 * 72.0 / 25.4; // 595.276 pt
            double expectedPtsH = 297.0 * 72.0 / 25.4; // 841.890 pt

            expectedPtsW.Should().BeApproximately(595.28, 0.1);
            expectedPtsH.Should().BeApproximately(841.89, 0.1);
        }

        [Fact]
        public async Task PrintLifecycle_FullSixStageTransitionAndNonBillableReprint_ShouldBeAccurate()
        {
            using var frontBmp = new SKBitmap(2022, 1275);
            using var backBmp = new SKBitmap(2022, 1275);

            var layout = _layoutEngine.CreateLayout(SheetLayoutMode.SingleSideA4, frontBmp, backBmp);

            var spoolJob = new PrintSpoolJob
            {
                DocumentTitle = "Aadhaar Card Print",
                CustomerName = "Ravi Kumar"
            };

            // Stage 1 & 2 & 3: Initial Print Spooling
            var attempt1 = await _printService.ExecutePrintJobAsync(
                spoolJob,
                layout,
                printerName: "Virtual Tray Spooler",
                isReprint: false);

            attempt1.AttemptNumber.Should().Be(1);
            attempt1.IsBillable.Should().BeTrue();
            attempt1.Status.Should().Be(PrintJobLifecycleStatus.SpoolerProcessingCompleted);

            // Stage 4 / 6: Operator inspects card, detects a scuffed ribbon defect
            await _printService.RecordOperatorConfirmationAsync(
                attempt1,
                confirmedSuccessful: false,
                defectReason: "RibbonScuff");

            attempt1.Status.Should().Be(PrintJobLifecycleStatus.PhysicalOutputNotVerified);
            attempt1.DefectReason.Should().Be("RibbonScuff");

            // Operator initiates hardware reprint (Attempt 2)
            var attempt2 = await _printService.ExecutePrintJobAsync(
                spoolJob,
                layout,
                printerName: "Virtual Tray Spooler",
                isReprint: true,
                defectReason: "RibbonScuff");

            attempt2.AttemptNumber.Should().Be(2);
            attempt2.IsBillable.Should().BeFalse(); // Non-billable hardware reprint!
            attempt2.DefectReason.Should().Be("RibbonScuff");
            attempt2.Status.Should().Be(PrintJobLifecycleStatus.SpoolerProcessingCompleted);

            // Stage 5: Operator confirms physical card output is flawless
            await _printService.RecordOperatorConfirmationAsync(
                attempt2,
                confirmedSuccessful: true);

            attempt2.Status.Should().Be(PrintJobLifecycleStatus.PhysicalOutputConfirmedByOperator);
            spoolJob.HasSuccessfulAttempt.Should().BeTrue();

            // Total billable attempts across job should strictly be 1
            spoolJob.Attempts.Count(a => a.IsBillable).Should().Be(1);
            spoolJob.Attempts.Count.Should().Be(2);
        }
    }
}
