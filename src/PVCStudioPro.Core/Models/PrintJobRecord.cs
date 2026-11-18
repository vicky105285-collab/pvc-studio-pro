using System;

namespace PVCStudioPro.Core.Models
{
    public class PrintJobRecord
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public string JobNumber { get; set; } = string.Empty;
        public string ProfileId { get; set; } = string.Empty;
        public string PrinterName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = "AADHAAR"; // AADHAAR, PAN, VOTER, AYUSHMAN, PHOTO_STUDIO, CUSTOM
        public string Status { get; set; } = "COMPLETED"; // PENDING, PRINTING, COMPLETED, FAILED
        public int Quantity { get; set; } = 1;
        public decimal TotalAmount { get; set; } = 70.00m;
        public decimal AmountPaid { get; set; } = 70.00m;
        public string PaymentMode { get; set; } = "CASH"; // CASH, UPI, CARD
        public bool IsReprint { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class DailySummary
    {
        public DateTime Date { get; set; }
        public int TotalCards { get; set; }
        public decimal TotalRevenue { get; set; }
        public int AadhaarCount { get; set; }
        public int PanCount { get; set; }
        public int VoterCount { get; set; }
        public int PhotoSheetCount { get; set; }
        public int OtherCount { get; set; }
    }
}
