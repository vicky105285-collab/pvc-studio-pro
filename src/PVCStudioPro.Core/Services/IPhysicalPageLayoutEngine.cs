using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IPhysicalPageLayoutEngine
    {
        PageLayoutModel CreateLayout(
            SheetLayoutMode mode,
            SKBitmap? frontCard,
            SKBitmap? backCard,
            bool showCutMarks = true,
            bool showFoldLine = false,
            bool mirrorMode = false,
            double offsetXMm = 0.0,
            double offsetYMm = 0.0,
            int targetDpi = 600);

        void UpdateLayoutOffsets(PageLayoutModel model, double offsetXMm, double offsetYMm);

        (float X, float Y) MillimetersToPixels(double xMm, double yMm, int dpi);
        (double X, double Y) PixelsToMillimeters(float xPx, float yPx, int dpi);
    }
}
