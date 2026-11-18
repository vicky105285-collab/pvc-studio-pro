using System.Collections.Generic;
using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public class DocumentDetectionResult
    {
        public DocumentKind DetectedKind { get; set; } = DocumentKind.Unknown;
        public AadhaarGeneration AadhaarGen { get; set; } = AadhaarGeneration.Unknown;
        public string DisplayName { get; set; } = "Unknown Document";
        public double Confidence { get; set; }
        public List<string> MatchedTokens { get; set; } = new();
        public bool IsAadhaar => DetectedKind == DocumentKind.Aadhaar;
        public CardTemplate? MatchedTemplate { get; set; }
        public string DetectionNotes { get; set; } = string.Empty;
    }

    public interface IDocumentTypeDetectionService
    {
        Task<DocumentDetectionResult> DetectDocumentTypeAsync(SKBitmap? pageBitmap, string extractedText, double widthMm = 210.0, double heightMm = 297.0);
    }
}
