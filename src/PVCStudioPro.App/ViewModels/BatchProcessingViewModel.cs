using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;

namespace PVCStudioPro.App.ViewModels
{
    public partial class BatchProcessingViewModel : ObservableObject
    {
        private readonly IBatchProcessingService _batchService;
        private CancellationTokenSource? _cts;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _readyCount;

        [ObservableProperty]
        private int _completedCount;

        [ObservableProperty]
        private decimal _totalRevenue;

        [ObservableProperty]
        private string _statusMessage = "Ready. Add PDF or image files to begin batch production.";

        public ObservableCollection<BatchItem> BatchQueue { get; } = new ObservableCollection<BatchItem>();

        public ICommand AddFilesCommand { get; }
        public ICommand StartBatchCommand { get; }
        public ICommand CancelBatchCommand { get; }
        public ICommand ClearQueueCommand { get; }
        public ICommand RemoveItemCommand { get; }

        public BatchProcessingViewModel(IBatchProcessingService batchService)
        {
            _batchService = batchService ?? throw new ArgumentNullException(nameof(batchService));

            AddFilesCommand = new RelayCommand(AddFiles);
            StartBatchCommand = new AsyncRelayCommand(StartBatchAnalysisAsync);
            CancelBatchCommand = new RelayCommand(CancelBatch);
            ClearQueueCommand = new RelayCommand(ClearQueue);
            RemoveItemCommand = new RelayCommand<BatchItem>(RemoveItem);
        }

        private void AddFiles()
        {
            var dlg = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Identity Documents (*.pdf;*.png;*.jpg)|*.pdf;*.png;*.jpg|All Files (*.*)|*.*",
                Title = "Select Documents for Batch Production"
            };

            if (dlg.ShowDialog() == true)
            {
                _batchService.EnqueueFiles(dlg.FileNames);
                RefreshQueue();
                StatusMessage = $"Added {dlg.FileNames.Length} files to queue. Click 'Start Batch' to analyze.";
            }
        }

        private async Task StartBatchAnalysisAsync()
        {
            if (IsProcessing) return;
            IsProcessing = true;
            _cts = new CancellationTokenSource();

            try
            {
                StatusMessage = "Analyzing batch documents...";
                await _batchService.ProcessQueueAnalysisAsync(item =>
                {
                    App.Current?.Dispatcher?.Invoke(() =>
                    {
                        RefreshQueue();
                    });
                }, _cts.Token);

                StatusMessage = "Batch analysis complete. Ready for spooling.";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Batch operation was cancelled.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Batch error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
                RefreshQueue();
            }
        }

        private void CancelBatch()
        {
            _cts?.Cancel();
            StatusMessage = "Cancelling batch...";
        }

        private void ClearQueue()
        {
            _batchService.ClearQueue();
            RefreshQueue();
            StatusMessage = "Queue cleared.";
        }

        private void RemoveItem(BatchItem? item)
        {
            if (item == null) return;
            _batchService.RemoveItem(item.ItemId);
            RefreshQueue();
        }

        private void RefreshQueue()
        {
            var items = _batchService.GetQueue();
            BatchQueue.Clear();
            foreach (var it in items)
            {
                BatchQueue.Add(it);
            }

            var sum = _batchService.GetSummary();
            TotalCount = sum.TotalItems;
            ReadyCount = sum.ReadyItems;
            CompletedCount = sum.CompletedItems;
            TotalRevenue = sum.TotalEstimatedRevenue;
        }
    }
}
