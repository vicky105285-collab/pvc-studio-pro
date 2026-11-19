using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        public void ToBitmapSource_MustReadWpfPixelsDirectly_AndVerifyExactChannelOrder()
        {
            // 1. Pure Blue (R=0, G=0, B=255, A=255) in BGRA8888
            using var blueBmp = new SKBitmap(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var c = new SKCanvas(blueBmp))
            {
                c.Clear(new SKColor(0, 0, 255, 255));
            }

            var blueSource = blueBmp.ToBitmapSource();
            blueSource.Should().NotBeNull();
            blueSource!.Format.Should().Be(PixelFormats.Bgra32);

            byte[] bluePixels = new byte[4 * 4 * 4];
            blueSource.CopyPixels(bluePixels, 4 * 4, 0);

            // In WPF PixelFormats.Bgra32: Byte 0 is Blue, Byte 1 is Green, Byte 2 is Red, Byte 3 is Alpha
            bluePixels[0].Should().Be(255, "Byte 0 in Bgra32 must be Blue (255)");
            bluePixels[1].Should().Be(0, "Byte 1 in Bgra32 must be Green (0)");
            bluePixels[2].Should().Be(0, "Byte 2 in Bgra32 must be Red (0)");
            bluePixels[3].Should().Be(255, "Byte 3 in Bgra32 must be Alpha (255)");

            // 2. Pure Red (R=255, G=0, B=0, A=255) passed as RGBA8888 to test conversion safety
            using var redBmp = new SKBitmap(4, 4, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var c = new SKCanvas(redBmp))
            {
                c.Clear(new SKColor(255, 0, 0, 255));
            }

            var redSource = redBmp.ToBitmapSource();
            redSource.Should().NotBeNull();
            redSource!.Format.Should().Be(PixelFormats.Bgra32);

            byte[] redPixels = new byte[4 * 4 * 4];
            redSource.CopyPixels(redPixels, 4 * 4, 0);

            // In WPF PixelFormats.Bgra32: Red is at byte index 2
            redPixels[0].Should().Be(0, "Byte 0 (Blue) must be 0 for pure red bitmap");
            redPixels[1].Should().Be(0, "Byte 1 (Green) must be 0 for pure red bitmap");
            redPixels[2].Should().Be(255, "Byte 2 (Red) must be 255 for pure red bitmap");
            redPixels[3].Should().Be(255, "Byte 3 (Alpha) must be 255");

            // 3. Indian Saffron (#FF9933: R=255, G=153, B=51, A=255)
            using var saffronBmp = new SKBitmap(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var c = new SKCanvas(saffronBmp))
            {
                c.Clear(new SKColor(255, 153, 51, 255));
            }

            var saffronSource = saffronBmp.ToBitmapSource();
            byte[] saffronPixels = new byte[4 * 4 * 4];
            saffronSource!.CopyPixels(saffronPixels, 4 * 4, 0);

            saffronPixels[0].Should().Be(51, "Byte 0 (Blue) must match 51");
            saffronPixels[1].Should().Be(153, "Byte 1 (Green) must match 153");
            saffronPixels[2].Should().Be(255, "Byte 2 (Red) must match 255");
            saffronPixels[3].Should().Be(255, "Byte 3 (Alpha) must match 255");
        }

        [Fact]
        public void ToBitmapSource_FromRasterSurface_HandlesNon96DpiAndAlpha()
        {
            // Create a high-DPI (300 DPI) surface with semi-transparency
            using var skBmp = new SKBitmap(10, 10, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var c = new SKCanvas(skBmp))
            {
                c.Clear(new SKColor(200, 100, 50, 220));
            }

            using var sourceAsset = SourceImageAsset.Create("test.png", skBmp, nativeDpi: 300);
            using var lease = sourceAsset.AcquireLease();

            var bitmapSource = lease.Surface.ToBitmapSource();
            bitmapSource.Should().NotBeNull();
            bitmapSource!.Format.Should().Be(PixelFormats.Bgra32);

            byte[] pixels = new byte[10 * 10 * 4];
            bitmapSource.CopyPixels(pixels, 10 * 4, 0);

            // Channel order must be preserved under lease snapshot conversion (premultiplied alpha: C * 220 / 255)
            pixels[0].Should().BeInRange(40, 50, "Blue channel premultiplied (50 * 220/255 = ~43)");
            pixels[1].Should().BeInRange(80, 95, "Green channel premultiplied (100 * 220/255 = ~86)");
            pixels[2].Should().BeInRange(165, 180, "Red channel premultiplied (200 * 220/255 = ~173)");
            pixels[3].Should().BeInRange(215, 225, "Alpha channel matches ~220");
        }

        [Fact]
        public async Task ExportLayoutToPdf_AndRenderBackToBitmap_MustPreserveColorsWithinTolerance()
        {
            var renderService = new PhysicalPageRenderService();
            var pdfService = new PdfService();

            // Create a 100x100 Red Card (255, 0, 0)
            using var redCard = new SKBitmap(100, 100, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var c = new SKCanvas(redCard))
            {
                c.Clear(new SKColor(255, 0, 0, 255));
            }

            // Create a 100x100 Saffron Card (255, 153, 51)
            using var saffronCard = new SKBitmap(100, 100, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var c = new SKCanvas(saffronCard))
            {
                c.Clear(new SKColor(255, 153, 51, 255));
            }

            var model = new PageLayoutModel
            {
                Title = "Roundtrip PDF Test",
                PageWidthMm = 210,
                PageHeightMm = 297,
                Items =
                {
                    new PageLayoutItem
                    {
                        Id = "card_red",
                        XPositionMm = 20,
                        YPositionMm = 20,
                        WidthMm = 50,
                        HeightMm = 50,
                        Bitmap = redCard
                    },
                    new PageLayoutItem
                    {
                        Id = "card_saffron",
                        XPositionMm = 80,
                        YPositionMm = 20,
                        WidthMm = 50,
                        HeightMm = 50,
                        Bitmap = saffronCard
                    }
                }
            };

            // 1. Export layout to PDF
            byte[] pdfBytes = renderService.ExportLayoutToPdf(model);
            pdfBytes.Should().NotBeNull();
            pdfBytes.Length.Should().BeGreaterThan(500);

            // 2. Render PDF back to bitmap using Docnet/PDFium engine
            using var pdfStream = new MemoryStream(pdfBytes);
            using var reRendered = await pdfService.RenderPageAsync(pdfStream, 0, dpi: 300);

            reRendered.Should().NotBeNull();
            reRendered.ColorType.Should().Be(SKColorType.Bgra8888);

            // 3. Sample pixel inside the red card (X = 35mm @ 300 DPI, Y = 35mm @ 300 DPI)
            int redSampleX = (int)Math.Round(35.0 * 300.0 / 25.4);
            int redSampleY = (int)Math.Round(35.0 * 300.0 / 25.4);
            var sampledRed = reRendered.GetPixel(redSampleX, redSampleY);

            // Allow ±10 tolerance for anti-aliasing / PDFium downsampling
            sampledRed.Red.Should().BeInRange(240, 255, "Red must be dominant in red card");
            sampledRed.Green.Should().BeInRange(0, 15, "Green must be near zero in red card");
            sampledRed.Blue.Should().BeInRange(0, 15, "Blue must be near zero in red card; channel not swapped");

            // 4. Sample pixel inside the saffron card (X = 95mm @ 300 DPI, Y = 35mm @ 300 DPI)
            int saffronSampleX = (int)Math.Round(95.0 * 300.0 / 25.4);
            int saffronSampleY = (int)Math.Round(35.0 * 300.0 / 25.4);
            var sampledSaffron = reRendered.GetPixel(saffronSampleX, saffronSampleY);

            sampledSaffron.Red.Should().BeInRange(240, 255, "Red channel preserved in saffron card");
            sampledSaffron.Green.Should().BeInRange(140, 165, "Green channel preserved in saffron card");
            sampledSaffron.Blue.Should().BeInRange(40, 65, "Blue channel preserved in saffron card");
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
        public void PhysicalPageLayoutEngine_CustomCardDimensions_PreservedWithoutStretching()
        {
            var layoutEngine = new PhysicalPageLayoutEngine();

            // Create a custom-sized card (70 x 100 mm @ 600 DPI = 1654 x 2362 px)
            int customW = (int)Math.Round(70.0 * 600.0 / 25.4);
            int customH = (int)Math.Round(100.0 * 600.0 / 25.4);

            using var customBitmap = new SKBitmap(customW, customH, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var c = new SKCanvas(customBitmap))
            {
                c.Clear(SKColors.DarkGreen);
            }

            var model = layoutEngine.CreateLayout(SheetLayoutMode.SingleSideA4, customBitmap, null, targetDpi: 600);

            model.Items.Should().HaveCount(1);
            var item = model.Items[0];

            item.WidthMm.Should().BeApproximately(70.0, 0.5, "Custom card width must not be stretched to CR80");
            item.HeightMm.Should().BeApproximately(100.0, 0.5, "Custom card height must not be squished to CR80");
        }

        [Fact]
        public void All29OriginalTemplates_AndSixPresets_AreSelectableAndHaveValidConfigurations()
        {
            var templateService = new TemplateService();

            // 1. Verify 29 original government templates
            var templates = templateService.GetAllTemplates();
            templates.Should().HaveCount(29);

            foreach (var t in templates)
            {
                t.TemplateId.Should().NotBeNullOrWhiteSpace();
                t.DisplayName.Should().NotBeNullOrWhiteSpace();
                t.Category.Should().NotBeNullOrWhiteSpace();
                t.Faces.Should().NotBeNull();
                t.Faces.Front.CropRectMm.Width.Should().BeGreaterThan(30.0);
                t.Faces.Front.CropRectMm.Height.Should().BeGreaterThan(20.0);
            }

            // 2. Verify 6 additional custom & institutional presets
            var otherTemplates = templateService.GetOtherCardTemplates();
            otherTemplates.Should().HaveCount(6);

            var expectedIds = new[]
            {
                "other_id_dual",
                "other_id_single",
                "custom_card",
                "student_id",
                "employee_badge",
                "membership_card"
            };

            var otherIds = otherTemplates.Select(t => t.TemplateId).ToList();
            otherIds.Should().BeEquivalentTo(expectedIds);

            foreach (var ot in otherTemplates)
            {
                ot.DisplayName.Should().NotBeNullOrWhiteSpace();
                ot.Category.Should().Be("CUSTOM_OTHER");
                ot.Faces.Front.Should().NotBeNull();
                ot.Faces.Front.CropRectMm.Width.Should().BeApproximately(85.60, 0.1);
                ot.Faces.Front.CropRectMm.Height.Should().BeApproximately(53.98, 0.1);
            }
        }
    }
}
