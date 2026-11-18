using System.IO;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IDocumentImportService
    {
        DocumentSniffResult SniffFile(string filePath);
        DocumentSniffResult SniffStream(Stream stream, string? originalFileName = null);
        Task<bool> ValidateIntegrityAsync(string filePath);
    }
}
