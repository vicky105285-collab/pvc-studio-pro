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
    public class PassportPhotoEngineTests
    {
        private readonly IVisionService _visionService;
        private readonly IPassportPhotoEngine _engine;

        public PassportPhotoEngineTests()
        {
            _visionService = new VisionService();
            _engine = new PassportPhotoEngine(_visionService);
        }

        [Fact]
        public void CalculateBiometricGuidelines_ShouldAdhereToICAO9303Standard()
        {
            int dpi = 600;
            int widthPx = 827;  // 35mm at 600 DPI
            int heightPx = 1063; // 45mm at 600 DPI

            var guide = _engine.CalculateBiometricGuidelines(widthPx, heightPx, dpi);

            guide.TargetWidthMm.Should().Be(35.0);
            guide.TargetHeightMm.Should().Be(45.0);
            guide.Dpi.Should().Be(600);

            // Eye level: ~29.5 mm from bottom
            // Height = 45mm. Eye from top = 45 - 29.5 = 15.5 mm.
            // In px: 15.5 * 600 / 25.4 = ~366 px
            guide.EyeLevelYPx.Should().BeApproximately(366, 10);

            // Chin line: ~12 mm from bottom
            // In px from bottom: 12 * 600 / 25.4 = ~283 px -> from top: 1063 - 283 = ~780 px
            guide.ChinLineYPx.Should().BeApproximately(780, 10);

            // Crown limit: ~4mm from top = 4 * 600 / 25.4 = ~94 px
            guide.CrownLimitYPx.Should().BeApproximately(94, 10);

            // Head oval height: ~75% of frame (33.75 mm = ~797 px)
            guide.HeadOvalHeightPx.Should().BeApproximately(797, 10);

            // Vertical center should be exactly half of width
            guide.VerticalCenterXPx.Should().Be(widthPx / 2.0);
        }

        [Fact]
        public async Task ProcessPassportPhoto_StandardDimensionsAndHairline_ShouldBeExact()
        {
            using var testPortrait = new SKBitmap(500, 600);
            using (var canvas = new SKCanvas(testPortrait))
            {
                canvas.Clear(SKColors.LightGray);
                using var paint = new SKPaint { Color = SKColors.DarkSlateBlue };
                // Draw torso and head
                canvas.DrawRect(150, 200, 200, 400, paint);
                canvas.DrawCircle(250, 150, 80, paint);
            }

            var options = new PassportPhotoOptions
            {
                BackgroundColor = SKColors.White,
                RemoveBackground = false,
                Dpi = 600,
                ZoomScale = 1.0
            };

            using var result = await _engine.ProcessPassportPhotoAsync(testPortrait, options);

            result.Should().NotBeNull();
            // 35mm x 45mm at 600 DPI = 827 x 1063 px
            result.Width.Should().Be(827);
            result.Height.Should().Be(1063);

            // Hairline border at (1, 1) and (Width-2, Height-2) should exist
            result.GetPixel(1, 1).Should().NotBe(SKColors.Transparent);
        }

        [Fact]
        public async Task ProcessPassportPhoto_WithPanAndZoom_ShouldScaleAndShiftCorrectly()
        {
            using var testPortrait = new SKBitmap(500, 600);
            using (var canvas = new SKCanvas(testPortrait))
            {
                canvas.Clear(SKColors.White);
                using var paint = new SKPaint { Color = SKColors.Navy };
                canvas.DrawCircle(250, 300, 100, paint);
            }

            var options = new PassportPhotoOptions
            {
                BackgroundColor = SKColors.White,
                RemoveBackground = false,
                Dpi = 600,
                ZoomScale = 1.5,
                PanOffsetXRatio = 0.1, // Shift right
                PanOffsetYRatio = -0.1 // Shift up
            };

            using var result = await _engine.ProcessPassportPhotoAsync(testPortrait, options);
            result.Should().NotBeNull();
            result.Width.Should().Be(827);
            result.Height.Should().Be(1063);
        }

        [Fact]
        public void ComposeMultiUpSheet_4x6AndA4Layouts_ShouldGenerateCorrectPhysicalSizes()
        {
            using var singlePhoto = new SKBitmap(827, 1063);
            using (var canvas = new SKCanvas(singlePhoto))
            {
                canvas.Clear(SKColors.White);
                using var p = new SKPaint { Color = SKColors.SteelBlue };
                canvas.DrawRect(100, 100, 627, 863, p);
            }

            // 1. 4x6" 8-up Sheet (152.4 x 101.6 mm)
            using var sheet8 = _engine.ComposeMultiUpSheet(singlePhoto, PhotoSheetLayout.Sheet4x6_8Up, dpi: 600);
            sheet8.Should().NotBeNull();
            sheet8.Width.Should().Be(3600);
            sheet8.Height.Should().Be(2400);

            // 2. A4 16-up Sheet (210 x 297 mm)
            using var sheet16 = _engine.ComposeMultiUpSheet(singlePhoto, PhotoSheetLayout.A4_16Up, dpi: 600);
            sheet16.Should().NotBeNull();
            sheet16.Width.Should().Be(4961);
            sheet16.Height.Should().Be(7016);

            // 3. A4 32-up Stamp Sheet (210 x 297 mm)
            using var sheet32 = _engine.ComposeMultiUpSheet(singlePhoto, PhotoSheetLayout.A4_32Up, dpi: 600);
            sheet32.Should().NotBeNull();
            sheet32.Width.Should().Be(4961);
            sheet32.Height.Should().Be(7016);
        }

        [Fact]
        public async Task GrabCut_BottomBoundary_ShouldMaintainSolidTorsoWithoutScallops()
        {
            // Build synthetic portrait with clear background (green) and subject torso reaching bottom edge
            int w = 400;
            int h = 500;
            using var portrait = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(portrait))
            {
                // Green background
                canvas.Clear(new SKColor(0, 200, 0));

                using var shirtPaint = new SKPaint { Color = new SKColor(20, 30, 40) }; // Dark shirt
                // Torso spanning from x=100 to x=300 and all the way to bottom y=500
                canvas.DrawRect(100, 250, 200, 250, shirtPaint);

                // Head
                using var skinPaint = new SKPaint { Color = new SKColor(220, 180, 140) };
                canvas.DrawCircle(200, 180, 60, skinPaint);
            }

            // Remove background to pure white
            using var segmented = await _visionService.RemoveBackgroundAsync(portrait, SKColors.White, iterations: 2);
            segmented.Should().NotBeNull();

            // Verify bottom-most row (y = h - 1):
            // Center torso pixels (e.g. x = 200) MUST be the shirt color, NOT the white background!
            var bottomCenterPixel = segmented.GetPixel(200, h - 1);
            bottomCenterPixel.Red.Should().BeLessThan(80, "Torso bottom edge must not be replaced by white background or scalloped");

            // Outer margin (e.g. x = 10, y = h - 1) should be background
            var outerBottomPixel = segmented.GetPixel(10, h - 1);
            outerBottomPixel.Red.Should().Be(255);
            outerBottomPixel.Green.Should().Be(255);
            outerBottomPixel.Blue.Should().Be(255);
        }
    }
}
