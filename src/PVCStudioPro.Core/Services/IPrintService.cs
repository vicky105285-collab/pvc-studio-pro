using System;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IPrintService
    {
        /// <summary>
        /// Composites card faces onto the selected tray or page geometry.
        /// </summary>
        SKBitmap ComposeTrayCanvas(
            SKBitmap card1,
            SKBitmap? card2,
            PrinterProfile profile,
            TrayLayoutMode layoutMode = TrayLayoutMode.BothCards);

        /// <summary>
        /// Generates a calibration test sheet with millimeter rulers and registration crosshairs.
        /// </summary>
        SKBitmap GenerateCalibrationSheet(string trayType, int dpi = 600);

        /// <summary>
        /// Executes or spools the print job directly to the designated Windows printer queue.
        /// </summary>
        Task<PrintResult> PrintTrayAsync(
            SKBitmap trayBitmap,
            PrinterProfile profile,
            string documentTitle = "PVC Studio Pro Card Print");

        /// <summary>
        /// Exports the composed tray canvas to an image file or PDF for visual verification.
        /// </summary>
        Task<string> ExportPreviewAsync(SKBitmap trayBitmap, string outputPath);

        /// <summary>
        /// Executes a tracked print attempt through the 6-stage lifecycle.
        /// </summary>
        Task<PrintJobAttempt> ExecutePrintJobAsync(
            PrintSpoolJob job,
            PageLayoutModel layout,
            string printerName,
            bool isReprint = false,
            string? defectReason = null);

        /// <summary>
        /// Records operator physical output verification (Confirmed Successful or Defective Reprint Needed).
        /// </summary>
        Task RecordOperatorConfirmationAsync(
            PrintJobAttempt attempt,
            bool confirmedSuccessful,
            string? defectReason = null);
    }

    public enum TrayLayoutMode
    {
        BothCards,
        Card1Only,
        Card2Only
    }

    public class PrintResult
    {
        public bool Success { get; set; }
        public string? JobId { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
