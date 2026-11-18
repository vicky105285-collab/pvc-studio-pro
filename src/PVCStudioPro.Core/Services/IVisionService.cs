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
        /// Replaces or cleans passport portrait background to studio solid white or blue.
        /// </summary>
        Task<SKBitmap> ProcessPassportPhotoAsync(SKBitmap photoBitmap, SKColor backgroundColor);

        /// <summary>
        /// Composites passport photos (35mm x 45mm) onto 4x6" or A4 glossy paper with cutting guidelines.
        /// </summary>
        SKBitmap ComposePassportPhotoSheet(SKBitmap singlePhoto, int count = 8, int dpi = 600);
    }
}
