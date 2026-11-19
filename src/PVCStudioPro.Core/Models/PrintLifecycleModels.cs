using System;
using System.Collections.Generic;

namespace PVCStudioPro.Core.Models
{
    public enum PrintJobLifecycleStatus
    {
        SubmittedToSpooler,
        AcceptedBySpooler,
        SpoolerProcessingCompleted,
        SpoolerErrorOrCancelled,
        PhysicalOutputConfirmedByOperator,
        PhysicalOutputNotVerified
    }

    public class PrintJobAttempt
    {
        public string AttemptId { get; set; } = Guid.NewGuid().ToString("N");
        public string JobId { get; set; } = string.Empty;
        public int AttemptNumber { get; set; } = 1;
        public PrintJobLifecycleStatus Status { get; set; } = PrintJobLifecycleStatus.SubmittedToSpooler;
        public int SpoolerJobId { get; set; }
        public string PrinterName { get; set; } = string.Empty;
        public string MediaKind { get; set; } = "A4_UPPER_DUAL";
        public bool IsBillable { get; set; } = true;
        public string? DefectReason { get; set; }
        public string OperatorNotes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }

    public class PrintSpoolJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString("N");
        public string DocumentTitle { get; set; } = "PVC Studio Pro Card Print";
        public string? CustomerName { get; set; }
        public PrintJobLifecycleStatus CurrentStatus { get; set; } = PrintJobLifecycleStatus.SubmittedToSpooler;
        public List<PrintJobAttempt> Attempts { get; set; } = new();
        public int TotalAttempts => Attempts.Count;
        public bool HasSuccessfulAttempt => Attempts.Exists(a => a.Status == PrintJobLifecycleStatus.PhysicalOutputConfirmedByOperator);
        public PrintJobAttempt? ActiveAttempt => Attempts.Count > 0 ? Attempts[^1] : null;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public PrintJobAttempt CreateNewAttempt(string printerName, string mediaKind, bool isBillable = false, string? defectReason = null)
        {
            var attempt = new PrintJobAttempt
            {
                JobId = JobId,
                AttemptNumber = Attempts.Count + 1,
                Status = PrintJobLifecycleStatus.SubmittedToSpooler,
                PrinterName = printerName,
                MediaKind = mediaKind,
                IsBillable = isBillable,
                DefectReason = defectReason,
                CreatedAt = DateTime.UtcNow
            };
            Attempts.Add(attempt);
            CurrentStatus = attempt.Status;
            return attempt;
        }
    }
}
