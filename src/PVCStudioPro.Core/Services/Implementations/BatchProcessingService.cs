using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class BatchProcessingService : IBatchProcessingService
    {
        private readonly IPdfService _pdfService;
        private readonly ITemplateService _templateService;
        private readonly List<BatchItem> _queue = new List<BatchItem>();
        private readonly object _lock = new object();

        public BatchProcessingService(IPdfService pdfService, ITemplateService templateService)
        {
            _pdfService = pdfService ?? throw new ArgumentNullException(nameof(pdfService));
            _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        }

        public IReadOnlyList<BatchItem> GetQueue()
        {
            lock (_lock)
            {
                return _queue.ToList().AsReadOnly();
            }
        }

        public void EnqueueFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            lock (_lock)
            {
                var fileInfo = new FileInfo(filePath);
                _queue.Add(new BatchItem
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    FileSizeBytes = fileInfo.Length,
                    Status = BatchItemStatus.Pending
                });
            }
        }

        public void EnqueueFiles(IEnumerable<string> filePaths)
        {
            foreach (var path in filePaths)
            {
                EnqueueFile(path);
            }
        }

        public void RemoveItem(string itemId)
        {
            lock (_lock)
            {
                _queue.RemoveAll(i => i.ItemId == itemId);
            }
        }

        public void ClearQueue()
        {
            lock (_lock)
            {
                _queue.Clear();
            }
        }

        public async Task ProcessQueueAnalysisAsync(Action<BatchItem>? itemUpdatedCallback = null, CancellationToken cancellationToken = default)
        {
            List<BatchItem> itemsToProcess;
            lock (_lock)
            {
                itemsToProcess = _queue.Where(i => i.Status == BatchItemStatus.Pending).ToList();
            }

            foreach (var item in itemsToProcess)
            {
                cancellationToken.ThrowIfCancellationRequested();

                item.Status = BatchItemStatus.Analyzing;
                itemUpdatedCallback?.Invoke(item);

                try
                {
                    if (item.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        using var stream = File.OpenRead(item.FilePath);
                        var docInfo = await _pdfService.InspectDocumentAsync(stream, null, cancellationToken);

                        if (docInfo.IsEncrypted)
                        {
                            item.Status = BatchItemStatus.Failed;
                            item.ErrorMessage = "Password Protected PDF. Unlock required.";
                            item.DetectedTemplateName = "Encrypted PDF";
                        }
                        else
                        {
                            var matchResult = _templateService.MatchTemplate(docInfo.ExtractedTextSample);
                            if (matchResult.IsSuccess && matchResult.Template != null)
                            {
                                item.DetectedTemplateId = matchResult.Template.TemplateId;
                                item.DetectedTemplateName = matchResult.Template.DisplayName;
                                item.ConfidenceScore = matchResult.ConfidenceScore;
                                item.RequiresConfirmation = matchResult.RequiresOperatorConfirmation;
                                item.Status = BatchItemStatus.Ready;
                            }
                            else
                            {
                                item.DetectedTemplateName = "Unknown Document";
                                item.Status = BatchItemStatus.Ready;
                                item.RequiresConfirmation = true;
                                item.ErrorMessage = "Low confidence: Operator verification required.";
                            }
                        }
                    }
                    else
                    {
                        // Image file (jpg, png, tiff)
                        item.DetectedTemplateName = "Direct Image / Photograph";
                        item.Status = BatchItemStatus.Ready;
                    }
                }
                catch (Exception ex)
                {
                    item.Status = BatchItemStatus.Failed;
                    item.ErrorMessage = ex.Message;
                }

                itemUpdatedCallback?.Invoke(item);
            }
        }

        public BatchSummary GetSummary()
        {
            lock (_lock)
            {
                return new BatchSummary
                {
                    TotalItems = _queue.Count,
                    ReadyItems = _queue.Count(i => i.Status == BatchItemStatus.Ready),
                    CompletedItems = _queue.Count(i => i.Status == BatchItemStatus.Completed),
                    FailedItems = _queue.Count(i => i.Status == BatchItemStatus.Failed),
                    TotalEstimatedRevenue = _queue.Where(i => i.Status != BatchItemStatus.Failed)
                                                 .Sum(i => i.Quantity * i.UnitPrice)
                };
            }
        }
    }
}
