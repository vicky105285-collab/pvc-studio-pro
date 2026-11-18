using System;
using System.Linq;
using System.Threading.Tasks;
using OpenCvSharp;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class CardDetectionService : ICardDetectionService
    {
        private const double MmPerInch = 25.4;

        public Task<(SKBitmap? Front, SKBitmap? Back, SKRectI? FrontRect, SKRectI? BackRect)> DetectAndExtractDualCardsAsync(
            SKBitmap pageBitmap,
            DocumentKind kind = DocumentKind.Aadhaar,
            AadhaarGeneration generation = AadhaarGeneration.Unknown)
        {
            return Task.Run<(SKBitmap? Front, SKBitmap? Back, SKRectI? FrontRect, SKRectI? BackRect)>(() =>
            {
                if (pageBitmap == null || pageBitmap.Width <= 0 || pageBitmap.Height <= 0)
                {
                    return (null, null, null, null);
                }

                // Assume standard A4 page aspect ratio to deduce DPI
                // A4 height is 297 mm
                double deducedDpi = (pageBitmap.Height * MmPerInch) / 297.0;
                if (deducedDpi < 72 || deducedDpi > 1200)
                {
                    deducedDpi = 300.0;
                }

                double cardWMm = 85.60;
                double cardHMm = 53.98;

                double frontXMm = 17.5;
                double frontYMm = 210.0;
                double backXMm = 108.0;
                double backYMm = 210.0;
                bool hasBack = true;

                switch (generation)
                {
                    case AadhaarGeneration.Pre2018:
                        frontXMm = 18.0;
                        frontYMm = 212.0;
                        backXMm = 108.0;
                        backYMm = 212.0;
                        break;
                    case AadhaarGeneration.Masked2018To2022:
                        frontXMm = 17.5;
                        frontYMm = 206.0;
                        backXMm = 108.5;
                        backYMm = 206.0;
                        break;
                    case AadhaarGeneration.Modern2022Plus:
                        frontXMm = 17.0;
                        frontYMm = 208.0;
                        backXMm = 108.0;
                        backYMm = 208.0;
                        break;
                    case AadhaarGeneration.PvcOrderLetter:
                        frontXMm = 62.2;
                        frontYMm = 95.0;
                        hasBack = false;
                        break;
                }

                // Calculate pixel bounds
                int fx = (int)Math.Round((frontXMm * deducedDpi) / MmPerInch);
                int fy = (int)Math.Round((frontYMm * deducedDpi) / MmPerInch);
                int cw = (int)Math.Round((cardWMm * deducedDpi) / MmPerInch);
                int ch = (int)Math.Round((cardHMm * deducedDpi) / MmPerInch);

                // Ensure within image bounds
                fx = Math.Clamp(fx, 0, Math.Max(0, pageBitmap.Width - cw));
                fy = Math.Clamp(fy, 0, Math.Max(0, pageBitmap.Height - ch));
                cw = Math.Min(cw, pageBitmap.Width - fx);
                ch = Math.Min(ch, pageBitmap.Height - fy);

                var frontRect = new SKRectI(fx, fy, fx + cw, fy + ch);
                var frontBitmap = new SKBitmap();
                pageBitmap.ExtractSubset(frontBitmap, frontRect);

                SKBitmap? backBitmap = null;
                SKRectI? backRect = null;

                if (hasBack)
                {
                    int bx = (int)Math.Round((backXMm * deducedDpi) / MmPerInch);
                    int by = (int)Math.Round((backYMm * deducedDpi) / MmPerInch);
                    bx = Math.Clamp(bx, 0, Math.Max(0, pageBitmap.Width - cw));
                    by = Math.Clamp(by, 0, Math.Max(0, pageBitmap.Height - ch));
                    int bw = Math.Min(cw, pageBitmap.Width - bx);
                    int bh = Math.Min(ch, pageBitmap.Height - by);

                    backRect = new SKRectI(bx, by, bx + bw, by + bh);
                    backBitmap = new SKBitmap();
                    pageBitmap.ExtractSubset(backBitmap, backRect.Value);
                }

                // Rescale to standard 600 DPI CR80 (2022 x 1275 px)
                int targetW = 2022;
                int targetH = 1275;

                var finalFront = new SKBitmap(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul);
                using (var canvas = new SKCanvas(finalFront))
                {
                    canvas.Clear(SKColors.White);
                    canvas.DrawBitmap(frontBitmap, new SKRect(0, 0, targetW, targetH), new SKSamplingOptions(SKCubicResampler.Mitchell));
                }
                frontBitmap.Dispose();

                SKBitmap? finalBack = null;
                if (backBitmap != null)
                {
                    finalBack = new SKBitmap(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using (var canvas = new SKCanvas(finalBack))
                    {
                        canvas.Clear(SKColors.White);
                        canvas.DrawBitmap(backBitmap, new SKRect(0, 0, targetW, targetH), new SKSamplingOptions(SKCubicResampler.Mitchell));
                    }
                    backBitmap.Dispose();
                }

                return (finalFront, finalBack, (SKRectI?)frontRect, backRect);
            });
        }

        public Task<SKBitmap> RectifyCardPerspectiveAsync(
            SKBitmap sourceBitmap,
            QuadrilateralPoints quad,
            int targetWidthPx = 2022,
            int targetHeightPx = 1275)
        {
            return Task.Run(() =>
            {
                using var srcMat = SkBitmapToMat(sourceBitmap);

                var srcPts = new Point2f[]
                {
                    new Point2f(quad.TopLeft.X, quad.TopLeft.Y),
                    new Point2f(quad.TopRight.X, quad.TopRight.Y),
                    new Point2f(quad.BottomRight.X, quad.BottomRight.Y),
                    new Point2f(quad.BottomLeft.X, quad.BottomLeft.Y)
                };

                var destPts = new Point2f[]
                {
                    new Point2f(0, 0),
                    new Point2f(targetWidthPx - 1, 0),
                    new Point2f(targetWidthPx - 1, targetHeightPx - 1),
                    new Point2f(0, targetHeightPx - 1)
                };

                using var transformMatrix = Cv2.GetPerspectiveTransform(srcPts, destPts);
                using var warped = new Mat();
                Cv2.WarpPerspective(srcMat, warped, transformMatrix, new Size(targetWidthPx, targetHeightPx), InterpolationFlags.Cubic);

                return MatToSkBitmap(warped);
            });
        }

        private static Mat SkBitmapToMat(SKBitmap bitmap)
        {
            var mat = new Mat(bitmap.Height, bitmap.Width, MatType.CV_8UC4);
            IntPtr srcPtr = bitmap.GetPixels();
            long totalBytes = (long)bitmap.Width * bitmap.Height * 4;
            unsafe
            {
                Buffer.MemoryCopy((void*)srcPtr, (void*)mat.Data, totalBytes, totalBytes);
            }
            return mat;
        }

        private static SKBitmap MatToSkBitmap(Mat mat)
        {
            var bitmap = new SKBitmap(mat.Width, mat.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            IntPtr destPtr = bitmap.GetPixels();
            long totalBytes = (long)mat.Width * mat.Height * 4;
            unsafe
            {
                Buffer.MemoryCopy((void*)mat.Data, (void*)destPtr, totalBytes, totalBytes);
            }
            return bitmap;
        }
    }
}
