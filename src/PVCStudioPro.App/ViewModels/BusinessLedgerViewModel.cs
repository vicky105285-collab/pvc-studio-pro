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
        private readonly ITransactionPaymentService _paymentService;
        private readonly IDatabasePathProvider _pathProvider;
        private readonly IJobLedgerService _jobLedger;
        private readonly IBusinessProfileService _profileService;
        private List<BusinessTransaction> _allLoadedTransactions = new();

        [ObservableProperty]
        private bool _isTestMode = false;

        public string TestModeButtonText => IsTestMode ? "Exit Test Mode" : "Switch to Test Mode";

        partial void OnIsTestModeChanged(bool value)
        {
            OnPropertyChanged(nameof(TestModeButtonText));
        }

        [ObservableProperty]
        private decimal _todayRevenue = 0.00m;

        [ObservableProperty]
        private decimal _totalCollected = 0.00m;

        [ObservableProperty]
        private decimal _totalRefunded = 0.00m;

        [ObservableProperty]
        private decimal _cashRevenue = 0.00m;

        [ObservableProperty]
        private decimal _upiRevenue = 0.00m;

        [ObservableProperty]
        private decimal _cardRevenue = 0.00m;

        [ObservableProperty]
        private int _todayCards = 0;

        [ObservableProperty]
        private string _searchFilter = string.Empty;

        [ObservableProperty]
        private ObservableCollection<BusinessTransaction> _filteredTransactions = new();

        public bool HasNoTransactions => FilteredTransactions == null || FilteredTransactions.Count == 0;

        partial void OnFilteredTransactionsChanged(ObservableCollection<BusinessTransaction> value)
        {
            OnPropertyChanged(nameof(HasNoTransactions));
        }

        [ObservableProperty]
        private BusinessTransaction? _selectedTransaction;

        [ObservableProperty]
        private ObservableCollection<TransactionPayment> _selectedPayments = new();

        [ObservableProperty]
        private ObservableCollection<TransactionAuditTrail> _selectedAuditTrail = new();

        [ObservableProperty]
        private string _statusMessage = "Zero Citizen PII Stored | Financial transaction ledger stored locally in SQLite";

        [ObservableProperty]
        private bool _isBusy = false;

        // Payment Dialog Controls
        [ObservableProperty]
        private bool _isPaymentDialogVisible = false;

        [ObservableProperty]
        private decimal _paymentInputAmount = 50.00m;

        [ObservableProperty]
        private string _paymentInputMethod = "CASH";

        [ObservableProperty]
        private string _paymentInputReference = string.Empty;

        // Refund Dialog Controls
        [ObservableProperty]
        private bool _isRefundDialogVisible = false;

        [ObservableProperty]
        private decimal _refundInputAmount = 50.00m;

        [ObservableProperty]
        private string _refundInputReason = string.Empty;

        // Void Dialog Controls
        [ObservableProperty]
        private bool _isVoidDialogVisible = false;

        [ObservableProperty]
        private string _voidInputReason = string.Empty;

        public BusinessLedgerViewModel(
            ITransactionPaymentService paymentService,
            IDatabasePathProvider pathProvider,
            IJobLedgerService jobLedger,
            IBusinessProfileService profileService)
        {
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
            _jobLedger = jobLedger ?? throw new ArgumentNullException(nameof(jobLedger));
            _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));

            _isTestMode = _pathProvider.IsTestMode;
            _pathProvider.TestModeChanged += (s, mode) =>
            {
                IsTestMode = mode;
                _ = LoadLedgerAsync();
            };

            _ = LoadLedgerAsync();
        }

        [RelayCommand]
        public async Task LoadLedgerAsync()
        {
            try
            {
                IsBusy = true;
                IsTestMode = _pathProvider.IsTestMode;

                // IST Day calculation (UTC + 05:30)
                var nowUtc = DateTime.UtcNow;
                var nowIst = nowUtc.AddHours(5.5);
                var todayIst = DateOnly.FromDateTime(nowIst);

                var report = await _paymentService.GetDailyReconciliationAsync(todayIst, _pathProvider.IsTestMode);
                TodayRevenue = report.TotalRevenueRupees;
                TotalCollected = report.TotalCollectedRupees;
                TotalRefunded = report.TotalRefundedRupees;
                CashRevenue = report.NetCashRupees;
                UpiRevenue = report.UpiRupees;
                CardRevenue = report.CardRupees;
                TodayCards = report.TransactionCount;

                var transactions = await _paymentService.GetRecentTransactionsAsync(200, _pathProvider.IsTestMode);
                _allLoadedTransactions = transactions.ToList();

                ApplyFilter();

                if (SelectedTransaction != null)
                {
                    await LoadSelectedTransactionDetailsAsync(SelectedTransaction);
                }

                string modeTag = _pathProvider.IsTestMode ? "⚠️ TEST SANDBOX ACTIVE" : "PRODUCTION LEDGER";
                StatusMessage = $"{modeTag} | Updated at {DateTime.Now:T} | {FilteredTransactions.Count} transactions loaded.";
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

        partial void OnSelectedTransactionChanged(BusinessTransaction? value)
        {
            if (value != null)
            {
                _ = LoadSelectedTransactionDetailsAsync(value);
            }
            else
            {
                SelectedPayments.Clear();
                SelectedAuditTrail.Clear();
            }
        }

        private async Task LoadSelectedTransactionDetailsAsync(BusinessTransaction tx)
        {
            try
            {
                var payments = await _paymentService.GetPaymentsForTransactionAsync(tx.TransactionId);
                SelectedPayments = new ObservableCollection<TransactionPayment>(payments);

                var audit = await _paymentService.GetAuditTrailAsync(tx.TransactionId);
                SelectedAuditTrail = new ObservableCollection<TransactionAuditTrail>(audit);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Notice: Failed to load details for {tx.InvoiceNumber}: {ex.Message}";
            }
        }

        partial void OnSearchFilterChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var q = _allLoadedTransactions.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchFilter))
            {
                q = q.Where(t =>
                    t.InvoiceNumber.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    t.CustomerName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    t.DocumentType.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    t.PrinterName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    t.Status.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)
                );
            }

            FilteredTransactions = new ObservableCollection<BusinessTransaction>(q);
        }

        [RelayCommand]
        public void OpenPaymentDialog()
        {
            if (SelectedTransaction == null || SelectedTransaction.Status == "VOID")
            {
                StatusMessage = "Please select an active, non-voided transaction.";
                return;
            }

            PaymentInputAmount = SelectedTransaction.BalanceRupees > 0 ? SelectedTransaction.BalanceRupees : 50.00m;
            PaymentInputMethod = "CASH";
            PaymentInputReference = string.Empty;
            IsPaymentDialogVisible = true;
        }

        [RelayCommand]
        public async Task SubmitPaymentAsync()
        {
            if (SelectedTransaction == null) return;

            try
            {
                IsBusy = true;
                long amountPaise = (long)Math.Round(PaymentInputAmount * 100);

                await _paymentService.RecordPaymentAsync(new AddPaymentCommand
                {
                    TransactionId = SelectedTransaction.TransactionId,
                    AmountPaise = amountPaise,
                    PaymentMethod = PaymentInputMethod,
                    ReferenceNumber = PaymentInputReference,
                    IsTestMode = _pathProvider.IsTestMode
                });

                IsPaymentDialogVisible = false;
                await LoadLedgerAsync();
                StatusMessage = $"✓ Recorded payment of ₹{PaymentInputAmount:F2} ({PaymentInputMethod}) for {SelectedTransaction.InvoiceNumber}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Payment error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void ClosePaymentDialog()
        {
            IsPaymentDialogVisible = false;
        }

        [RelayCommand]
        public void OpenRefundDialog()
        {
            if (SelectedTransaction == null)
            {
                StatusMessage = "Please select a transaction to refund.";
                return;
            }

            RefundInputAmount = SelectedTransaction.PaidRupees > 0 ? SelectedTransaction.PaidRupees : 10.00m;
            RefundInputReason = "Print output defect / customer request";
            IsRefundDialogVisible = true;
        }

        [RelayCommand]
        public async Task SubmitRefundAsync()
        {
            if (SelectedTransaction == null) return;

            try
            {
                IsBusy = true;
                long amountPaise = (long)Math.Round(RefundInputAmount * 100);

                await _paymentService.RecordRefundAsync(new AddRefundCommand
                {
                    TransactionId = SelectedTransaction.TransactionId,
                    AmountPaise = amountPaise,
                    PaymentMethod = "CASH",
                    MandatoryReason = RefundInputReason,
                    IsTestMode = _pathProvider.IsTestMode
                });

                IsRefundDialogVisible = false;
                await LoadLedgerAsync();
                StatusMessage = $"✓ Issued refund of ₹{RefundInputAmount:F2} for {SelectedTransaction.InvoiceNumber}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Refund error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void CloseRefundDialog()
        {
            IsRefundDialogVisible = false;
        }

        [RelayCommand]
        public void OpenVoidDialog()
        {
            if (SelectedTransaction == null || SelectedTransaction.Status == "VOID")
            {
                StatusMessage = "Please select an active transaction to void.";
                return;
            }

            VoidInputReason = "Damaged print output during spooling / operator cancellation";
            IsVoidDialogVisible = true;
        }

        [RelayCommand]
        public async Task SubmitVoidAsync()
        {
            if (SelectedTransaction == null) return;

            try
            {
                IsBusy = true;
                await _paymentService.VoidTransactionAsync(new VoidTransactionCommand
                {
                    TransactionId = SelectedTransaction.TransactionId,
                    MandatoryReason = VoidInputReason,
                    RefundAllCollected = true,
                    IsTestMode = _pathProvider.IsTestMode
                });

                IsVoidDialogVisible = false;
                await LoadLedgerAsync();
                StatusMessage = $"✓ Voided invoice {SelectedTransaction.InvoiceNumber}. Full collected amount refunded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Void error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void CloseVoidDialog()
        {
            IsVoidDialogVisible = false;
        }

        [RelayCommand]
        public void ToggleTestMode()
        {
            _pathProvider.SetTestMode(!_pathProvider.IsTestMode);
            IsTestMode = _pathProvider.IsTestMode;
        }

        [RelayCommand]
        public async Task ResetTestModeDatabaseAsync()
        {
            if (!_pathProvider.IsTestMode)
            {
                StatusMessage = "Cannot reset database while in Production Mode!";
                return;
            }

            try
            {
                _pathProvider.ResetTestDatabase();
                await LoadLedgerAsync();
                StatusMessage = "✓ Test mode sandbox database has been safely cleared.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Reset notice: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task ExportCsvAsync()
        {
            if (FilteredTransactions.Count == 0)
            {
                StatusMessage = "No transaction records to export.";
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                FileName = $"BusinessLedger_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Title = "Export Financial Ledger to CSV"
            };

            if (dlg.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("InvoiceNumber,DateUTC,CustomerName,DocumentType,Quantity,UnitPrice,Discount,NetTotal,Paid,Balance,Status,Printer,IsTestMode");

                foreach (var t in FilteredTransactions)
                {
                    sb.AppendLine($"\"{t.InvoiceNumber}\",\"{t.CreatedAt:yyyy-MM-dd HH:mm:ss}\",\"{t.CustomerName}\",\"{t.DocumentType}\",{t.Quantity},{t.UnitPricePaise / 100.0:F2},{t.DiscountPaise / 100.0:F2},{t.NetTotalPaise / 100.0:F2},{t.PaidPaise / 100.0:F2},{t.BalancePaise / 100.0:F2},\"{t.Status}\",\"{t.PrinterName}\",{t.IsTestMode}");
                }

                await File.WriteAllTextAsync(dlg.FileName, sb.ToString(), Encoding.UTF8);
                StatusMessage = $"✓ Exported {FilteredTransactions.Count} ledger rows to {Path.GetFileName(dlg.FileName)}.";
            }
        }
    }
}
