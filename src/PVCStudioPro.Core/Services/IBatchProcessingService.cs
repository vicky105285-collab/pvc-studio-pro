using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IBatchProcessingService
    {
        IReadOnlyList<BatchItem> GetQueue();
        void EnqueueFile(string filePath);
        void EnqueueFiles(IEnumerable<string> filePaths);
        void RemoveItem(string itemId);
        void ClearQueue();
        Task ProcessQueueAnalysisAsync(Action<BatchItem>? itemUpdatedCallback = null, CancellationToken cancellationToken = default);
        BatchSummary GetSummary();
    }
}
