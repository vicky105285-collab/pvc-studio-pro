using System.Collections.Generic;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IPrinterTrayService
    {
        Task InitializeAsync();
        Task<IReadOnlyList<PrinterProfile>> GetAllProfilesAsync();
        Task<PrinterProfile?> GetProfileByIdAsync(string profileId);
        Task<IReadOnlyList<PrinterProfile>> GetProfilesForPrinterAsync(string printerDeviceName);
        Task SaveProfileAsync(PrinterProfile profile);
        Task<bool> DeleteProfileAsync(string profileId);
        Task<PrinterProfile> DuplicateProfileAsync(string sourceProfileId, string newProfileName);
        Task<IReadOnlyList<string>> DiscoverSystemPrintersAsync();
        Task<PrinterProfile> CreateDefaultProfileForPrinterAsync(string printerName, string trayType, string attachmentType);
    }
}
