using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OpenCvSharp;
using SkiaSharp;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class VisionService : IVisionService
    {
        private const double MmPerInch = 25.4;

        public Task<SKBitmap> DeskewAndCropCardAsync(SKBitmap sourceBitmap)
        {
            return Task.Run(() =>
            {
                using var srcMat = SkBitmapToMat(sourceBitmap);
                using var gray = new Mat();
                Cv2.CvtColor(srcMat, gray, ColorConversionCodes.BGRA2GRAY);

                using var blurred = new Mat();
                Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);

                using var edges = new Mat();
                Cv2.Canny(blurred, edges, 50, 150);

                Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                Point2f[]? bestQuad = null;
                double maxArea = (sourceBitmap.Width * sourceBitmap.Height) * 0.10; // At least 10% image area

                foreach (var c in contours)
                {
                    double area = Cv2.ContourArea(c);
                    if (area > maxArea)
                    {
                        double peri = Cv2.ArcLength(c, true);
                        var approx = Cv2.ApproxPolyDP(c, 0.02 * peri, true);
                        if (approx.Length == 4)
                        {
                            maxArea = area;
                            bestQuad = approx.Select(p => new Point2f(p.X, p.Y)).ToArray();
                        }
                    }
                }

                if (bestQuad == null)
                {
                    return CenterCropToCr80(sourceBitmap);
                }

                var orderedPts = OrderCornerPoints(bestQuad);

                // Standard CR80 target resolution: 2022 x 1275 px (600 DPI)
                int targetW = 2022;
                int targetH = 1275;

                var destPts = new Point2f[]
                {
                    new Point2f(0, 0),
                    new Point2f(targetW - 1, 0),
                    new Point2f(targetW - 1, targetH - 1),
                    new Point2f(0, targetH - 1)
                };

                using var transformMatrix = Cv2.GetPerspectiveTransform(orderedPts, destPts);
                using var warped = new Mat();
                Cv2.WarpPerspective(srcMat, warped, transformMatrix, new Size(targetW, targetH), InterpolationFlags.Cubic);

                return MatToSkBitmap(warped);
            });
        }

        public Task<SKBitmap> RemoveBackgroundAsync(SKBitmap portraitBitmap, SKColor targetBgColor, int iterations = 5)
        {
            return Task.Run(() =>
            {
                // 1. Convert to 3-channel 8-bit BGR Mat
                using var srcMat = SkBitmapToMat(portraitBitmap);
                using var bgr = new Mat();
                if (srcMat.Channels() == 4)
                {
                    Cv2.CvtColor(srcMat, bgr, ColorConversionCodes.BGRA2BGR);
                }
                else
                {
                    srcMat.CopyTo(bgr);
                }

                // 2. Define foreground ROI:
                // Elimination of 5% margin flaw: Portrait photos have torso/clothing extending down through the bottom edge.
                // Leaving a bottom margin cuts off the torso and creates wavy scallops.
                // The ROI rect must extend to the very bottom boundary (bgr.Height).
                int marginX = Math.Max(2, (int)(bgr.Width * 0.05));
                int marginY = Math.Max(2, (int)(bgr.Height * 0.05));
                int rectHeight = bgr.Height - marginY;
                var rect = new Rect(marginX, marginY, bgr.Width - (2 * marginX), rectHeight);

                // 3. Initialize GrabCut masks and models
                using var mask = new Mat(bgr.Size(), MatType.CV_8UC1, new Scalar((int)GrabCutClasses.BGD));
                using var bgdModel = new Mat();
                using var fgdModel = new Mat();

                // 4. Run GrabCut Segmentation (Iterative Energy Minimization via Graph Cut)
                Cv2.GrabCut(bgr, mask, rect, bgdModel, fgdModel, iterations, GrabCutModes.InitWithRect);

                // 5. Build foreground matte mask: pixels classified as FGD (1) or PR_FGD (3)
                using var fgMask = new Mat();
                using var mask1 = new Mat();
                using var mask3 = new Mat();
                Cv2.Compare(mask, (int)GrabCutClasses.FGD, mask1, CmpType.EQ);
                Cv2.Compare(mask, (int)GrabCutClasses.PR_FGD, mask3, CmpType.EQ);
                Cv2.BitwiseOr(mask1, mask3, fgMask);

                // Anchor bottom rows: The torso at the bottom edge must be straight solid foreground without scalloping.
                // Any PR_FGD or FGD in the lower boundary extending towards the bottom is anchored to a straight baseline.
                int anchorThresholdY = Math.Max(0, bgr.Height - 15);
                for (int x = marginX; x < bgr.Width - marginX; x++)
                {
                    bool hasTorsoNearBottom = false;
                    for (int y = anchorThresholdY; y < bgr.Height; y++)
                    {
                        if (fgMask.Get<byte>(y, x) == 255)
                        {
                            hasTorsoNearBottom = true;
                            break;
                        }
                    }

                    if (hasTorsoNearBottom)
                    {
                        for (int y = anchorThresholdY; y < bgr.Height; y++)
                        {
                            fgMask.Set<byte>(y, x, 255);
                        }
                    }
                }

                // Feather mask edges using Gaussian blur to blend hair and shoulders smoothly
                using var featheredMask = new Mat();
                Cv2.GaussianBlur(fgMask, featheredMask, new Size(3, 3), 0.5);

                // Ensure bottom boundary maintains 100% opacity for foreground columns (no fade out / transparency)
                for (int x = 0; x < bgr.Width; x++)
                {
                    if (fgMask.Get<byte>(bgr.Height - 1, x) == 255)
                    {
                        featheredMask.Set<byte>(bgr.Height - 1, x, 255);
                        if (bgr.Height > 1)
                        {
                            featheredMask.Set<byte>(bgr.Height - 2, x, 255);
                        }
                    }
                }

                // 6. Create solid studio background canvas
                var segmented = new SKBitmap(portraitBitmap.Width, portraitBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var canvas = new SKCanvas(segmented);
                canvas.Clear(targetBgColor);

                // 7. Composite foreground onto background using true alpha channel from featheredMask
                var bgrChannels = Cv2.Split(bgr);
                using var bgraMat = new Mat();
                Cv2.Merge(new[] { bgrChannels[0], bgrChannels[1], bgrChannels[2], featheredMask }, bgraMat);
                foreach (var ch in bgrChannels)
                {
                    ch.Dispose();
                }

                using var fgBitmap = MatToSkBitmap(bgraMat);
                canvas.DrawBitmap(fgBitmap, 0, 0, SKSamplingOptions.Default, null);
                canvas.Flush();
                return segmented;
            });
        }

        public async Task<SKBitmap> ProcessPassportPhotoAsync(SKBitmap photoBitmap, SKColor backgroundColor, bool removeOriginalBg = true)
        {
            SKBitmap workingPhoto = photoBitmap;
            SKBitmap? segmented = null;

            if (removeOriginalBg)
            {
                // Execute real offline GrabCut segmentation
                segmented = await RemoveBackgroundAsync(photoBitmap, backgroundColor);
                workingPhoto = segmented;
            }

            try
            {
                return await Task.Run(() =>
                {
                    // Standard 35mm x 45mm at 600 DPI = 827 x 1063 px
                    int targetW = 827;
                    int targetH = 1063;

                    var result = new SKBitmap(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using var canvas = new SKCanvas(result);
                    canvas.Clear(backgroundColor);

                    // Maintain aspect ratio, center portrait into 35x45mm frame
                    float scale = Math.Max((float)targetW / workingPhoto.Width, (float)targetH / workingPhoto.Height);
                    float drawW = workingPhoto.Width * scale;
                    float drawH = workingPhoto.Height * scale;
                    float drawX = (targetW - drawW) / 2f;
                    float drawY = (targetH - drawH) / 2f;

                    var destRect = new SKRect(drawX, drawY, drawX + drawW, drawY + drawH);
                    canvas.DrawBitmap(workingPhoto, destRect, new SKSamplingOptions(SKCubicResampler.Mitchell));

                    // Draw fine 0.2mm studio boundary hairline
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

        public SKBitmap ComposePassportPhotoSheet(SKBitmap singlePhoto, int count = 8, int dpi = 600)
        {
            double paperWidthMm;
            double paperHeightMm;
            double photoWidthMm = 35.0;
            double photoHeightMm = 45.0;
            int cols;
            int rows;

            if (count <= 8)
            {
                // Standard 4x6" (101.6 x 152.4 mm) photo paper landscape (4 x 2 grid)
                paperWidthMm = 152.4;
                paperHeightMm = 101.6;
                cols = 4;
                rows = 2;
            }
            else if (count <= 16)
            {
                // Standard A4 (210 x 297 mm) portrait (4 x 4 grid)
                paperWidthMm = 210.0;
                paperHeightMm = 297.0;
                cols = 4;
                rows = 4;
            }
            else
            {
                // A4 (210 x 297 mm) portrait 32-up stamp sheet (4 x 8 grid: 25 x 35 mm)
                paperWidthMm = 210.0;
                paperHeightMm = 297.0;
                photoWidthMm = 25.0;
                photoHeightMm = 35.0;
                cols = 4;
                rows = 8;
            }

            int sheetW = (int)Math.Round((paperWidthMm * dpi) / MmPerInch);
            int sheetH = (int)Math.Round((paperHeightMm * dpi) / MmPerInch);

            var sheet = new SKBitmap(sheetW, sheetH, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(sheet);
            canvas.Clear(SKColors.White);

            int photoW = (int)Math.Round((photoWidthMm * dpi) / MmPerInch);
            int photoH = (int)Math.Round((photoHeightMm * dpi) / MmPerInch);

            int marginX = Math.Max(10, (sheetW - (cols * photoW)) / (cols + 1));
            int marginY = Math.Max(10, (sheetH - (rows * photoH)) / (rows + 1));

            using var cutLinePaint = new SKPaint
            {
                Color = new SKColor(180, 180, 180),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                PathEffect = SKPathEffect.CreateDash(new float[] { 12, 8 }, 0)
            };

            int placed = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (placed >= count) break;

                    int x = marginX + c * (photoW + marginX);
                    int y = marginY + r * (photoH + marginY);

                    var destRect = new SKRect(x, y, x + photoW, y + photoH);
                    canvas.DrawBitmap(singlePhoto, destRect, new SKSamplingOptions(SKCubicResampler.Mitchell));

                    // Cut guide corners
                    canvas.DrawRect(new SKRect(x - 2, y - 2, x + photoW + 2, y + photoH + 2), cutLinePaint);
                    placed++;
                }
            }

            canvas.Flush();
            return sheet;
        }

        private static Point2f[] OrderCornerPoints(Point2f[] pts)
        {
            var ordered = new Point2f[4];
            var sumSorted = pts.OrderBy(p => p.X + p.Y).ToArray();
            ordered[0] = sumSorted[0];  // TL
            ordered[2] = sumSorted[3];  // BR

            var diffSorted = pts.OrderBy(p => p.Y - p.X).ToArray();
            ordered[1] = diffSorted[0]; // TR
            ordered[3] = diffSorted[3]; // BL

            return ordered;
        }

        private static SKBitmap CenterCropToCr80(SKBitmap source)
        {
            const double cr80Ratio = 85.60 / 53.98;
            int cropW = source.Width;
            int cropH = (int)Math.Round(cropW / cr80Ratio);

            if (cropH > source.Height)
            {
                cropH = source.Height;
                cropW = (int)Math.Round(cropH * cr80Ratio);
            }

            int cropX = (source.Width - cropW) / 2;
            int cropY = (source.Height - cropH) / 2;

            var cropRect = new SKRectI(cropX, cropY, cropX + cropW, cropY + cropH);
            using var cropped = new SKBitmap();
            source.ExtractSubset(cropped, cropRect);

            var finalCard = new SKBitmap(2022, 1275, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(finalCard);
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(cropped, new SKRect(0, 0, 2022, 1275), new SKSamplingOptions(SKCubicResampler.Mitchell));
            canvas.Flush();
            return finalCard;
        }

        private static Mat SkBitmapToMat(SKBitmap bitmap)
        {
            return Mat.FromPixelData(bitmap.Height, bitmap.Width, MatType.CV_8UC4, bitmap.GetPixels(), bitmap.RowBytes);
        }

        private static SKBitmap MatToSkBitmap(Mat mat)
        {
            Mat bgra;
            if (mat.Type() == MatType.CV_8UC4)
            {
                bgra = mat;
            }
            else if (mat.Type() == MatType.CV_8UC3)
            {
                bgra = new Mat();
                Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGR2BGRA);
            }
            else
            {
                bgra = new Mat();
                Cv2.CvtColor(mat, bgra, ColorConversionCodes.GRAY2BGRA);
            }

            var result = new SKBitmap(bgra.Width, bgra.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            unsafe
            {
                Buffer.MemoryCopy((void*)bgra.Data, (void*)result.GetPixels(), result.ByteCount, result.ByteCount);
            }

            if (bgra != mat)
            {
                bgra.Dispose();
            }

            return result;
        }
    }
}
