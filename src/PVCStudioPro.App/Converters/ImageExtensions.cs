using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.App.Converters
{
    public static class ImageExtensions
    {
        public static BitmapSource? ToBitmapSource(this IReadOnlyRasterSurface? surface)
        {
            if (surface == null) return null;

            using var snapshot = surface.CreateImmutableSnapshot();
            using var skBitmap = SKBitmap.FromImage(snapshot);
            return skBitmap.ToBitmapSource();
        }

        public static BitmapSource? ToBitmapSource(this SKBitmap? skBitmap)
        {
            if (skBitmap == null) return null;

            SKBitmap bgraBitmap;
            bool mustDispose = false;

            if (skBitmap.ColorType != SKColorType.Bgra8888)
            {
                bgraBitmap = new SKBitmap(skBitmap.Width, skBitmap.Height, SKColorType.Bgra8888, skBitmap.AlphaType);
                using (var canvas = new SKCanvas(bgraBitmap))
                {
                    canvas.DrawBitmap(skBitmap, 0, 0, SKSamplingOptions.Default, null);
                    canvas.Flush();
                }
                mustDispose = true;
            }
            else
            {
                bgraBitmap = skBitmap;
            }

            try
            {
                var writeableBitmap = new WriteableBitmap(
                    bgraBitmap.Width,
                    bgraBitmap.Height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null);

                writeableBitmap.Lock();
                try
                {
                    unsafe
                    {
                        Buffer.MemoryCopy(
                            (void*)bgraBitmap.GetPixels(),
                            (void*)writeableBitmap.BackBuffer,
                            bgraBitmap.ByteCount,
                            bgraBitmap.ByteCount);
                    }
                    writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, bgraBitmap.Width, bgraBitmap.Height));
                }
                finally
                {
                    writeableBitmap.Unlock();
                }

                writeableBitmap.Freeze();
                return writeableBitmap;
            }
            finally
            {
                if (mustDispose)
                {
                    bgraBitmap.Dispose();
                }
            }
        }
    }
}
