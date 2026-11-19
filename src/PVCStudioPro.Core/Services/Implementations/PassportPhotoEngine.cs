using System;
using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class PassportPhotoEngine : IPassportPhotoEngine
    {
        private readonly IVisionService _visionService;
        private const double MmPerInch = 25.4;

        public PassportPhotoEngine(IVisionService visionService)
        {
            _visionService = visionService ?? throw new ArgumentNullException(nameof(visionService));
        }

        public async Task<SKBitmap> ProcessPassportPhotoAsync(
            SKBitmap originalPhoto,
            PassportPhotoOptions options)
        {
            if (originalPhoto == null) throw new ArgumentNullException(nameof(originalPhoto));
            if (options == null) options = new PassportPhotoOptions();

            SKBitmap workingPhoto = originalPhoto;
            SKBitmap? segmented = null;

            if (options.RemoveBackground)
            {
                segmented = await _visionService.RemoveBackgroundAsync(originalPhoto, options.BackgroundColor);
                workingPhoto = segmented;
            }

            try
            {
                return await Task.Run(() =>
                {
                    int dpi = options.Dpi > 0 ? options.Dpi : 600;
                    // Standard ICAO 9303: 35mm x 45mm
                    int targetW = (int)Math.Round((35.0 * dpi) / MmPerInch); // 827 px at 600 DPI
                    int targetH = (int)Math.Round((45.0 * dpi) / MmPerInch); // 1063 px at 600 DPI

                    var result = new SKBitmap(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using var canvas = new SKCanvas(result);
                    canvas.Clear(options.BackgroundColor);

                    // Maintain aspect ratio, apply operator zoom and pan
                    float baseScale = Math.Max((float)targetW / workingPhoto.Width, (float)targetH / workingPhoto.Height);
                    float scale = baseScale * (float)Math.Clamp(options.ZoomScale, 0.2, 5.0);
                    float drawW = workingPhoto.Width * scale;
                    float drawH = workingPhoto.Height * scale;

                    float drawX = ((targetW - drawW) / 2f) + (float)(options.PanOffsetXRatio * targetW);
                    float drawY = ((targetH - drawH) / 2f) + (float)(options.PanOffsetYRatio * targetH);

                    var destRect = new SKRect(drawX, drawY, drawX + drawW, drawY + drawH);
                    canvas.DrawBitmap(workingPhoto, destRect, new SKSamplingOptions(SKCubicResampler.Mitchell));

                    // 0.2mm studio boundary hairline
                    using var borderPaint = new SKPaint
                    {
                        Color = new SKColor(220, 220, 220),
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 2,
                        IsAntialias = true
                    };
                    canvas.DrawRect(new SKRect(1, 1, targetW - 2, targetH - 2), borderPaint);

                    canvas.Flush();
                    return result;
                });
            }
            finally
            {
                segmented?.Dispose();
            }
        }

        public SKBitmap ComposeMultiUpSheet(
            SKBitmap singlePhoto,
            PhotoSheetLayout layout,
            int dpi = 600,
            bool showCutGuides = true)
        {
            if (singlePhoto == null) throw new ArgumentNullException(nameof(singlePhoto));

            int count = layout switch
            {
                PhotoSheetLayout.Sheet4x6_8Up => 8,
                PhotoSheetLayout.A4_16Up => 16,
                PhotoSheetLayout.A4_32Up => 32,
                _ => 8
            };

            return _visionService.ComposePassportPhotoSheet(singlePhoto, count, dpi);
        }

        public BiometricGuidelineModel CalculateBiometricGuidelines(int widthPx, int heightPx, double dpi = 600)
        {
            var model = new BiometricGuidelineModel
            {
                Dpi = (int)dpi,
                TargetWidthMm = 35.0,
                TargetHeightMm = 45.0
            };

            double pxPerMm = dpi / MmPerInch;

            // Crown limit (~4.0 mm from top margin)
            model.CrownLimitYPx = 4.0 * pxPerMm;

            // Eye level (~29.5 mm from bottom, or 15.5 mm from top)
            model.EyeLevelYPx = heightPx - (29.5 * pxPerMm);

            // Chin base line (~12.0 mm from bottom)
            model.ChinLineYPx = heightPx - (12.0 * pxPerMm);

            // Symmetry axis
            model.VerticalCenterXPx = widthPx / 2.0;

            // Head oval (height ~ 75% of frame: 33.75 mm; width ~ 60% of frame: 21.0 mm)
            model.HeadOvalWidthPx = 21.0 * pxPerMm;
            model.HeadOvalHeightPx = 33.75 * pxPerMm;
            model.HeadOvalLeftPx = (widthPx - model.HeadOvalWidthPx) / 2.0;
            model.HeadOvalTopPx = model.CrownLimitYPx;

            return model;
        }
    }
}
