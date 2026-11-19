using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;

namespace PVCStudioPro.App.ViewModels
{
    public partial class PrinterTrayManagerViewModel : ObservableObject
    {
        private readonly IPrinterTrayService _trayService;

        [ObservableProperty]
        private PrinterProfile? _selectedProfile;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        public ObservableCollection<PrinterProfile> Profiles { get; } = new ObservableCollection<PrinterProfile>();
        public ObservableCollection<string> DiscoveredPrinters { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AttachmentTypes { get; } = new ObservableCollection<string>
        {
            "AftermarketTray",
            "ManufacturerAttachment",
            "CustomTray",
            "DirectCR80"
        };

        public ObservableCollection<string> TrayTypes { get; } = new ObservableCollection<string>
        {
            "EPSON_L805_TRAY",
            "EPSON_L8050_TRAY",
            "CANON_G_SERIES_TRAY",
            "DIRECT_CR80",
            "A4_DRAGON_SHEET"
        };

        public ICommand RefreshCommand { get; }
        public ICommand NewProfileCommand { get; }
        public ICommand DuplicateProfileCommand { get; }
        public ICommand SaveProfileCommand { get; }
        public ICommand DeleteProfileCommand { get; }

        public PrinterTrayManagerViewModel(IPrinterTrayService trayService)
        {
            _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));

            RefreshCommand = new AsyncRelayCommand(LoadDataAsync);
            NewProfileCommand = new RelayCommand(CreateNewProfile);
            DuplicateProfileCommand = new AsyncRelayCommand(DuplicateSelectedProfileAsync);
            SaveProfileCommand = new AsyncRelayCommand(SaveSelectedProfileAsync);
            DeleteProfileCommand = new AsyncRelayCommand(DeleteSelectedProfileAsync);

            _ = LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            try
            {
                var profiles = await _trayService.GetAllProfilesAsync();
                Profiles.Clear();
                foreach (var p in profiles)
                {
                    Profiles.Add(p);
                }

                if (Profiles.Count > 0 && SelectedProfile == null)
                {
                    SelectedProfile = Profiles[0];
                }

                var printers = await _trayService.DiscoverSystemPrintersAsync();
                DiscoveredPrinters.Clear();
                foreach (var pr in printers)
                {
                    DiscoveredPrinters.Add(pr);
                }

                StatusMessage = $"Loaded {Profiles.Count} tray profiles and {DiscoveredPrinters.Count} installed printers.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        private void CreateNewProfile()
        {
            var newProf = new PrinterProfile
            {
                ProfileName = "Custom PVC Tray Profile",
                TrayType = "EPSON_L805_TRAY",
                AttachmentType = "CustomTray",
                Description = "Custom user-defined tray alignment profile.",
                CardsPerTray = 2,
                TrayWidthMm = 140.0,
                TrayHeightMm = 297.0,
                OffsetXMm = 0.0,
                OffsetYMm = 0.0,
                TargetDpi = 600,
                IsPhysicallyVerified = false
            };

            Profiles.Add(newProf);
            SelectedProfile = newProf;
            StatusMessage = "Created new profile template. Adjust parameters and click 'Save Profile Changes'.";
        }

        private async Task DuplicateSelectedProfileAsync()
        {
            if (SelectedProfile == null) return;

            try
            {
                var copy = await _trayService.DuplicateProfileAsync(SelectedProfile.ProfileId, $"{SelectedProfile.ProfileName} (Copy)");
                Profiles.Add(copy);
                SelectedProfile = copy;
                StatusMessage = $"Duplicated profile: {copy.ProfileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Duplicate failed: {ex.Message}";
            }
        }

        private async Task SaveSelectedProfileAsync()
        {
            if (SelectedProfile == null) return;

            try
            {
                await _trayService.SaveProfileAsync(SelectedProfile);
                StatusMessage = $"Saved profile '{SelectedProfile.ProfileName}' at {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save failed: {ex.Message}";
            }
        }

        private async Task DeleteSelectedProfileAsync()
        {
            if (SelectedProfile == null) return;

            try
            {
                string id = SelectedProfile.ProfileId;
                await _trayService.DeleteProfileAsync(id);
                Profiles.Remove(SelectedProfile);
                SelectedProfile = Profiles.FirstOrDefault();
                StatusMessage = "Profile deleted successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
            }
        }
    }
}
