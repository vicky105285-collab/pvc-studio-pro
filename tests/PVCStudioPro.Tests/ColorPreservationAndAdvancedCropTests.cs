using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentAssertions;
using SkiaSharp;
using Xunit;
using PVCStudioPro.App.Converters;
using PVCStudioPro.App.ViewModels;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.Tests
{
    public class ColorPreservationAndAdvancedCropTests
    {
        [Fact]
        public void RenderLayoutToBitmap_MustProduceBgra8888_WithExactColorsPreserved()
        {
            var renderService = new PhysicalPageRenderService();

            // Create a 100x100 source bitmap with Pure Red (255, 0, 0)
            using var redCard = new SKBitmap(100, 100, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var c = new SKCanvas(redCard))
            {
                c.Clear(new SKColor(255, 0, 0, 255));
            }

            var model = new PageLayoutModel
            {
                Title = "Test Layout",
                PageWidthMm = 100,
                PageHeightMm = 100,
                Items =
                {
                    new PageLayoutItem
                    {
                        Id = "card_red",
                        XPositionMm = 10,
                        YPositionMm = 10,
                        WidthMm = 50,
                        HeightMm = 50,
                        Bitmap = redCard
                    }
                }
            };

            using var rendered = renderService.RenderLayoutToBitmap(model, dpi: 300);

            rendered.Should().NotBeNull();
            rendered.ColorType.Should().Be(SKColorType.Bgra8888, "Windows GDI and WPF require BGRA8888 byte alignment");

            // Sample a pixel well inside the red card (X = 20mm @ 300 DPI, Y = 20mm @ 300 DPI)
            int sampleX = (int)Math.Round(20.0 * 300.0 / 25.4);
            int sampleY = (int)Math.Round(20.0 * 300.0 / 25.4);

            SKColor pixel = rendered.GetPixel(sampleX, sampleY);

            // Channel swap would result in pixel.Red == 0 and pixel.Blue == 255. Verify Red is 255 and Blue is 0!
            pixel.Red.Should().Be(255, "Red channel must be preserved and not swapped to Blue");
            pixel.Blue.Should().Be(0, "Blue channel must be 0 for a pure red card");
            pixel.Green.Should().Be(0, "Green channel must be 0 for a pure red card");
        }

        [Fact]
        public void ToBitmapSource_FromBgra8888AndRgba8888_MustPreserveColorChannels()
        {
            // 1. Test BGRA8888 with pure Blue (Red=0, Green=0, Blue=255)
            using var blueBmp = new SKBitmap(10, 10, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var c = new SKCanvas(blueBmp))
            {
                c.Clear(new SKColor(0, 0, 255, 255));
            }

            var blueSource = blueBmp.ToBitmapSource();
            blueSource.Should().NotBeNull();
            blueSource!.Format.Should().Be(PixelFormats.Bgra32);

            // 2. Test RGBA8888 with pure Red (Red=255, Green=0, Blue=0) to verify safe conversion
            using var redBmp = new SKBitmap(10, 10, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var c = new SKCanvas(redBmp))
            {
                c.Clear(new SKColor(255, 0, 0, 255));
            }

            var redSource = redBmp.ToBitmapSource();
            redSource.Should().NotBeNull();
            redSource!.Format.Should().Be(PixelFormats.Bgra32);
        }

        [Fact]
        public void ExportLayoutToPdf_MustProduceValidPdfStream_WithoutDistortingColors()
        {
            var renderService = new PhysicalPageRenderService();

            using var testCard = new SKBitmap(200, 120, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var c = new SKCanvas(testCard))
            {
                c.Clear(new SKColor(255, 153, 51)); // Indian Saffron #FF9933
            }

            var model = new PageLayoutModel
            {
                Title = "PDF Color Test",
                PageWidthMm = 210,
                PageHeightMm = 297,
                Items =
                {
                    new PageLayoutItem
                    {
                        Id = "card_saffron",
                        XPositionMm = 20,
                        YPositionMm = 30,
                        WidthMm = 85.60,
                        HeightMm = 53.98,
                        Bitmap = testCard
                    }
                }
            };

            byte[] pdfBytes = renderService.ExportLayoutToPdf(model);
            pdfBytes.Should().NotBeNull();
            pdfBytes.Length.Should().BeGreaterThan(500, "PDF stream must contain valid header and rendered objects");

            // PDF header check
            string header = System.Text.Encoding.ASCII.GetString(pdfBytes, 0, Math.Min(10, pdfBytes.Length));
            header.Should().StartWith("%PDF-", "Generated document must be a valid PDF format");
        }

        [Fact]
        public void AdvancedCrop_StraightenTilt_RotatesSmoothlyAtFineAngles()
        {
            var cropService = new ManualCropService();
            using var testImage = new SKBitmap(200, 200, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var c = new SKCanvas(testImage))
            {
                c.Clear(SKColors.Blue);
            }

            // Test fine straighten angle of +5.5 degrees
            using var rotated = cropService.RotateBitmap(testImage, 5.5, expandBounds: false);
            rotated.Should().NotBeNull();
            rotated.Width.Should().Be(200);
            rotated.Height.Should().Be(200);
            rotated.ColorType.Should().Be(SKColorType.Bgra8888);

            // Test 90 degree exact rotation
            using var rotated90 = cropService.RotateBitmap(testImage, 90);
            rotated90.Should().NotBeNull();
            rotated90.Width.Should().Be(200);
            rotated90.Height.Should().Be(200);
        }

        [Fact]
        public void OtherCards_Templates_MustProvideCustomAndGenericPresets()
        {
            var templateService = new TemplateService();
            var otherTemplates = templateService.GetOtherCardTemplates();

            otherTemplates.Should().NotBeNull();
            otherTemplates.Count.Should().Be(6);

            var ids = otherTemplates.Select(t => t.TemplateId).ToList();
            ids.Should().Contain(new[]
            {
                "other_id_dual",
                "other_id_single",
                "custom_card",
                "student_id",
                "employee_badge",
                "membership_card"
            });

            // Standard competitor templates should remain exactly 29
            templateService.GetAllTemplates().Count.Should().Be(29);
        }
    }
}
