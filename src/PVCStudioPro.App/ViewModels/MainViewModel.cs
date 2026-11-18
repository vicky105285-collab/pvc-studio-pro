using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PVCStudioPro.App.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private string _currentLanguage = "en";

        [ObservableProperty]
        private string _languageButtonText = "தமிழ் (Tamil)";

        [ObservableProperty]
        private string _currentTheme = "Light";

        [ObservableProperty]
        private string _activeWorkspaceTitle = "Dashboard";

        // 14 Core Workspaces
        public DashboardViewModel DashboardVM { get; }
        public CardPrintViewModel CardPrintVM { get; }
        public PhotoStudioViewModel PhotoStudioVM { get; }
        public DocumentProcessingViewModel DocProcessingVM { get; }
        public BatchProcessingViewModel BatchVM { get; }
        public PrintCenterViewModel PrintCenterVM { get; }
        public PrinterTrayManagerViewModel PrinterTrayVM { get; }
        public CalibrationViewModel CalibrationVM { get; }
        public TemplateManagerViewModel TemplatesVM { get; }
        public BusinessProfileViewModel BusinessProfileVM { get; }
        public BusinessLedgerViewModel LedgerVM { get; }
        public ReportsAnalyticsViewModel ReportsVM { get; }
        public BackupRestoreViewModel BackupVM { get; }
        public SettingsViewModel SettingsVM { get; }

        public MainViewModel(
            DashboardViewModel dashboardVM,
            CardPrintViewModel cardPrintVM,
            PhotoStudioViewModel photoStudioVM,
            DocumentProcessingViewModel docProcessingVM,
            BatchProcessingViewModel batchVM,
            PrintCenterViewModel printCenterVM,
            PrinterTrayManagerViewModel printerTrayVM,
            CalibrationViewModel calibrationVM,
            TemplateManagerViewModel templatesVM,
            BusinessProfileViewModel businessProfileVM,
            BusinessLedgerViewModel ledgerVM,
            ReportsAnalyticsViewModel reportsVM,
            BackupRestoreViewModel backupVM,
            SettingsViewModel settingsVM)
        {
            DashboardVM = dashboardVM;
            CardPrintVM = cardPrintVM;
            PhotoStudioVM = photoStudioVM;
            DocProcessingVM = docProcessingVM;
            BatchVM = batchVM;
            PrintCenterVM = printCenterVM;
            PrinterTrayVM = printerTrayVM;
            CalibrationVM = calibrationVM;
            TemplatesVM = templatesVM;
            BusinessProfileVM = businessProfileVM;
            LedgerVM = ledgerVM;
            ReportsVM = reportsVM;
            BackupVM = backupVM;
            SettingsVM = settingsVM;

            CurrentView = DashboardVM;
            ActiveWorkspaceTitle = "Dashboard";
        }

        [RelayCommand]
        public void Navigate(string destination)
        {
            switch (destination)
            {
                case "Dashboard":
                    _ = DashboardVM.LoadDashboardDataAsync();
                    CurrentView = DashboardVM;
                    ActiveWorkspaceTitle = "Dashboard";
                    break;
                case "Cards":
                    CurrentView = CardPrintVM;
                    ActiveWorkspaceTitle = "Card Studio";
                    break;
                case "Photo":
                    CurrentView = PhotoStudioVM;
                    ActiveWorkspaceTitle = "Photo Studio";
                    break;
                case "DocProcessing":
                    CurrentView = DocProcessingVM;
                    ActiveWorkspaceTitle = "Document Processing";
                    break;
                case "Batch":
                    CurrentView = BatchVM;
                    ActiveWorkspaceTitle = "Batch Processing";
                    break;
                case "PrintCenter":
                    _ = PrintCenterVM.RefreshStatusAsync();
                    CurrentView = PrintCenterVM;
                    ActiveWorkspaceTitle = "Print Center";
                    break;
                case "PrinterTray":
                    _ = PrinterTrayVM.LoadDataAsync();
                    CurrentView = PrinterTrayVM;
                    ActiveWorkspaceTitle = "Printer & Tray Manager";
                    break;
                case "Calibration":
                    CurrentView = CalibrationVM;
                    ActiveWorkspaceTitle = "Calibration Studio";
                    break;
                case "Templates":
                    _ = TemplatesVM.LoadTemplatesAsync();
                    CurrentView = TemplatesVM;
                    ActiveWorkspaceTitle = "Template Manager";
                    break;
                case "BusinessProfile":
                    CurrentView = BusinessProfileVM;
                    ActiveWorkspaceTitle = "Business Profile";
                    break;
                case "Ledger":
                    _ = LedgerVM.LoadLedgerAsync();
                    CurrentView = LedgerVM;
                    ActiveWorkspaceTitle = "Transactions & Ledger";
                    break;
                case "Reports":
                    _ = ReportsVM.LoadAnalyticsAsync();
                    CurrentView = ReportsVM;
                    ActiveWorkspaceTitle = "Reports & Analytics";
                    break;
                case "Backup":
                    CurrentView = BackupVM;
                    ActiveWorkspaceTitle = "Backup & Restore";
                    break;
                case "Settings":
                    CurrentView = SettingsVM;
                    ActiveWorkspaceTitle = "Settings & Preferences";
                    break;
                default:
                    CurrentView = DashboardVM;
                    ActiveWorkspaceTitle = "Dashboard";
                    break;
            }
        }

        [RelayCommand]
        public void ToggleLanguage()
        {
            if (CurrentLanguage == "en")
            {
                CurrentLanguage = "ta";
                LanguageButtonText = "English";
                ApplyLanguageDictionary("Strings.ta.xaml");
            }
            else
            {
                CurrentLanguage = "en";
                LanguageButtonText = "தமிழ் (Tamil)";
                ApplyLanguageDictionary("Strings.en.xaml");
            }
        }

        [RelayCommand]
        public void ToggleTheme()
        {
            if (CurrentTheme == "Light")
            {
                SetTheme("Dark");
            }
            else
            {
                SetTheme("Light");
            }
        }

        public void SetTheme(string theme)
        {
            CurrentTheme = theme;
            string themeFile = theme == "Dark" ? "DarkTheme.xaml" : "LightTheme.xaml";
            var dictUri = new Uri($"pack://application:,,,/Themes/{themeFile}", UriKind.Absolute);
            var newDict = new ResourceDictionary { Source = dictUri };

            var appResources = Application.Current.Resources.MergedDictionaries;
            for (int i = appResources.Count - 1; i >= 0; i--)
            {
                if (appResources[i].Source != null && appResources[i].Source.OriginalString.Contains("/Themes/"))
                {
                    appResources.RemoveAt(i);
                }
            }
            appResources.Add(newDict);
        }

        private static void ApplyLanguageDictionary(string dictFile)
        {
            var dictUri = new Uri($"pack://application:,,,/I18n/{dictFile}", UriKind.Absolute);
            var newDict = new ResourceDictionary { Source = dictUri };

            var appResources = Application.Current.Resources.MergedDictionaries;
            for (int i = appResources.Count - 1; i >= 0; i--)
            {
                if (appResources[i].Source != null && appResources[i].Source.OriginalString.Contains("/I18n/"))
                {
                    appResources.RemoveAt(i);
                }
            }
            appResources.Add(newDict);
        }
    }
}
