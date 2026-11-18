using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class PrinterTrayService : IPrinterTrayService
    {
        private readonly IPrinterDiscoveryService _discoveryService;
        private readonly string _profilesFilePath;
        private readonly List<PrinterProfile> _profiles = new List<PrinterProfile>();
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;

        public PrinterTrayService(IPrinterDiscoveryService discoveryService, string? customProfilesPath = null)
        {
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            if (!string.IsNullOrWhiteSpace(customProfilesPath))
            {
                _profilesFilePath = customProfilesPath;
            }
            else
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string baseDir = Path.Combine(appData, "PVCStudioPro", "config");
                Directory.CreateDirectory(baseDir);
                _profilesFilePath = Path.Combine(baseDir, "printer_profiles.json");
            }
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await _lock.WaitAsync();
            try
            {
                if (File.Exists(_profilesFilePath))
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(_profilesFilePath);
                        var loaded = JsonSerializer.Deserialize<List<PrinterProfile>>(json);
                        if (loaded != null && loaded.Count > 0)
                        {
                            _profiles.Clear();
                            _profiles.AddRange(loaded);
                            _isInitialized = true;
                            return;
                        }
                    }
                    catch
                    {
                        // Fallback to default profiles if file is corrupted
                    }
                }

                // Generate baseline default profiles
                _profiles.Clear();
                _profiles.AddRange(CreateDefaultProfiles());
                await SaveInternalAsync();
                _isInitialized = true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IReadOnlyList<PrinterProfile>> GetAllProfilesAsync()
        {
            await InitializeAsync();
            await _lock.WaitAsync();
            try
            {
                return _profiles.ToList().AsReadOnly();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<PrinterProfile?> GetProfileByIdAsync(string profileId)
        {
            await InitializeAsync();
            await _lock.WaitAsync();
            try
            {
                return _profiles.FirstOrDefault(p => p.ProfileId == profileId);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IReadOnlyList<PrinterProfile>> GetProfilesForPrinterAsync(string printerDeviceName)
        {
            await InitializeAsync();
            await _lock.WaitAsync();
            try
            {
                return _profiles
                    .Where(p => string.IsNullOrEmpty(p.PrinterDeviceName) ||
                                p.PrinterDeviceName.Equals(printerDeviceName, StringComparison.OrdinalIgnoreCase))
                    .ToList()
                    .AsReadOnly();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SaveProfileAsync(PrinterProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            await InitializeAsync();

            await _lock.WaitAsync();
            try
            {
                profile.UpdatedAt = DateTime.UtcNow;
                _profiles.RemoveAll(p => p.ProfileId == profile.ProfileId);
                _profiles.Add(profile);

                if (profile.IsDefault)
                {
                    foreach (var other in _profiles.Where(p => p.ProfileId != profile.ProfileId && p.PrinterDeviceName == profile.PrinterDeviceName))
                    {
                        other.IsDefault = false;
                    }
                }

                await SaveInternalAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> DeleteProfileAsync(string profileId)
        {
            await InitializeAsync();
            await _lock.WaitAsync();
            try
            {
                int removed = _profiles.RemoveAll(p => p.ProfileId == profileId);
                if (removed > 0)
                {
                    await SaveInternalAsync();
                    return true;
                }
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<PrinterProfile> DuplicateProfileAsync(string sourceProfileId, string newProfileName)
        {
            await InitializeAsync();
            await _lock.WaitAsync();
            try
            {
                var source = _profiles.FirstOrDefault(p => p.ProfileId == sourceProfileId);
                if (source == null)
                    throw new ArgumentException($"Source profile '{sourceProfileId}' not found.");

                var duplicated = new PrinterProfile
                {
                    ProfileId = Guid.NewGuid().ToString(),
                    ProfileName = string.IsNullOrWhiteSpace(newProfileName) ? $"{source.ProfileName} (Copy)" : newProfileName,
                    PrinterDeviceName = source.PrinterDeviceName,
                    TrayType = source.TrayType,
                    AttachmentType = source.AttachmentType,
                    Description = source.Description,
                    OffsetXMm = source.OffsetXMm,
                    OffsetYMm = source.OffsetYMm,
                    CardGapMm = source.CardGapMm,
                    CardsPerTray = source.CardsPerTray,
                    TrayWidthMm = source.TrayWidthMm,
                    TrayHeightMm = source.TrayHeightMm,
                    TargetDpi = source.TargetDpi,
                    MirrorMode = source.MirrorMode,
                    IsDefault = false,
                    IsPhysicallyVerified = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _profiles.Add(duplicated);
                await SaveInternalAsync();
                return duplicated;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IReadOnlyList<string>> DiscoverSystemPrintersAsync()
        {
            var printers = await _discoveryService.GetInstalledPrintersAsync();
            return printers.Select(p => p.PrinterName).Distinct().ToList().AsReadOnly();
        }

        public async Task<PrinterProfile> CreateDefaultProfileForPrinterAsync(string printerName, string trayType, string attachmentType)
        {
            var profile = new PrinterProfile
            {
                ProfileName = $"{printerName} ({attachmentType})",
                PrinterDeviceName = printerName,
                TrayType = trayType,
                AttachmentType = attachmentType,
                TargetDpi = 600,
                IsPhysicallyVerified = false
            };

            await SaveProfileAsync(profile);
            return profile;
        }

        private async Task SaveInternalAsync()
        {
            string? dir = Path.GetDirectoryName(_profilesFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_profiles, options);
            await File.WriteAllTextAsync(_profilesFilePath, json);
        }

        private static List<PrinterProfile> CreateDefaultProfiles()
        {
            return new List<PrinterProfile>
            {
                new PrinterProfile
                {
                    ProfileId = "epson_l805_aftermarket_tray",
                    ProfileName = "Epson L805 (Aftermarket PVC 2-Card Tray)",
                    TrayType = "EPSON_L805_TRAY",
                    AttachmentType = "AftermarketTray",
                    Description = "Standard blue/black aftermarket PVC card tray for classic Epson L805 / R260 series.",
                    OffsetXMm = 0.0,
                    OffsetYMm = 18.0,
                    CardGapMm = 5.60,
                    CardsPerTray = 2,
                    TrayWidthMm = 140.0,
                    TrayHeightMm = 297.0,
                    TargetDpi = 600,
                    IsDefault = true,
                    IsPhysicallyVerified = false
                },
                new PrinterProfile
                {
                    ProfileId = "epson_l805_mfg_cd_attachment",
                    ProfileName = "Epson L805 (Printer-Supplied CD/DVD Attachment)",
                    TrayType = "EPSON_L805_TRAY",
                    AttachmentType = "ManufacturerAttachment",
                    Description = "Original manufacturer disc guide with PVC card insertion cutout.",
                    OffsetXMm = 0.0,
                    OffsetYMm = 20.0,
                    CardGapMm = 5.60,
                    CardsPerTray = 2,
                    TrayWidthMm = 140.0,
                    TrayHeightMm = 297.0,
                    TargetDpi = 600,
                    IsDefault = false,
                    IsPhysicallyVerified = false
                },
                new PrinterProfile
                {
                    ProfileId = "epson_l8050_aftermarket_tray",
                    ProfileName = "Epson EcoTank L8050 / L18050 (Aftermarket PVC Tray)",
                    TrayType = "EPSON_L8050_TRAY",
                    AttachmentType = "AftermarketTray",
                    Description = "Front-loading PVC card tray for 6-color EcoTank L8050 and A3+ L18050.",
                    OffsetXMm = 0.0,
                    OffsetYMm = 22.5,
                    CardGapMm = 5.60,
                    CardsPerTray = 2,
                    TrayWidthMm = 140.0,
                    TrayHeightMm = 297.0,
                    TargetDpi = 600,
                    IsDefault = false,
                    IsPhysicallyVerified = false
                },
                new PrinterProfile
                {
                    ProfileId = "canon_g_series_disc_tray",
                    ProfileName = "Canon Pixma G570 / G670 (Multi-Purpose Tray)",
                    TrayType = "CANON_G_SERIES_TRAY",
                    AttachmentType = "ManufacturerAttachment",
                    Description = "Canon multi-purpose disc tray for 6-color MegaTank photo printers.",
                    OffsetXMm = 0.0,
                    OffsetYMm = 12.0,
                    CardGapMm = 6.00,
                    CardsPerTray = 2,
                    TrayWidthMm = 130.0,
                    TrayHeightMm = 240.0,
                    TargetDpi = 600,
                    IsDefault = false,
                    IsPhysicallyVerified = false
                },
                new PrinterProfile
                {
                    ProfileId = "direct_cr80_card_printer",
                    ProfileName = "Direct CR80 PVC Card Printer (Fargo / Evolis / Zebra)",
                    TrayType = "DIRECT_CR80",
                    AttachmentType = "DirectCR80",
                    Description = "Edge-to-edge dedicated PVC thermal dye-sublimation printer.",
                    OffsetXMm = 0.0,
                    OffsetYMm = 0.0,
                    CardGapMm = 0.0,
                    CardsPerTray = 1,
                    TrayWidthMm = 85.60,
                    TrayHeightMm = 53.98,
                    TargetDpi = 600,
                    IsDefault = false,
                    IsPhysicallyVerified = false
                },
                new PrinterProfile
                {
                    ProfileId = "a4_dragon_sheet_mirror",
                    ProfileName = "A4 Thermal Lamination Mirror Sheet (Dragon Sheet)",
                    TrayType = "A4_DRAGON_SHEET",
                    AttachmentType = "CustomTray",
                    Description = "Standard A4 PVC sheet for thermal roll lamination in mirror mode.",
                    OffsetXMm = 0.0,
                    OffsetYMm = 0.0,
                    CardGapMm = 10.0,
                    CardsPerTray = 10,
                    TrayWidthMm = 210.0,
                    TrayHeightMm = 297.0,
                    TargetDpi = 600,
                    MirrorMode = true,
                    IsDefault = false,
                    IsPhysicallyVerified = false
                }
            };
        }
    }
}
