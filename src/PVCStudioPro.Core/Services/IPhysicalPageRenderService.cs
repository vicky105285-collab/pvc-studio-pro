using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IPhysicalPageRenderService
    {
        /// <summary>
        /// Renders the page layout model directly to an in-memory SKBitmap at specified DPI.
        /// Guaranteed millimeter-accurate layout.
        /// </summary>
        SKBitmap RenderLayoutToBitmap(PageLayoutModel model, int dpi = 300);

        /// <summary>
        /// Renders the layout onto an existing Skia canvas (e.g. for WPF drawing context or PDF document canvas).
        /// </summary>
        void RenderToPrintCanvas(PageLayoutModel model, SKCanvas canvas, int targetDpi);

        /// <summary>
        /// Exports the complete physical layout into a high-fidelity vector/raster PDF document.
        /// </summary>
        byte[] ExportLayoutToPdf(PageLayoutModel model);

        /// <summary>
        /// Serializes a PageLayoutModel into a portable JSON .pvcproj string including embedded image data.
        /// </summary>
        string SerializeProject(PageLayoutModel model);

        /// <summary>
        /// Deserializes a .pvcproj JSON string into a live PageLayoutModel with hydrated SKBitmaps.
        /// </summary>
        PageLayoutModel DeserializeProject(string json);

        /// <summary>
        /// Persists layout and items to a .pvcproj project file on disk.
        /// </summary>
        Task SaveProjectAsync(PageLayoutModel model, string filePath);

        /// <summary>
        /// Loads a .pvcproj project file from disk.
        /// </summary>
        Task<PageLayoutModel> LoadProjectAsync(string filePath);
    }
}
