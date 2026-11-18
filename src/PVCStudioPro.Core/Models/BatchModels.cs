using System;

namespace PVCStudioPro.Core.Models
{
    public enum BatchItemStatus
    {
        Pending,
        Analyzing,
        Ready,
        Printing,
        Completed,
        Failed,
        Skipped
    }

    public class BatchItem
    {
        public string ItemId { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string DetectedTemplateId { get; set; } = string.Empty;
        public string DetectedTemplateName { get; set; } = "Analyzing...";
        public double ConfidenceScore { get; set; } = 0.0;
        public bool RequiresConfirmation { get; set; } = false;
        public BatchItemStatus Status { get; set; } = BatchItemStatus.Pending;
        public string? ErrorMessage { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; } = 70.00m;
        public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class BatchSummary
    {
        public int TotalItems { get; set; }
        public int ReadyItems { get; set; }
        public int CompletedItems { get; set; }
        public int FailedItems { get; set; }
        public decimal TotalEstimatedRevenue { get; set; }
    }
}
