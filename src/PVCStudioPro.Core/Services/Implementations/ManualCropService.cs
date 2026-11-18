using System;
using OpenCvSharp;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class ManualCropService : IManualCropService
    {
        private const double MmPerInch = 25.4;

        public SKBitmap CropPerspective(SKBitmap source, QuadrilateralPoints quad, CropAspectRatio ratio, int dpi = 600)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            int targetW;
            int targetH;

            switch (ratio)
            {
                case CropAspectRatio.PassportPhoto:
                    // 35 x 45 mm
                    targetW = (int)Math.Round((35.0 * dpi) / MmPerInch);
                    targetH = (int)Math.Round((45.0 * dpi) / MmPerInch);
                    break;
                case CropAspectRatio.StampPhoto:
                    // 25 x 30 mm
                    targetW = (int)Math.Round((25.0 * dpi) / MmPerInch);
                    targetH = (int)Math.Round((30.0 * dpi) / MmPerInch);
                    break;
                case CropAspectRatio.Freeform:
                    float topDist = MathF.Sqrt(MathF.Pow(quad.TopRight.X - quad.TopLeft.X, 2) + MathF.Pow(quad.TopRight.Y - quad.TopLeft.Y, 2));
                    float bottomDist = MathF.Sqrt(MathF.Pow(quad.BottomRight.X - quad.BottomLeft.X, 2) + MathF.Pow(quad.BottomRight.Y - quad.BottomLeft.Y, 2));
                    float leftDist = MathF.Sqrt(MathF.Pow(quad.BottomLeft.X - quad.TopLeft.X, 2) + MathF.Pow(quad.BottomLeft.Y - quad.TopLeft.Y, 2));
                    float rightDist = MathF.Sqrt(MathF.Pow(quad.BottomRight.X - quad.TopRight.X, 2) + MathF.Pow(quad.BottomRight.Y - quad.TopRight.Y, 2));
                    targetW = Math.Max(100, (int)MathF.Max(topDist, bottomDist));
                    targetH = Math.Max(100, (int)MathF.Max(leftDist, rightDist));
                    break;
                case CropAspectRatio.Cr80Card:
                default:
                    // 85.60 x 53.98 mm = 2022 x 1275 px at 600 DPI
                    targetW = (int)Math.Round((85.60 * dpi) / MmPerInch);
                    targetH = (int)Math.Round((53.98 * dpi) / MmPerInch);
                    break;
            }

            using var srcMat = SkBitmapToMat(source);
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
                new Point2f(targetW - 1, 0),
                new Point2f(targetW - 1, targetH - 1),
                new Point2f(0, targetH - 1)
            };

            using var transformMatrix = Cv2.GetPerspectiveTransform(srcPts, destPts);
            using var warped = new Mat();
            Cv2.WarpPerspective(srcMat, warped, transformMatrix, new Size(targetW, targetH), InterpolationFlags.Cubic);

            return MatToSkBitmap(warped);
        }

        public QuadrilateralPoints GetInitialCropBox(int imageWidth, int imageHeight, CropAspectRatio ratio)
        {
            float targetAspect;
            switch (ratio)
            {
                case CropAspectRatio.PassportPhoto:
                    targetAspect = 35.0f / 45.0f; // ~0.7778
                    break;
                case CropAspectRatio.StampPhoto:
                    targetAspect = 25.0f / 30.0f; // ~0.8333
                    break;
                case CropAspectRatio.Freeform:
                    targetAspect = 1.0f;
                    break;
                case CropAspectRatio.Cr80Card:
                default:
                    targetAspect = 85.60f / 53.98f; // ~1.5857
                    break;
            }

            // Target box to fill ~70% of the image
            float boxW = imageWidth * 0.70f;
            float boxH = boxW / targetAspect;

            if (boxH > imageHeight * 0.75f)
            {
                boxH = imageHeight * 0.75f;
                boxW = boxH * targetAspect;
            }

            float cx = imageWidth / 2f;
            float cy = imageHeight / 2f;

            float x1 = cx - (boxW / 2f);
            float y1 = cy - (boxH / 2f);
            float x2 = cx + (boxW / 2f);
            float y2 = cy + (boxH / 2f);

            return new QuadrilateralPoints(
                new SKPoint(x1, y1),
                new SKPoint(x2, y1),
                new SKPoint(x2, y2),
                new SKPoint(x1, y2)
            );
        }

        public SKBitmap RotateBitmap(SKBitmap source, int degrees)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            int normDeg = ((degrees % 360) + 360) % 360;
            if (normDeg == 0) return source.Copy();

            bool swap = normDeg == 90 || normDeg == 270;
            int newW = swap ? source.Height : source.Width;
            int newH = swap ? source.Width : source.Height;

            var rotated = new SKBitmap(newW, newH, source.ColorType, source.AlphaType);
            using var canvas = new SKCanvas(rotated);
            canvas.Clear(SKColors.Transparent);

            canvas.Translate(newW / 2f, newH / 2f);
            canvas.RotateDegrees(normDeg);
            canvas.Translate(-source.Width / 2f, -source.Height / 2f);
            canvas.DrawBitmap(source, 0, 0);

            return rotated;
        }

        public SKBitmap FlipHorizontal(SKBitmap source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var flipped = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
            using var canvas = new SKCanvas(flipped);
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(-1, 1, source.Width / 2f, 0);
            canvas.DrawBitmap(source, 0, 0);
            return flipped;
        }

        public SKBitmap ApplyAdjustments(SKBitmap source, float brightness, float contrast, float saturation, float sharpness)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // Fast path if all parameters are default
            if (Math.Abs(brightness) < 0.001f && Math.Abs(contrast - 1.0f) < 0.001f &&
                Math.Abs(saturation - 1.0f) < 0.001f && sharpness < 0.001f)
            {
                return source.Copy();
            }

            using var srcMat = SkBitmapToMat(source);
            using var adjustedMat = new Mat();

            // 1. Brightness & Contrast (Convert to BGR for OpenCV color ops)
            using var bgrMat = new Mat();
            Cv2.CvtColor(srcMat, bgrMat, ColorConversionCodes.BGRA2BGR);

            // Contrast (alpha) and Brightness (beta)
            bgrMat.ConvertTo(adjustedMat, -1, alpha: contrast, beta: brightness);

            // 2. Saturation
            if (Math.Abs(saturation - 1.0f) > 0.001f)
            {
                using var hsvMat = new Mat();
                Cv2.CvtColor(adjustedMat, hsvMat, ColorConversionCodes.BGR2HSV);
                Mat[] hsvChannels = Cv2.Split(hsvMat);
                try
                {
                    hsvChannels[1].ConvertTo(hsvChannels[1], -1, alpha: saturation);
                    Cv2.Merge(hsvChannels, hsvMat);
                    Cv2.CvtColor(hsvMat, adjustedMat, ColorConversionCodes.HSV2BGR);
                }
                finally
                {
                    foreach (var ch in hsvChannels) ch.Dispose();
                }
            }

            // 3. Sharpness (Unsharp Mask)
            if (sharpness > 0.01f)
            {
                using var blurred = new Mat();
                Cv2.GaussianBlur(adjustedMat, blurred, new Size(0, 0), sigmaX: 3.0);
                Cv2.AddWeighted(adjustedMat, 1.0 + sharpness, blurred, -sharpness, 0, adjustedMat);
            }

            // Convert back to BGRA
            using var finalBgra = new Mat();
            Cv2.CvtColor(adjustedMat, finalBgra, ColorConversionCodes.BGR2BGRA);

            return MatToSkBitmap(finalBgra);
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
