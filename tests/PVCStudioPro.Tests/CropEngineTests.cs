using System;
using FluentAssertions;
using SkiaSharp;
using Xunit;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.Tests
{
    public class CropEngineTests
    {
        private readonly ICropEngine _engine = new CropEngine();

        private static SKBitmap CreateTestBitmap(int width = 800, int height = 600, SKColor? color = null)
        {
            var bmp = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(color ?? SKColors.CornflowerBlue);
            return bmp;
        }

        #region Test 1: Sub-pixel Coordinate Mapping Roundtrip Accuracy

        [Fact]
        public void SubPixel_ImageToScreen_AndScreenToImage_RoundtripAccuracy()
        {
            var state = new CropInteractionState
            {
                ZoomFactor = 1.85,
                PanOffsetX = 45.5,
                PanOffsetY = -30.25,
                ViewportWidth = 1200,
                ViewportHeight = 900
            };

            double imgW = 2400;
            double imgH = 1600;

            double testImgX = 352.45;
            double testImgY = 879.12;

            // 1. Image -> Screen
            var (screenX, screenY) = _engine.ImageToScreen(testImgX, testImgY, state, imgW, imgH);

            // 2. Screen -> Image
            var (roundtripX, roundtripY) = _engine.ScreenToImage(screenX, screenY, state, imgW, imgH);

            // Assert sub-pixel precision within 0.001 px
            roundtripX.Should().BeApproximately(testImgX, 0.001);
            roundtripY.Should().BeApproximately(testImgY, 0.001);
        }

        #endregion

        #region Test 2: Aspect Ratio Preservation (CR80 Standard)

        [Fact]
        public void AspectRatioPreservation_CR80_PreservesExactISOStandards()
        {
            double cr80Ratio = _engine.GetTargetAspectRatioValue(CropAspectRatio.Cr80Card);
            cr80Ratio.Should().BeApproximately(85.60 / 53.98, 0.0001);

            var bounds = new CropBounds
            {
                X = 100,
                Y = 100,
                Width = 500,
                Height = 400, // Non-standard ratio
                IsAspectRatioLocked = true
            };

            var constrained = _engine.ConstrainToAspectRatio(bounds, CropAspectRatio.Cr80Card, maxW: 2000, maxH: 2000);

            double resultingRatio = constrained.Width / constrained.Height;
            resultingRatio.Should().BeApproximately(cr80Ratio, 0.001);
        }

        #endregion

        #region Test 3: Millimeter To Pixel Roundtrip Accuracy At Multiple DPIs

        [Theory]
        [InlineData(150)]
        [InlineData(300)]
        [InlineData(600)]
        [InlineData(1200)]
        public void MillimeterToPixel_RoundtripAccuracy_AtMultipleDpis(int dpi)
        {
            double targetWmm = 85.60;
            double targetHmm = 53.98;

            var (pxW, pxH) = _engine.MillimetersToPixels(targetWmm, targetHmm, dpi);
            pxW.Should().BeGreaterThan(0);
            pxH.Should().BeGreaterThan(0);

            var (calculatedWmm, calculatedHmm) = _engine.PixelsToMillimeters(pxW, pxH, dpi);
            calculatedWmm.Should().BeApproximately(targetWmm, 0.1);
            calculatedHmm.Should().BeApproximately(targetHmm, 0.1);
        }

        #endregion

        #region Test 4: Crop Generates DerivedImageAsset With Exact Bounds And Lineage

        [Fact]
        public void Crop_GeneratesDerivedImageAsset_WithExactBoundsAndLineage()
        {
            using var rawBmp = CreateTestBitmap(1000, 800, SKColors.DarkSlateBlue);
            using var sourceAsset = SourceImageAsset.Create("test_source.png", rawBmp, 300);

            var bounds = new CropBounds
            {
                X = 100,
                Y = 80,
                Width = 508,
                Height = 320,
                Dpi = 300,
                RotationDegrees = 0
            };

            using var derived = _engine.Crop(sourceAsset, bounds, "Crop CR80 Front");

            derived.Should().NotBeNull();
            derived.ParentAssetId.Should().Be(sourceAsset.AssetId);
            derived.TransformDescription.Should().Be("Crop CR80 Front");
            derived.Width.Should().Be(508);
            derived.Height.Should().Be(320);
            derived.Dpi.Should().Be(300);

            // Verify surface operations on derived asset
            using var lease = derived.AcquireLease();
            lease.Surface.Width.Should().Be(508);
            lease.Surface.Height.Should().Be(320);
        }

        #endregion

        #region Test 5: Perspective Quad Warp Accurately Rectifies Distorted Quad

        [Fact]
        public void PerspectiveQuadWarp_AccuratelyRectifiesDistortedQuad()
        {
            using var rawBmp = CreateTestBitmap(1200, 1000, SKColors.DarkGreen);
            using var sourceAsset = SourceImageAsset.Create("perspective_test.png", rawBmp, 300);

            var quad = new QuadrilateralPoints(
                new SKPoint(120, 80),
                new SKPoint(850, 110),
                new SKPoint(830, 580),
                new SKPoint(90, 540)
            );

            using var warped = _engine.CropPerspective(sourceAsset, quad, CropAspectRatio.Cr80Card, targetDpi: 600, "Perspective Warp");

            warped.Should().NotBeNull();
            warped.Dpi.Should().Be(600);
            warped.Width.Should().Be(2022);
            warped.Height.Should().Be(1275);
            warped.IsCr80Standard.Should().BeTrue();
        }

        #endregion

        #region Test 6: Rotation Invariance And Aspect Preservation

        [Fact]
        public void Crop_With90DegreeRotation_ProducesExpectedOrientedDimensions()
        {
            using var rawBmp = CreateTestBitmap(1000, 600, SKColors.Crimson);
            using var sourceAsset = SourceImageAsset.Create("rotate_test.png", rawBmp, 300);

            var bounds = new CropBounds
            {
                X = 50,
                Y = 50,
                Width = 400,
                Height = 250,
                RotationDegrees = 90f,
                Dpi = 300
            };

            using var rotated = _engine.Crop(sourceAsset, bounds, "Rotated 90 Deg");

            rotated.Should().NotBeNull();
            // A 400x250 rect rotated 90 degrees becomes 250x400
            rotated.Width.Should().Be(250);
            rotated.Height.Should().Be(400);
        }

        #endregion
    }
}
