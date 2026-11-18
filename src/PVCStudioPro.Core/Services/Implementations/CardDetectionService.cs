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

                double frontWMm = 88.48;
                double frontHMm = 56.30;
                double frontXMm = 17.78;
                double frontYMm = 219.71;

                double backWMm = 88.48;
                double backHMm = 56.30;
                double backXMm = 110.06;
                double backYMm = 219.71;
                bool hasBack = true;

                switch (kind)
                {
                    case DocumentKind.Aadhaar:
                        if (generation == AadhaarGeneration.PvcOrderLetter)
                        {
                            frontXMm = 62.2;
                            frontYMm = 95.0;
                            frontWMm = 85.60;
                            frontHMm = 53.98;
                            hasBack = false;
                        }
                        else
                        {
                            // Ground truth calibrated from Real.db (UIDAI e-Aadhaar 300 DPI: F(210,2595,1045,665), B(1300,2595,1045,665))
                            frontXMm = 17.78;
                            frontYMm = 219.71;
                            frontWMm = 88.48;
                            frontHMm = 56.30;

                            backXMm = 110.06;
                            backYMm = 219.71;
                            backWMm = 88.48;
                            backHMm = 56.30;
                            hasBack = true;
                        }
                        break;

                    case DocumentKind.Pan:
                        // Ground truth calibrated from Real.db PAN-NSDL (300 DPI: F(290,2714,970,605), B(1285,2714,975,605))
                        frontXMm = 24.55;
                        frontYMm = 229.79;
                        frontWMm = 82.13;
                        frontHMm = 51.22;

                        backXMm = 108.80;
                        backYMm = 229.79;
                        backWMm = 82.55;
                        backHMm = 51.22;
                        hasBack = true;
                        break;

                    case DocumentKind.VoterId:
                        frontXMm = 18.0;
                        frontYMm = 160.0;
                        frontWMm = 85.60;
                        frontHMm = 53.98;

                        backXMm = 108.0;
                        backYMm = 160.0;
                        backWMm = 85.60;
                        backHMm = 53.98;
                        hasBack = true;
                        break;

                    case DocumentKind.AyushmanBharat:
                        // Ground truth calibrated from Real.db AYUSHMAN-NEW (300 DPI: F(120,270,1040,665), B(1350,270,1040,665))
                        frontXMm = 10.16;
                        frontYMm = 22.86;
                        frontWMm = 88.05;
                        frontHMm = 56.30;

                        backXMm = 114.30;
                        backYMm = 22.86;
                        backWMm = 88.05;
                        backHMm = 56.30;
                        hasBack = true;
                        break;

                    case DocumentKind.EShram:
                        // Ground truth calibrated from Real.db E-SHRAM (300 DPI: F(480,415,950,605), B(480,1035,950,600))
                        frontXMm = 40.64;
                        frontYMm = 35.13;
                        frontWMm = 80.43;
                        frontHMm = 51.22;

                        backXMm = 40.64;
                        backYMm = 87.63;
                        backWMm = 80.43;
                        backHMm = 50.80;
                        hasBack = true;
                        break;

                    case DocumentKind.DrivingLicence:
                        frontXMm = 20.0;
                        frontYMm = 80.0;
                        frontWMm = 85.60;
                        frontHMm = 53.98;

                        backXMm = 110.0;
                        backYMm = 80.0;
                        backWMm = 85.60;
                        backHMm = 53.98;
                        hasBack = true;
                        break;

                    case DocumentKind.VehicleRc:
                        frontXMm = 20.0;
                        frontYMm = 90.0;
                        frontWMm = 85.60;
                        frontHMm = 53.98;

                        backXMm = 110.0;
                        backYMm = 90.0;
                        backWMm = 85.60;
                        backHMm = 53.98;
                        hasBack = true;
                        break;

                    case DocumentKind.Abha:
                        frontXMm = 17.0;
                        frontYMm = 75.0;
                        frontWMm = 85.60;
                        frontHMm = 53.98;

                        backXMm = 108.0;
                        backYMm = 75.0;
                        backWMm = 85.60;
                        backHMm = 53.98;
                        hasBack = true;
                        break;

                    case DocumentKind.TamilNaduRation:
                        frontXMm = 15.0;
                        frontYMm = 140.0;
                        frontWMm = 85.60;
                        frontHMm = 53.98;

                        backXMm = 105.0;
                        backYMm = 140.0;
                        backWMm = 85.60;
                        backHMm = 53.98;
                        hasBack = true;
                        break;

                    default:
                        // Generic fallback: standard Aadhaar UIDAI positions
                        frontXMm = 17.78;
                        frontYMm = 219.71;
                        frontWMm = 88.48;
                        frontHMm = 56.30;

                        backXMm = 110.06;
                        backYMm = 219.71;
                        backWMm = 88.48;
                        backHMm = 56.30;
                        hasBack = true;
                        break;
                }

                // Calculate pixel bounds
                int fx = (int)Math.Round((frontXMm * deducedDpi) / MmPerInch);
                int fy = (int)Math.Round((frontYMm * deducedDpi) / MmPerInch);
                int cw = (int)Math.Round((frontWMm * deducedDpi) / MmPerInch);
                int ch = (int)Math.Round((frontHMm * deducedDpi) / MmPerInch);

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
                    int bw = (int)Math.Round((backWMm * deducedDpi) / MmPerInch);
                    int bh = (int)Math.Round((backHMm * deducedDpi) / MmPerInch);

                    bx = Math.Clamp(bx, 0, Math.Max(0, pageBitmap.Width - bw));
                    by = Math.Clamp(by, 0, Math.Max(0, pageBitmap.Height - bh));
                    bw = Math.Min(bw, pageBitmap.Width - bx);
                    bh = Math.Min(bh, pageBitmap.Height - by);

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
