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

            using var result = await service.ProcessPassportPhotoAsync(portrait, SKColors.White);

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
