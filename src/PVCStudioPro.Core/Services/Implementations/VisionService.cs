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
                    // Fallback to center CR80 crop if no distinct boundary contour is resolved
                    return CenterCropToCr80(sourceBitmap);
                }

                // Order corners: Top-Left, Top-Right, Bottom-Right, Bottom-Left
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

        public Task<SKBitmap> ProcessPassportPhotoAsync(SKBitmap photoBitmap, SKColor backgroundColor)
        {
            return Task.Run(() =>
            {
                // Standard 35mm x 45mm at 600 DPI = 827 x 1063 px
                int targetW = 827;
                int targetH = 1063;

                var result = new SKBitmap(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var canvas = new SKCanvas(result);
                canvas.Clear(backgroundColor);

                // Maintain aspect ratio, center crop portrait into 35x45mm frame
                float scale = Math.Max((float)targetW / photoBitmap.Width, (float)targetH / photoBitmap.Height);
                float drawW = photoBitmap.Width * scale;
                float drawH = photoBitmap.Height * scale;
                float drawX = (targetW - drawW) / 2f;
                float drawY = (targetH - drawH) / 2f;

                var destRect = new SKRect(drawX, drawY, drawX + drawW, drawY + drawH);
                canvas.DrawBitmap(photoBitmap, destRect, new SKSamplingOptions(SKCubicResampler.Mitchell));

                // Draw ultra-fine 0.2mm studio boundary hairline
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

        public SKBitmap ComposePassportPhotoSheet(SKBitmap singlePhoto, int count = 8, int dpi = 600)
        {
            // Standard 4x6 inch (102 x 152 mm) photo paper at 600 DPI = 2400 x 3600 px (or 3600 x 2400 landscape)
            int sheetW = (int)Math.Round((152.4 * dpi) / MmPerInch); // 3600 px
            int sheetH = (int)Math.Round((101.6 * dpi) / MmPerInch); // 2400 px

            var sheet = new SKBitmap(sheetW, sheetH, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(sheet);
            canvas.Clear(SKColors.White);

            int photoW = (int)Math.Round((35.0 * dpi) / MmPerInch); // 827 px
            int photoH = (int)Math.Round((45.0 * dpi) / MmPerInch); // 1063 px

            int cols = 4;
            int rows = 2; // 8 photos total (4 x 2 grid)
            if (count > 8)
            {
                cols = 4;
                rows = (int)Math.Ceiling(count / 4.0);
            }

            int marginX = (sheetW - (cols * photoW)) / (cols + 1);
            int marginY = (sheetH - (rows * photoH)) / (rows + 1);

            using var cutLinePaint = new SKPaint
            {
                Color = new SKColor(200, 200, 200),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                PathEffect = SKPathEffect.CreateDash(new float[] { 10, 10 }, 0)
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
            // Sum of coordinates: Top-Left has smallest sum, Bottom-Right has largest sum
            var sumSorted = pts.OrderBy(p => p.X + p.Y).ToArray();
            ordered[0] = sumSorted[0];  // TL
            ordered[2] = sumSorted[3];  // BR

            // Difference of coordinates: Top-Right has smallest (or largest diff)
            var diffSorted = pts.OrderBy(p => p.Y - p.X).ToArray();
            ordered[1] = diffSorted[0]; // TR
            ordered[3] = diffSorted[3]; // BL

            return ordered;
        }

        private static SKBitmap CenterCropToCr80(SKBitmap source)
        {
            const double cr80Ratio = 85.60 / 53.98; // ~1.5858
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

            var result = new SKBitmap(bgra.Width, bgra.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
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
