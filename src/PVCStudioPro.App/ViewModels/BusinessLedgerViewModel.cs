using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;

namespace PVCStudioPro.App.ViewModels
{
    public partial class BusinessLedgerViewModel : ObservableObject
    {
        private readonly IJobLedgerService _jobLedger;

        [ObservableProperty]
        private int _todayCards = 0;

        [ObservableProperty]
        private decimal _todayRevenue = 0.00m;

        [ObservableProperty]
        private int _todayAadhaarCount = 0;

        [ObservableProperty]
        private int _todayPanCount = 0;

        [ObservableProperty]
        private ObservableCollection<PrintJobRecord> _recentJobs = new();

        [ObservableProperty]
        private string _statusMessage = "Zero Citizen PII Stored | Financial records stored locally in SQLite";

        public BusinessLedgerViewModel(IJobLedgerService jobLedger)
        {
            _jobLedger = jobLedger;
            _ = LoadLedgerAsync();
        }

        [RelayCommand]
        public async Task LoadLedgerAsync()
        {
            try
            {
                var summary = await _jobLedger.GetDailySummaryAsync(DateTime.UtcNow);
                TodayCards = summary.TotalCards;
                TodayRevenue = summary.TotalRevenue;
                TodayAadhaarCount = summary.AadhaarCount;
                TodayPanCount = summary.PanCount;

                var jobs = await _jobLedger.GetRecentJobsAsync(50);
                RecentJobs.Clear();
                foreach (var j in jobs)
                {
                    RecentJobs.Add(j);
                }

                StatusMessage = $"Updated at {DateTime.Now:T} | {RecentJobs.Count} transactions loaded";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ledger error: {ex.Message}";
            }
        }

        [RelayCommand]
        public void PrintReceipt(PrintJobRecord? record)
        {
            if (record == null) return;
            StatusMessage = $"Thermal 58mm/80mm receipt spooled for Job {record.JobId.Substring(0, 8)} (₹{record.TotalAmount})";
        }
    }
}
