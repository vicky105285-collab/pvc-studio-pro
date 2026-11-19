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
    public partial class PrintCenterViewModel : ObservableObject
    {
        private readonly IJobLedgerService _ledgerService;
        private readonly IPrinterTrayService _trayService;

        [ObservableProperty]
        private string _selectedPrinter = string.Empty;

        [ObservableProperty]
        private string _spoolerStatus = "Spooler Service Active (winspool.drv)";

        [ObservableProperty]
        private bool _isSpoolerHealthy = true;

        [ObservableProperty]
        private string _statusMessage = "Print Center ready.";

        public ObservableCollection<string> InstalledPrinters { get; } = new ObservableCollection<string>();
        public ObservableCollection<PrintJobRecord> RecentSpoolJobs { get; } = new ObservableCollection<PrintJobRecord>();

        public ICommand RefreshCommand { get; }
        public ICommand ReprintJobCommand { get; }

        public PrintCenterViewModel(IJobLedgerService ledgerService, IPrinterTrayService trayService)
        {
            _ledgerService = ledgerService ?? throw new ArgumentNullException(nameof(ledgerService));
            _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));

            RefreshCommand = new AsyncRelayCommand(RefreshStatusAsync);
            ReprintJobCommand = new RelayCommand<PrintJobRecord>(ReprintJob);

            _ = RefreshStatusAsync();
        }

        public async Task RefreshStatusAsync()
        {
            try
            {
                var printers = await _trayService.DiscoverSystemPrintersAsync();
                InstalledPrinters.Clear();
                foreach (var p in printers)
                {
                    InstalledPrinters.Add(p);
                }
                if (InstalledPrinters.Count > 0 && string.IsNullOrEmpty(SelectedPrinter))
                {
                    SelectedPrinter = InstalledPrinters[0];
                }

                var jobs = await _ledgerService.GetRecentJobsAsync(15);
                RecentSpoolJobs.Clear();
                foreach (var j in jobs)
                {
                    RecentSpoolJobs.Add(j);
                }

                SpoolerStatus = $"Windows Spooler Active | {InstalledPrinters.Count} Detected Printers";
                IsSpoolerHealthy = true;
                StatusMessage = "Spooler status refreshed.";
            }
            catch (Exception ex)
            {
                SpoolerStatus = $"Spooler Warning: {ex.Message}";
                IsSpoolerHealthy = false;
            }
        }

        private void ReprintJob(PrintJobRecord? job)
        {
            if (job == null) return;
            StatusMessage = $"Quick Reprint requested for Invoice #{job.JobNumber} ({job.DocumentType}). Re-spooling...";
        }
    }
}
