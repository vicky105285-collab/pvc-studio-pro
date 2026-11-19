using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IPrintPreviewService
    {
        Task<PrintPreviewRender> GeneratePreviewAsync(
            SKBitmap? frontCard,
            SKBitmap? backCard,
            PrintPreviewConfig config,
            CancellationToken cancellationToken = default);

        Task<PrintPreviewRender> GenerateDocumentPreviewAsync(
            IReadOnlyList<SKBitmap> pages,
            PrintPreviewConfig config,
            CancellationToken cancellationToken = default);
    }
}
