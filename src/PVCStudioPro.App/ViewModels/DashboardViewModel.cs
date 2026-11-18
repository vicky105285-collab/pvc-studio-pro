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
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IJobLedgerService _ledgerService;
        private readonly IPrinterTrayService _trayService;
        public Action<string>? NavigateAction { get; set; }

        [ObservableProperty]
        private int _todayCards;

        [ObservableProperty]
        private int _todayPhotos;

        [ObservableProperty]
        private decimal _todayRevenue;

        [ObservableProperty]
        private int _cashPaymentCount;

        [ObservableProperty]
        private int _upiPaymentCount;

        [ObservableProperty]
        private string _activePrinterName = "Scanning...";

        [ObservableProperty]
        private string _activeTrayProfileName = "Default Tray";

        [ObservableProperty]
        private bool _isPrinterOnline = true;

        [ObservableProperty]
        private string _systemNotice = "Local Offline Engine Active | Zero Customer PII Stored | Hardware Verification Ready";

        public ObservableCollection<PrintJobRecord> RecentJobs { get; } = new ObservableCollection<PrintJobRecord>();

        public ICommand RefreshCommand { get; }
        public ICommand NavigateToCommand { get; }

        public DashboardViewModel(IJobLedgerService ledgerService, IPrinterTrayService trayService, Action<string>? navigateAction = null)
        {
            _ledgerService = ledgerService ?? throw new ArgumentNullException(nameof(ledgerService));
            _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));
            NavigateAction = navigateAction;

            RefreshCommand = new AsyncRelayCommand(LoadDashboardDataAsync);
            NavigateToCommand = new RelayCommand<string>(dest =>
            {
                if (!string.IsNullOrEmpty(dest))
                    NavigateAction?.Invoke(dest);
            });

            _ = LoadDashboardDataAsync();
        }

        public async Task LoadDashboardDataAsync()
        {
            try
            {
                var summary = await _ledgerService.GetDailySummaryAsync(DateTime.UtcNow);
                TodayCards = summary.TotalCards;
                TodayRevenue = summary.TotalRevenue;
                CashPaymentCount = summary.CashPaymentCount;
                UpiPaymentCount = summary.UpiPaymentCount;

                var jobs = await _ledgerService.GetRecentJobsAsync(8);
                RecentJobs.Clear();
                int photosCount = 0;
                foreach (var j in jobs)
                {
                    RecentJobs.Add(j);
                    if (j.DocumentType.Contains("PHOTO", StringComparison.OrdinalIgnoreCase))
                    {
                        photosCount += j.Quantity;
                    }
                }
                TodayPhotos = photosCount;

                // Load active printer and tray profile
                var profiles = await _trayService.GetAllProfilesAsync();
                var defaultProf = profiles.Count > 0 ? profiles[0] : null;
                if (defaultProf != null)
                {
                    ActivePrinterName = string.IsNullOrEmpty(defaultProf.PrinterDeviceName) ? "Default Windows Spooler" : defaultProf.PrinterDeviceName;
                    ActiveTrayProfileName = defaultProf.ProfileName;
                    IsPrinterOnline = true;
                }
            }
            catch
            {
                // Resilient fallback
            }
        }
    }
}
