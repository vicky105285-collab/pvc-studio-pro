using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IPdfDocumentService : IPdfService
    {
        Task<PasswordUnlockResult> TryUnlockPasswordAsync(Stream pdfStream, string? password, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SKBitmap>> RenderAllPagesAsync(Stream pdfStream, int dpi = 300, string? password = null, CancellationToken cancellationToken = default);
    }
}
