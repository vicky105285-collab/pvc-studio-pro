using System.Collections.Generic;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IPrinterDiscoveryService
    {
        Task<IReadOnlyList<PrinterCapabilities>> GetInstalledPrintersAsync();
        PrinterCapabilities QueryPrinterCapabilities(string printerName, string portName = "");
        bool ValidateMediaTrayMatch(PrinterCapabilities caps, string selectedTray);
    }
}
