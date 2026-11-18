using System;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IPdfService
    {
        Task<PdfDocumentInfo> InspectDocumentAsync(Stream pdfStream, string? password = null);
        Task<SKBitmap> RenderPageAsync(Stream pdfStream, int pageIndex, int dpi = 600, string? password = null);
        Task<(SKBitmap Front, SKBitmap? Back)> ExtractCardFacesAsync(Stream pdfStream, CardTemplate template, string? password = null, bool applyMask = false);
    }

    public class PdfDocumentInfo
    {
        public int PageCount { get; set; }
        public bool IsEncrypted { get; set; }
        public double PageWidthMm { get; set; }
        public double PageHeightMm { get; set; }
        public string ExtractedTextSample { get; set; } = string.Empty;
    }
}
