using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;

namespace PVCStudioPro.App.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IBusinessProfileService _profileService;
        public Action<string>? ToggleLanguageAction { get; set; }
        public Action<string>? ToggleThemeAction { get; set; }

        [ObservableProperty]
        private string _selectedTheme = "System";

        [ObservableProperty]
        private string _selectedLanguage = "en";

        [ObservableProperty]
        private int _uiScale = 100;

        [ObservableProperty]
        private string _storagePath = string.Empty;

        [ObservableProperty]
        private string _appVersion = "2.0.0-RC (Windows 11 x64)";

        [ObservableProperty]
        private string _statusMessage = "Ready";

        public ObservableCollection<string> ThemeOptions { get; } = new ObservableCollection<string> { "System", "Light", "Dark" };
        public ObservableCollection<string> LanguageOptions { get; } = new ObservableCollection<string> { "English", "தமிழ் (Tamil)" };
        public ObservableCollection<int> ScaleOptions { get; } = new ObservableCollection<int> { 100, 125, 150 };

        public ICommand SaveSettingsCommand { get; }
        public ICommand SwitchThemeCommand { get; }
        public ICommand SwitchLanguageCommand { get; }

        public SettingsViewModel(IBusinessProfileService profileService, Action<string>? toggleLanguageAction = null, Action<string>? toggleThemeAction = null)
        {
            _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
            ToggleLanguageAction = toggleLanguageAction;
            ToggleThemeAction = toggleThemeAction;

            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            SwitchThemeCommand = new RelayCommand<string>(SwitchTheme);
            SwitchLanguageCommand = new RelayCommand<string>(SwitchLanguage);

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            StoragePath = Path.Combine(appData, "PVCStudioPro");

            _ = LoadCurrentSettingsAsync();
        }

        private async Task LoadCurrentSettingsAsync()
        {
            try
            {
                var prof = await _profileService.GetProfileAsync();
                SelectedTheme = prof.ThemePreference;
                SelectedLanguage = prof.LanguagePreference;
                UiScale = prof.UiScalePercent;
            }
            catch
            {
                // Fallback
            }
        }

        private void SwitchTheme(string? theme)
        {
            if (string.IsNullOrEmpty(theme)) return;
            SelectedTheme = theme;
            ToggleThemeAction?.Invoke(theme);
            StatusMessage = $"Switched theme to {theme}.";
        }

        private void SwitchLanguage(string? lang)
        {
            if (string.IsNullOrEmpty(lang)) return;
            string code = lang.Contains("தமிழ்") ? "ta" : "en";
            SelectedLanguage = code;
            ToggleLanguageAction?.Invoke(code);
            StatusMessage = $"Language switched to {lang}.";
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                var prof = await _profileService.GetProfileAsync();
                prof.ThemePreference = SelectedTheme;
                prof.LanguagePreference = SelectedLanguage;
                prof.UiScalePercent = UiScale;
                await _profileService.SaveProfileAsync(prof);
                StatusMessage = "Settings saved successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save settings: {ex.Message}";
            }
        }
    }
}
