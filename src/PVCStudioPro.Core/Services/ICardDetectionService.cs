using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface ICardDetectionService
    {
        Task<(SKBitmap? Front, SKBitmap? Back, SKRectI? FrontRect, SKRectI? BackRect)> DetectAndExtractDualCardsAsync(
            SKBitmap pageBitmap,
            DocumentKind kind = DocumentKind.Aadhaar,
            AadhaarGeneration generation = AadhaarGeneration.Unknown);

        Task<SKBitmap> RectifyCardPerspectiveAsync(
            SKBitmap sourceBitmap,
            QuadrilateralPoints quad,
            int targetWidthPx = 2022,
            int targetHeightPx = 1275);
    }
}
