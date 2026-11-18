using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;

namespace PVCStudioPro.App.ViewModels
{
    public partial class BusinessProfileViewModel : ObservableObject
    {
        private readonly IBusinessProfileService _profileService;
        private readonly IPrinterTrayService _trayService;

        [ObservableProperty]
        private BusinessProfile _profile = new BusinessProfile();

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _isSaved = true;

        public ObservableCollection<string> AvailablePrinters { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AvailablePaperSizes { get; } = new ObservableCollection<string> { "58mm", "80mm", "A4" };
        public ObservableCollection<string> AvailablePaymentModes { get; } = new ObservableCollection<string> { "CASH", "UPI", "CARD" };
        public ObservableCollection<string> AvailableThemes { get; } = new ObservableCollection<string> { "System", "Light", "Dark" };
        public ObservableCollection<string> AvailableLanguages { get; } = new ObservableCollection<string> { "English", "தமிழ் (Tamil)" };

        public ICommand SaveCommand { get; }
        public ICommand ResetDefaultsCommand { get; }

        public BusinessProfileViewModel(IBusinessProfileService profileService, IPrinterTrayService trayService)
        {
            _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
            _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));

            SaveCommand = new AsyncRelayCommand(SaveProfileAsync);
            ResetDefaultsCommand = new AsyncRelayCommand(ResetDefaultsAsync);

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                Profile = await _profileService.GetProfileAsync();
                var printers = await _trayService.DiscoverSystemPrintersAsync();
                AvailablePrinters.Clear();
                foreach (var p in printers)
                {
                    AvailablePrinters.Add(p);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading profile: {ex.Message}";
            }
        }

        private async Task SaveProfileAsync()
        {
            try
            {
                await _profileService.SaveProfileAsync(Profile);
                StatusMessage = "Business Profile saved successfully at " + DateTime.Now.ToString("HH:mm:ss");
                IsSaved = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save profile: {ex.Message}";
            }
        }

        private async Task ResetDefaultsAsync()
        {
            try
            {
                Profile = await _profileService.ResetToDefaultsAsync();
                StatusMessage = "Business Profile reset to default settings.";
                IsSaved = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to reset profile: {ex.Message}";
            }
        }
    }
}
