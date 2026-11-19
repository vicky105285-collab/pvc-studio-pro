using System.Threading.Tasks;
using FluentAssertions;
using SkiaSharp;
using PVCStudioPro.Core.Services.Implementations;
using Xunit;

namespace PVCStudioPro.Tests
{
    public class VisionServiceTests
    {
        [Fact]
        public async Task DeskewAndCropCardAsync_ShouldReturnExactCr80Dimensions()
        {
            var service = new VisionService();

            // Create synthetic raw photographed document bitmap
            using var sample = new SKBitmap(3000, 2000, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(sample))
            {
                canvas.Clear(SKColors.LightGray);
                // Draw a high contrast card rectangle
                using var paint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
                canvas.DrawRect(new SKRect(300, 300, 2700, 1700), paint);
            }

            using var result = await service.DeskewAndCropCardAsync(sample);

            result.Should().NotBeNull();
            result.Width.Should().Be(2022);
            result.Height.Should().Be(1275);
        }

        [Fact]
        public async Task ProcessPassportPhotoAsync_ShouldReturnStandard35x45mmDimensions()
        {
            var service = new VisionService();

            using var portrait = new SKBitmap(1000, 1000, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(portrait))
            {
                canvas.Clear(SKColors.SandyBrown);
            }

            using var result = await service.ProcessPassportPhotoAsync(portrait, SKColors.White, removeOriginalBg: false);

            result.Should().NotBeNull();
            result.Width.Should().Be(827);   // 35mm at 600 DPI
            result.Height.Should().Be(1063); // 45mm at 600 DPI
        }

        [Fact]
        public async Task RemoveBackgroundAsync_GrabCut_ShouldReplaceBackgroundWithTargetColor()
        {
            var service = new VisionService();

            // Create a 200x200 image: White background with a dark blue circular foreground subject in center
            using var input = new SKBitmap(200, 200, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(input))
            {
                canvas.Clear(SKColors.White);
                using var paint = new SKPaint { Color = SKColors.Navy, Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawCircle(100, 100, 50, paint);
            }

            // Remove background and replace with Bright Red
            using var segmented = await service.RemoveBackgroundAsync(input, SKColors.Red);

            segmented.Should().NotBeNull();
            segmented.Width.Should().Be(200);
            segmented.Height.Should().Be(200);

            // Verify corner pixel (originally white background) has been replaced by Red
            SKColor cornerColor = segmented.GetPixel(5, 5);
            cornerColor.Red.Should().BeGreaterThan(200);
            cornerColor.Green.Should().BeLessThan(50);
            cornerColor.Blue.Should().BeLessThan(50);

            // Verify center pixel (foreground circle) remains primarily Navy / Blue
            SKColor centerColor = segmented.GetPixel(100, 100);
            centerColor.Blue.Should().BeGreaterThan(100);
            centerColor.Red.Should().BeLessThan(50);
        }

        [Fact]
        public async Task ProcessPassportPhotoAsync_WithGrabCutBgRemoval_ShouldGenerateValidPassportSize()
        {
            var service = new VisionService();

            // Create dual-tone image: Light background with dark center
            using var portrait = new SKBitmap(400, 400, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(portrait))
            {
                canvas.Clear(SKColors.LightGreen);
                using var paint = new SKPaint { Color = SKColors.DarkSlateGray, Style = SKPaintStyle.Fill };
                canvas.DrawRect(new SKRect(100, 50, 300, 350), paint);
            }

            using var result = await service.ProcessPassportPhotoAsync(portrait, SKColors.White, removeOriginalBg: true);

            result.Should().NotBeNull();
            result.Width.Should().Be(827);   // 35mm at 600 DPI
            result.Height.Should().Be(1063); // 45mm at 600 DPI
        }

        [Fact]
        public void ComposePassportPhotoSheet_ShouldProduce4x6Sheet()
        {
            var service = new VisionService();

            using var photo = new SKBitmap(827, 1063, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(photo))
            {
                canvas.Clear(SKColors.AliceBlue);
            }

            using var sheet = service.ComposePassportPhotoSheet(photo, count: 8, dpi: 600);

            sheet.Should().NotBeNull();
            sheet.Width.Should().Be(3600);  // 6 inches at 600 DPI
            sheet.Height.Should().Be(2400); // 4 inches at 600 DPI
        }
    }
}
