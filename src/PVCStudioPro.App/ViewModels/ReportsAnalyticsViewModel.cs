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
    public partial class ReportsAnalyticsViewModel : ObservableObject
    {
        private readonly IJobLedgerService _ledgerService;

        [ObservableProperty]
        private decimal _monthlyRevenue;

        [ObservableProperty]
        private int _monthlyCards;

        [ObservableProperty]
        private int _totalAadhaarCount;

        [ObservableProperty]
        private int _totalPanCount;

        [ObservableProperty]
        private int _totalVoterCount;

        [ObservableProperty]
        private int _totalPhotoCount;

        [ObservableProperty]
        private double _cashPercent;

        [ObservableProperty]
        private double _upiPercent;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        public ObservableCollection<PrintJobRecord> AuditRecords { get; } = new ObservableCollection<PrintJobRecord>();

        public ICommand RefreshCommand { get; }

        public ReportsAnalyticsViewModel(IJobLedgerService ledgerService)
        {
            _ledgerService = ledgerService ?? throw new ArgumentNullException(nameof(ledgerService));
            RefreshCommand = new AsyncRelayCommand(LoadAnalyticsAsync);

            _ = LoadAnalyticsAsync();
        }

        public async Task LoadAnalyticsAsync()
        {
            try
            {
                var summary = await _ledgerService.GetDailySummaryAsync(DateTime.UtcNow);
                var jobs = await _ledgerService.GetRecentJobsAsync(100);

                AuditRecords.Clear();
                int aadhaar = 0, pan = 0, voter = 0, photo = 0;
                decimal totalRev = 0;
                int cashCount = 0, upiCount = 0;

                foreach (var j in jobs)
                {
                    AuditRecords.Add(j);
                    totalRev += j.TotalAmount;

                    if (j.DocumentType.Contains("AADHAAR", StringComparison.OrdinalIgnoreCase)) aadhaar += j.Quantity;
                    else if (j.DocumentType.Contains("PAN", StringComparison.OrdinalIgnoreCase)) pan += j.Quantity;
                    else if (j.DocumentType.Contains("VOTER", StringComparison.OrdinalIgnoreCase)) voter += j.Quantity;
                    else if (j.DocumentType.Contains("PHOTO", StringComparison.OrdinalIgnoreCase)) photo += j.Quantity;

                    if (j.PaymentMode.Equals("UPI", StringComparison.OrdinalIgnoreCase)) upiCount += j.Quantity;
                    else cashCount += j.Quantity;
                }

                MonthlyRevenue = totalRev;
                MonthlyCards = jobs.Count;
                TotalAadhaarCount = aadhaar;
                TotalPanCount = pan;
                TotalVoterCount = voter;
                TotalPhotoCount = photo;

                int totalP = cashCount + upiCount;
                CashPercent = totalP > 0 ? Math.Round((double)cashCount / totalP * 100.0, 1) : 50.0;
                UpiPercent = totalP > 0 ? Math.Round((double)upiCount / totalP * 100.0, 1) : 50.0;

                StatusMessage = $"Analytics updated at {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }
    }
}
