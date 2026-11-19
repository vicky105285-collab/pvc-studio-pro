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

        #region Test 7: Aadhaar Full Pouch Preset Aspect Ratio & Distortion-Free Geometry

        [Fact]
        public void AspectRatio_AadhaarFullPouch_PreservesCalibratedDimensionsWithoutClipping()
        {
            double targetAspect = _engine.GetTargetAspectRatioValue(CropAspectRatio.AadhaarFullPouch);
            targetAspect.Should().BeApproximately(88.50 / 56.50, 0.0001);

            using var rawBmp = CreateTestBitmap(2500, 1800, SKColors.Navy);
            using var sourceAsset = SourceImageAsset.Create("aadhaar_pouch_test.png", rawBmp, 600);

            var quad = new QuadrilateralPoints(
                new SKPoint(100, 100),
                new SKPoint(1870, 100),
                new SKPoint(1870, 1230),
                new SKPoint(100, 1230)
            );

            using var warped = _engine.CropPerspective(sourceAsset, quad, CropAspectRatio.AadhaarFullPouch, targetDpi: 600, "Aadhaar Full Pouch Warping");

            warped.Should().NotBeNull();
            // 88.50 mm @ 600 DPI = 2091 px; 56.50 mm @ 600 DPI = 1335 px
            warped.Width.Should().Be(2091);
            warped.Height.Should().Be(1335);
        }

        [Fact]
        public void PhysicalPageLayoutEngine_CustomCardDimensions_PreservesExactMillimeterDimensions()
        {
            var layoutEngine = new PhysicalPageLayoutEngine();
            // Create a custom 88.50 x 56.50 mm bitmap at 600 DPI (2091 x 1335 px)
            using var customFront = CreateTestBitmap(2091, 1335, SKColors.DarkGreen);
            using var customBack = CreateTestBitmap(2091, 1335, SKColors.DarkSlateBlue);

            var model = layoutEngine.CreateLayout(
                SheetLayoutMode.SingleSideA4,
                customFront,
                customBack,
                showCutMarks: true,
                targetDpi: 600);

            model.Items.Should().HaveCount(2);
            var frontItem = model.Items[0];
            var backItem = model.Items[1];

            frontItem.WidthMm.Should().BeApproximately(88.5, 0.05);
            frontItem.HeightMm.Should().BeApproximately(56.5, 0.05);
            backItem.WidthMm.Should().BeApproximately(88.5, 0.05);
            backItem.HeightMm.Should().BeApproximately(56.5, 0.05);
        }

        #endregion
    }
}
