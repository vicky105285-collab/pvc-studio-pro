using System;
using System.Threading.Tasks;
using SkiaSharp;

namespace PVCStudioPro.Core.Services
{
    public interface IVisionService
    {
        /// <summary>
        /// Automatically detects card boundary contours, corrects perspective skew, and crops to exact CR80 standard.
        /// </summary>
        Task<SKBitmap> DeskewAndCropCardAsync(SKBitmap sourceBitmap);

        /// <summary>
        /// Segments portrait subject using offline computer vision (GrabCut foreground-background segmentation)
        /// and composites onto a clean studio solid background. Original source image remains unmodified.
        /// </summary>
        Task<SKBitmap> RemoveBackgroundAsync(SKBitmap portraitBitmap, SKColor targetBgColor, int iterations = 5);

        /// <summary>
        /// Crops and frames portrait to official Indian Passport specification (35 x 45 mm) at 600 DPI (827 x 1063 px),
        /// with optional offline background removal.
        /// </summary>
        Task<SKBitmap> ProcessPassportPhotoAsync(SKBitmap photoBitmap, SKColor backgroundColor, bool removeOriginalBg = true);

        /// <summary>
        /// Composites passport photos (35mm x 45mm) onto 4x6" or A4 glossy paper with fine cutting guidelines.
        /// </summary>
        SKBitmap ComposePassportPhotoSheet(SKBitmap singlePhoto, int count = 8, int dpi = 600);
    }
}
