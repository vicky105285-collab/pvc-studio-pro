using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace PVCStudioPro.App.Converters
{
    public static class ImageExtensions
    {
        public static BitmapSource? ToBitmapSource(this SKBitmap? skBitmap)
        {
            if (skBitmap == null) return null;

            var writeableBitmap = new WriteableBitmap(
                skBitmap.Width,
                skBitmap.Height,
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
                        (void*)skBitmap.GetPixels(),
                        (void*)writeableBitmap.BackBuffer,
                        skBitmap.ByteCount,
                        skBitmap.ByteCount);
                }
                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, skBitmap.Width, skBitmap.Height));
            }
            finally
            {
                writeableBitmap.Unlock();
            }

            writeableBitmap.Freeze();
            return writeableBitmap;
        }
    }
}
