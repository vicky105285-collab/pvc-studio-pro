using System;
using System.Collections.Generic;

namespace PVCStudioPro.Core.Models
{
    public class BackupMetadata
    {
        public string BackupId { get; set; } = Guid.NewGuid().ToString();
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public long FileSizeBytes { get; set; }
        public int TotalRecordsIncluded { get; set; }
        public bool IsVerified { get; set; } = false;
        public string VerificationNotes { get; set; } = string.Empty;
    }

    public class RestorePreview
    {
        public DateTime BackupTimestampUtc { get; set; }
        public int TotalJobsCount { get; set; }
        public DateTime? EarliestJobUtc { get; set; }
        public DateTime? LatestJobUtc { get; set; }
        public decimal TotalRecordedRevenue { get; set; }
        public bool CanSafelyRestore { get; set; } = true;
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
