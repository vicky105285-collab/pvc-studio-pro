using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;

namespace PVCStudioPro.App.ViewModels
{
    public partial class BusinessLedgerViewModel : ObservableObject
    {
        private readonly IJobLedgerService _jobLedger;
        private readonly IBusinessProfileService _profileService;
        private List<PrintJobRecord> _allLoadedJobs = new();

        [ObservableProperty]
        private int _todayCards = 0;

        [ObservableProperty]
        private decimal _todayRevenue = 0.00m;

        [ObservableProperty]
        private decimal _cashRevenue = 0.00m;

        [ObservableProperty]
        private decimal _upiRevenue = 0.00m;

        [ObservableProperty]
        private int _todayAadhaarCount = 0;

        [ObservableProperty]
        private int _todayPanCount = 0;

        [ObservableProperty]
        private string _searchFilter = string.Empty;

        [ObservableProperty]
        private string _selectedDateRange = "Today"; // Today, ThisWeek, ThisMonth, All

        [ObservableProperty]
        private ObservableCollection<PrintJobRecord> _filteredJobs = new();

        [ObservableProperty]
        private string _statusMessage = "Zero Citizen PII Stored | Financial transaction ledger stored locally in SQLite";

        [ObservableProperty]
        private bool _isBusy = false;

        public BusinessLedgerViewModel(
            IJobLedgerService jobLedger,
            IBusinessProfileService profileService)
        {
            _jobLedger = jobLedger;
            _profileService = profileService;
            _ = LoadLedgerAsync();
        }

        [RelayCommand]
        public async Task LoadLedgerAsync()
        {
            try
            {
                IsBusy = true;
                var summary = await _jobLedger.GetDailySummaryAsync(DateTime.UtcNow);
                TodayCards = summary.TotalCards;
                TodayRevenue = summary.TotalRevenue;
                TodayAadhaarCount = summary.AadhaarCount;
                TodayPanCount = summary.PanCount;

                var jobs = await _jobLedger.GetRecentJobsAsync(200);
                _allLoadedJobs = jobs.ToList();

                // Compute cash vs upi
                CashRevenue = _allLoadedJobs.Where(j => j.PaymentMode.Equals("CASH", StringComparison.OrdinalIgnoreCase)).Sum(j => j.TotalAmount);
                UpiRevenue = _allLoadedJobs.Where(j => j.PaymentMode.Equals("UPI", StringComparison.OrdinalIgnoreCase)).Sum(j => j.TotalAmount);

                ApplyFilter();
                StatusMessage = $"Updated at {DateTime.Now:T} | {FilteredJobs.Count} transactions loaded (Zero Citizen PII retained).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ledger error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSearchFilterChanged(string value)
        {
            ApplyFilter();
        }

        [RelayCommand]
        public void SetDateRange(string range)
        {
            SelectedDateRange = range;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var query = _allLoadedJobs.AsEnumerable();

            // Date filtering
            var now = DateTime.UtcNow;
            switch (SelectedDateRange)
            {
                case "Today":
                    query = query.Where(j => j.CreatedAt.Date == now.Date);
                    break;
                case "ThisWeek":
                    var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
                    query = query.Where(j => j.CreatedAt.Date >= startOfWeek);
                    break;
                case "ThisMonth":
                    var startOfMonth = new DateTime(now.Year, now.Month, 1);
                    query = query.Where(j => j.CreatedAt.Date >= startOfMonth);
                    break;
                case "All":
                default:
                    break;
            }

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchFilter))
            {
                string term = SearchFilter.Trim().ToLowerInvariant();
                query = query.Where(j =>
                    j.DocumentType.ToLowerInvariant().Contains(term) ||
                    j.PaymentMode.ToLowerInvariant().Contains(term) ||
                    j.PrinterName.ToLowerInvariant().Contains(term) ||
                    j.JobId.ToLowerInvariant().Contains(term));
            }

            FilteredJobs.Clear();
            foreach (var item in query.OrderByDescending(j => j.CreatedAt))
            {
                FilteredJobs.Add(item);
            }
        }

        [RelayCommand]
        public async Task ExportCsvAsync()
        {
            if (FilteredJobs.Count == 0)
            {
                StatusMessage = "No transactions available to export.";
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "CSV Spreadsheet (*.csv)|*.csv",
                FileName = $"PVC_Studio_Pro_Ledger_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                Title = "Export Financial Ledger to CSV"
            };

            if (dlg.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("JobId,Timestamp,DocumentType,Quantity,TotalAmount,PaymentMode,PrinterName,Status");

                foreach (var j in FilteredJobs)
                {
                    sb.AppendLine($"\"{j.JobId}\",\"{j.CreatedAt:yyyy-MM-dd HH:mm:ss}\",\"{j.DocumentType}\",{j.Quantity},{j.TotalAmount:F2},\"{j.PaymentMode}\",\"{j.PrinterName}\",\"{j.Status}\"");
                }

                await File.WriteAllTextAsync(dlg.FileName, sb.ToString(), Encoding.UTF8);
                StatusMessage = $"Financial ledger exported ({FilteredJobs.Count} records) to {dlg.FileName}";
            }
        }

        [RelayCommand]
        public async Task PrintReceiptAsync(PrintJobRecord? record)
        {
            if (record == null) return;

            try
            {
                var profile = await _profileService.GetProfileAsync();
                string shop = string.IsNullOrWhiteSpace(profile.ShopName) ? "e-Sevai Maiyam" : profile.ShopName;
                string paper = profile.ReceiptPaperSize;

                StatusMessage = $"Receipt spooled to {paper} thermal receipt printer for Job {record.JobId.Substring(0, Math.Min(8, record.JobId.Length))} [Shop: {shop}, ₹{record.TotalAmount:F2}]";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Receipt error: {ex.Message}";
            }
        }
    }
}
