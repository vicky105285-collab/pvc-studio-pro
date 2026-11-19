using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.App.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public IDocumentSessionManager SessionManager { get; }
        private readonly IDatabasePathProvider? _pathProvider;

        public ObservableCollection<IDocumentSession> OpenSessions { get; } = new();

        [ObservableProperty]
        private bool _isTestMode;

        [ObservableProperty]
        private IDocumentSession? _selectedSession;

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

        // Production & Processing Workspaces
        public ProductionWorkspaceViewModel ProductionVM { get; }
        public DashboardViewModel DashboardVM { get; }
        public DocumentDeskViewModel DocumentDeskVM { get; }
        public CropStudioViewModel CropStudioVM { get; }
        public DocumentPageDesignerViewModel PageDesignerVM { get; }
        public PrintPreviewApprovalViewModel PrintPreviewApprovalVM { get; }
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
            ProductionWorkspaceViewModel productionVM,
            DashboardViewModel dashboardVM,
            DocumentDeskViewModel documentDeskVM,
            CropStudioViewModel cropStudioVM,
            DocumentPageDesignerViewModel pageDesignerVM,
            PrintPreviewApprovalViewModel printPreviewApprovalVM,
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
            SettingsViewModel settingsVM,
            IDocumentSessionManager? sessionManager = null,
            IDatabasePathProvider? pathProvider = null)
        {
            _pathProvider = pathProvider;
            if (_pathProvider != null)
            {
                IsTestMode = _pathProvider.IsTestMode;
                _pathProvider.TestModeChanged += (s, mode) => IsTestMode = mode;
            }

            SessionManager = sessionManager ?? new DocumentSessionManager();
            SessionManager.SessionChanged += OnSessionChanged;
            SyncSessions();

            ProductionVM = productionVM;
            DashboardVM = dashboardVM;
            DocumentDeskVM = documentDeskVM;
            CropStudioVM = cropStudioVM;
            PageDesignerVM = pageDesignerVM;
            PrintPreviewApprovalVM = printPreviewApprovalVM;
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

            // Wire cross-workspace callbacks
            DashboardVM.NavigateAction = dest => Navigate(dest);

            DocumentDeskVM.OpenInCropStudioAction = (bmp, title) =>
            {
                CropStudioVM.LoadImage(bmp, title);
                Navigate("CropStudio");
            };

            DocumentDeskVM.OpenInPrintPreviewAction = (front, back, kind) =>
            {
                _ = PrintPreviewApprovalVM.LoadCardsAsync(front, back, kind);
                Navigate("PrintPreviewApproval");
            };

            DocumentDeskVM.OpenInPageDesignerAction = (front, back, title) =>
            {
                PageDesignerVM.LoadCardsIntoDesigner(front, back, title);
                Navigate("PageDesigner");
            };

            CardPrintVM.OpenInCropStudioAction = (bmp, title) =>
            {
                CropStudioVM.LoadImage(bmp, title);
                Navigate("CropStudio");
            };

            CardPrintVM.OpenInPrintPreviewAction = (front, back, kind) =>
            {
                _ = PrintPreviewApprovalVM.LoadCardsAsync(front, back, kind);
                Navigate("PrintPreviewApproval");
            };

            CardPrintVM.OpenInPageDesignerAction = (front, back, title) =>
            {
                PageDesignerVM.LoadCardsIntoDesigner(front, back, title);
                Navigate("PageDesigner");
            };

            PageDesignerVM.OpenInPrintPreviewAction = layout =>
            {
                PrintPreviewApprovalVM.InitializeWithLayout(layout);
                Navigate("PrintPreviewApproval");
            };

            PageDesignerVM.CloseAction = () =>
            {
                Navigate("DocumentDesk");
            };

            CropStudioVM.ReturnToDeskAction = (bmp, isFront) =>
            {
                DocumentDeskVM.ReceiveCroppedCard(bmp, isFront);
            };

            CropStudioVM.CloseAction = () =>
            {
                Navigate("DocumentDesk");
            };

            PrintPreviewApprovalVM.CloseAction = () =>
            {
                Navigate("DocumentDesk");
            };

            SettingsVM.ToggleLanguageAction = lang =>
            {
                if (lang != CurrentLanguage)
                {
                    ToggleLanguage();
                }
            };
            SettingsVM.ToggleThemeAction = theme => SetTheme(theme);

            CurrentView = ProductionVM;
            ActiveWorkspaceTitle = "Production Studio";
        }

        private void OnSessionChanged(SessionEvent ev)
        {
            var app = Application.Current;
            if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.InvokeAsync(SyncSessions);
            }
            else
            {
                SyncSessions();
            }
        }

        public void SyncSessions()
        {
            OpenSessions.Clear();
            foreach (var s in SessionManager.Sessions)
            {
                OpenSessions.Add(s);
            }
            SelectedSession = SessionManager.ActiveSession;
        }

        [RelayCommand]
        public void SwitchSession(string sessionId)
        {
            SessionManager.SwitchActiveSession(sessionId);
            SyncSessions();
        }

        [RelayCommand]
        public void CloseSession(string sessionId)
        {
            SessionManager.CloseSession(sessionId);
            SyncSessions();
        }

        [RelayCommand]
        public void Navigate(string destination)
        {
            switch (destination)
            {
                case "Production":
                    CurrentView = ProductionVM;
                    ActiveWorkspaceTitle = "Production Studio";
                    break;
                case "DocumentDesk":
                    CurrentView = DocumentDeskVM;
                    ActiveWorkspaceTitle = "Document Desk";
                    break;
                case "CropStudio":
                    CurrentView = CropStudioVM;
                    ActiveWorkspaceTitle = "Crop Studio";
                    break;
                case "PageDesigner":
                    CurrentView = PageDesignerVM;
                    ActiveWorkspaceTitle = "Page Designer";
                    break;
                case "PrintPreviewApproval":
                    CurrentView = PrintPreviewApprovalVM;
                    ActiveWorkspaceTitle = "Print Preview & Approval";
                    break;
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
            bool isDark = theme == "Dark";
            string themeFile = isDark ? "DarkTheme.xaml" : "LightTheme.xaml";
            string wpfUiThemeFile = isDark ? "dark.xaml" : "light.xaml";

            var appResources = Application.Current.Resources.MergedDictionaries;

            // 1. Update Wpf.Ui framework theme dictionary (light.xaml vs dark.xaml)
            for (int i = appResources.Count - 1; i >= 0; i--)
            {
                var src = appResources[i].Source?.OriginalString;
                if (src != null && src.Contains("/resources/theme/"))
                {
                    appResources.RemoveAt(i);
                }
            }
            appResources.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Wpf.Ui;component/resources/theme/{wpfUiThemeFile}", UriKind.Absolute)
            });

            // 2. Update app semantic theme tokens (LightTheme.xaml vs DarkTheme.xaml, preserving ThemeTokens.xaml!)
            for (int i = appResources.Count - 1; i >= 0; i--)
            {
                var src = appResources[i].Source?.OriginalString;
                if (src != null && (src.Contains("LightTheme.xaml") || src.Contains("DarkTheme.xaml")))
                {
                    appResources.RemoveAt(i);
                }
            }
            appResources.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Themes/{themeFile}", UriKind.Absolute)
            });
        }

        [RelayCommand]
        public void ExitTestMode()
        {
            _pathProvider?.SetTestMode(false);
            IsTestMode = false;
        }

        [RelayCommand]
        public async System.Threading.Tasks.Task QuickOpenDocumentAsync()
        {
            CurrentView = ProductionVM;
            ActiveWorkspaceTitle = "Production Studio";
            await ProductionVM.OpenDocumentFileDialogAsync();
        }

        [RelayCommand]
        public async System.Threading.Tasks.Task QuickPrintAsync()
        {
            if (CurrentView == ProductionVM)
            {
                await ProductionVM.StartPrintingAsync();
            }
        }

        [RelayCommand]
        public async System.Threading.Tasks.Task QuickExportPdfAsync()
        {
            if (CurrentView == ProductionVM)
            {
                await ProductionVM.ExportPdfAsync();
            }
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
