using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using SkiaSharp;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;
using Xunit;

namespace PVCStudioPro.Tests
{
    public class PrintServiceTests
    {
        [Fact]
        public void ComposeTrayCanvas_Epson2CardTray_ShouldProduceExpectedDimensions()
        {
            var service = new PrintService();
            var profile = new PrinterProfile
            {
                TrayType = "EPSON_2CARD_TRAY",
                TargetDpi = 600,
                OffsetXMm = 0.0,
                OffsetYMm = 0.0
            };

            using var card1 = new SKBitmap(2022, 1275, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var card2 = new SKBitmap(2022, 1275, SKColorType.Bgra8888, SKAlphaType.Premul);

            using var tray = service.ComposeTrayCanvas(card1, card2, profile, TrayLayoutMode.BothCards);

            tray.Should().NotBeNull();
            // 140mm width at 600 DPI = ~3307 px
            tray.Width.Should().BeInRange(3300, 3315);
            // 297mm height at 600 DPI = ~7016 px
            tray.Height.Should().BeInRange(7000, 7030);
        }

        [Fact]
        public void ComposeTrayCanvas_MirrorMode_ShouldProduceValidCanvas()
        {
            var service = new PrintService();
            var profile = new PrinterProfile
            {
                TrayType = "EPSON_2CARD_TRAY",
                TargetDpi = 600,
                MirrorMode = true
            };

            using var card1 = new SKBitmap(2022, 1275, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var tray = service.ComposeTrayCanvas(card1, null, profile, TrayLayoutMode.Card1Only);

            tray.Should().NotBeNull();
            tray.Width.Should().BeGreaterThan(3000);
        }

        [Fact]
        public async Task GenerateCalibrationSheetAndExportPreview_ShouldCreateValidPngFile()
        {
            var service = new PrintService();
            using var sheet = service.GenerateCalibrationSheet("EPSON_2CARD_TRAY", dpi: 300);

            sheet.Should().NotBeNull();
            sheet.Width.Should().BeInRange(1640, 1660); // 140mm at 300 DPI = ~1654 px

            string tempFile = Path.Combine(Path.GetTempPath(), $"calib_test_{System.Guid.NewGuid()}.png");
            try
            {
                string savedPath = await service.ExportPreviewAsync(sheet, tempFile);
                File.Exists(savedPath).Should().BeTrue();
                new FileInfo(savedPath).Length.Should().BeGreaterThan(1000);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }
    }
}
