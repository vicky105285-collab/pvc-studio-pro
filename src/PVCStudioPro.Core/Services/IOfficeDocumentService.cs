using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IOfficeDocumentService
    {
        Task<IReadOnlyList<SKBitmap>> RenderOfficeDocumentAsync(Stream stream, FileMagicKind kind, int dpi = 300, CancellationToken cancellationToken = default);
        Task<string> ExtractTextAsync(Stream stream, FileMagicKind kind, CancellationToken cancellationToken = default);
    }
}
