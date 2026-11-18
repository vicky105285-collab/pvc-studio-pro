using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IManualCropService
    {
        SKBitmap CropPerspective(SKBitmap source, QuadrilateralPoints quad, CropAspectRatio ratio, int dpi = 600);
        QuadrilateralPoints GetInitialCropBox(int imageWidth, int imageHeight, CropAspectRatio ratio);
        SKBitmap RotateBitmap(SKBitmap source, int degrees);
        SKBitmap FlipHorizontal(SKBitmap source);
    }
}
