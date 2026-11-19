using System;
using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IPassportPhotoEngine
    {
        /// <summary>
        /// Crops and frames portrait to ICAO 9303 specification (35 x 45 mm) at specified DPI,
        /// with pan/zoom positioning and GrabCut background removal with straight-edge bottom anchoring.
        /// </summary>
        Task<SKBitmap> ProcessPassportPhotoAsync(
            SKBitmap originalPhoto,
            PassportPhotoOptions options);

        /// <summary>
        /// Composites passport photos onto standard 4x6" (8-up) or A4 (16/32-up) paper with high-precision cut guides.
        /// </summary>
        SKBitmap ComposeMultiUpSheet(
            SKBitmap singlePhoto,
            PhotoSheetLayout layout,
            int dpi = 600,
            bool showCutGuides = true);

        /// <summary>
        /// Computes exact pixel guidelines matching ICAO 9303 standards for the given frame dimensions and DPI.
        /// </summary>
        BiometricGuidelineModel CalculateBiometricGuidelines(int widthPx, int heightPx, double dpi = 600);
    }
}
