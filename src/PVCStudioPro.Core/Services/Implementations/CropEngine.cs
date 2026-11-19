using System;
using OpenCvSharp;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public sealed class CropEngine : ICropEngine
    {
        private const double MmPerInch = 25.4;
        private readonly IManualCropService _manualCropService;

        public CropEngine(IManualCropService? manualCropService = null)
        {
            _manualCropService = manualCropService ?? new ManualCropService();
        }

        public double GetTargetAspectRatioValue(CropAspectRatio ratio) => ratio switch
        {
            CropAspectRatio.Cr80Card => 85.60 / 53.98, // ~1.58577
            CropAspectRatio.AadhaarFullPouch => 88.50 / 56.50, // ~1.56637
            CropAspectRatio.PassportPhoto => 35.0 / 45.0, // ~0.77778
            CropAspectRatio.StampPhoto => 25.0 / 30.0, // ~0.83333
            _ => 1.0
        };

        public (double MmWidth, double MmHeight) PixelsToMillimeters(int pixelW, int pixelH, int dpi)
        {
            int effectiveDpi = dpi > 0 ? dpi : 300;
            double wMm = pixelW * MmPerInch / effectiveDpi;
            double hMm = pixelH * MmPerInch / effectiveDpi;
            return (Math.Round(wMm, 2), Math.Round(hMm, 2));
        }

        public (int PixelWidth, int PixelHeight) MillimetersToPixels(double mmW, double mmH, int dpi)
        {
            int effectiveDpi = dpi > 0 ? dpi : 300;
            int pxW = (int)Math.Round(mmW * effectiveDpi / MmPerInch);
            int pxH = (int)Math.Round(mmH * effectiveDpi / MmPerInch);
            return (pxW, pxH);
        }

        public CropBounds ConstrainToAspectRatio(CropBounds bounds, CropAspectRatio ratio, double maxW, double maxH)
        {
            var result = bounds.Clone();
            if (ratio == CropAspectRatio.Freeform || !result.IsAspectRatioLocked)
            {
                result.X = Math.Clamp(result.X, 0, Math.Max(0, maxW - 10));
                result.Y = Math.Clamp(result.Y, 0, Math.Max(0, maxH - 10));
                result.Width = Math.Clamp(result.Width, 10, maxW - result.X);
                result.Height = Math.Clamp(result.Height, 10, maxH - result.Y);
                return result;
            }

            double targetRatio = GetTargetAspectRatioValue(ratio);
            double currentRatio = result.Width / Math.Max(1.0, result.Height);

            if (currentRatio > targetRatio)
            {
                // Adjust width to fit target ratio
                result.Width = result.Height * targetRatio;
            }
            else
            {
                // Adjust height to fit target ratio
                result.Height = result.Width / targetRatio;
            }

            // Clamp inside max image dimensions preserving ratio
            if (result.X + result.Width > maxW)
            {
                double scale = (maxW - result.X) / result.Width;
                result.Width *= scale;
                result.Height *= scale;
            }

            if (result.Y + result.Height > maxH)
            {
                double scale = (maxH - result.Y) / result.Height;
                result.Width *= scale;
                result.Height *= scale;
            }

            return result;
        }

        public (double ScreenX, double ScreenY) ImageToScreen(
            double imgX,
            double imgY,
            CropInteractionState state,
            double imgW,
            double imgH)
        {
            double fitScale = Math.Min(state.ViewportWidth / Math.Max(1.0, imgW), state.ViewportHeight / Math.Max(1.0, imgH));
            double totalScale = fitScale * Math.Clamp(state.ZoomFactor, 0.1, 5.0);

            double centerX = state.ViewportWidth / 2.0 + state.PanOffsetX;
            double centerY = state.ViewportHeight / 2.0 + state.PanOffsetY;

            double originX = centerX - (imgW * totalScale) / 2.0;
            double originY = centerY - (imgH * totalScale) / 2.0;

            double screenX = originX + imgX * totalScale;
            double screenY = originY + imgY * totalScale;

            return (screenX, screenY);
        }

        public (double ImageX, double ImageY) ScreenToImage(
            double screenX,
            double screenY,
            CropInteractionState state,
            double imgW,
            double imgH)
        {
            double fitScale = Math.Min(state.ViewportWidth / Math.Max(1.0, imgW), state.ViewportHeight / Math.Max(1.0, imgH));
            double totalScale = fitScale * Math.Clamp(state.ZoomFactor, 0.1, 5.0);

            double centerX = state.ViewportWidth / 2.0 + state.PanOffsetX;
            double centerY = state.ViewportHeight / 2.0 + state.PanOffsetY;

            double originX = centerX - (imgW * totalScale) / 2.0;
            double originY = centerY - (imgH * totalScale) / 2.0;

            double imgX = (screenX - originX) / totalScale;
            double imgY = (screenY - originY) / totalScale;

            return (imgX, imgY);
        }

        public DerivedImageAsset Crop(SourceImageAsset source, CropBounds bounds, string transformDescription)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(bounds);

            using var lease = source.AcquireLease();
            using var snapshot = lease.Surface.CreateImmutableSnapshot();
            using var fullBitmap = SKBitmap.FromImage(snapshot);

            int x = (int)Math.Clamp(Math.Round(bounds.X), 0, fullBitmap.Width - 1);
            int y = (int)Math.Clamp(Math.Round(bounds.Y), 0, fullBitmap.Height - 1);
            int w = (int)Math.Clamp(Math.Round(bounds.Width), 1, fullBitmap.Width - x);
            int h = (int)Math.Clamp(Math.Round(bounds.Height), 1, fullBitmap.Height - y);

            var subsetRect = new SKRectI(x, y, x + w, y + h);
            var croppedBitmap = new SKBitmap(w, h, fullBitmap.ColorType, fullBitmap.AlphaType);

            if (!fullBitmap.ExtractSubset(croppedBitmap, subsetRect))
            {
                using var canvas = new SKCanvas(croppedBitmap);
                canvas.DrawBitmap(fullBitmap, subsetRect, new SKRect(0, 0, w, h), SKSamplingOptions.Default, null);
            }

            // Apply rotation if required
            if (Math.Abs(bounds.RotationDegrees) > 0.001f)
            {
                var rotated = _manualCropService.RotateBitmap(croppedBitmap, (int)Math.Round(bounds.RotationDegrees));
                croppedBitmap.Dispose();
                croppedBitmap = rotated;
            }

            return DerivedImageAsset.Create(
                source.AssetId,
                revision: 1,
                dpi: bounds.Dpi,
                transformDescription: transformDescription,
                bitmap: croppedBitmap,
                parentLease: lease);
        }

        public DerivedImageAsset CropPerspective(
            SourceImageAsset source,
            QuadrilateralPoints quad,
            CropAspectRatio ratio,
            int targetDpi,
            string transformDescription)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(quad);

            using var lease = source.AcquireLease();
            using var snapshot = lease.Surface.CreateImmutableSnapshot();
            using var fullBitmap = SKBitmap.FromImage(snapshot);

            var warpedBitmap = _manualCropService.CropPerspective(fullBitmap, quad, ratio, targetDpi);

            return DerivedImageAsset.Create(
                source.AssetId,
                revision: 1,
                dpi: targetDpi,
                transformDescription: transformDescription,
                bitmap: warpedBitmap,
                parentLease: lease);
        }
    }
}
