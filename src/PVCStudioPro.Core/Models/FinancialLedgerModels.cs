using System;
using System.Collections.Generic;

namespace PVCStudioPro.Core.Models
{
    public class BusinessTransaction
    {
        public string TransactionId { get; set; } = Guid.NewGuid().ToString("N");
        public string InvoiceNumber { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public string CustomerName { get; set; } = "Walk-in Customer";
        public string? CustomerPhone { get; set; }
        public string DocumentType { get; set; } = "AADHAAR_CR80";
        public string? TemplateId { get; set; }
        public string PrinterName { get; set; } = "Default Printer";
        public int Quantity { get; set; } = 1;

        // All amounts stored strictly in 64-bit integer paise (₹1.00 = 100 paise)
        public long UnitPricePaise { get; set; }
        public long DiscountPaise { get; set; } = 0;
        public long NetTotalPaise { get; set; }
        public long PaidPaise { get; set; } = 0;
        public long BalancePaise { get; set; }
        public string Status { get; set; } = "PENDING"; // PENDING, PARTIAL, PAID, VOID
        public bool IsTestMode { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // UI display helpers
        public decimal NetTotalRupees => NetTotalPaise / 100.0m;
        public decimal PaidRupees => PaidPaise / 100.0m;
        public decimal BalanceRupees => BalancePaise / 100.0m;
    }

    public class TransactionPayment
    {
        public string PaymentId { get; set; } = Guid.NewGuid().ToString("N");
        public string TransactionId { get; set; } = string.Empty;
        public long AmountPaise { get; set; }
        public bool IsRefund { get; set; } = false;
        public string PaymentMethod { get; set; } = "CASH"; // CASH, UPI, CARD, OTHER
        public string? ReferenceNumber { get; set; }
        public bool IsTestMode { get; set; } = false;
        public string OperatorId { get; set; } = "Admin";
        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

        public decimal AmountRupees => AmountPaise / 100.0m;
    }

    public class TransactionAuditTrail
    {
        public string AuditId { get; set; } = Guid.NewGuid().ToString("N");
        public string TransactionId { get; set; } = string.Empty;
        public string ActionType { get; set; } = "INVOICE_CREATED"; // INVOICE_CREATED, PAYMENT_ADDED, VOIDED, REFUNDED, REPRINTED
        public string? FieldChanged { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string MandatoryReason { get; set; } = string.Empty;
        public string OperatorId { get; set; } = "Admin";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class DailyReconciliationReport
    {
        public DateOnly BusinessDateIst { get; set; }
        public bool IsTestMode { get; set; }
        public long TotalCollectedPaise { get; set; }
        public long TotalRefundedPaise { get; set; }
        public long NetCashPaise { get; set; }
        public long UpiPaise { get; set; }
        public long CardPaise { get; set; }
        public long TotalRevenuePaise { get; set; }
        public int TransactionCount { get; set; }

        public decimal TotalCollectedRupees => TotalCollectedPaise / 100.0m;
        public decimal TotalRefundedRupees => TotalRefundedPaise / 100.0m;
        public decimal NetCashRupees => NetCashPaise / 100.0m;
        public decimal UpiRupees => UpiPaise / 100.0m;
        public decimal CardRupees => CardPaise / 100.0m;
        public decimal TotalRevenueRupees => TotalRevenuePaise / 100.0m;
    }

    public class CreateTransactionCommand
    {
        public string? InvoiceNumber { get; set; }
        public string? SessionId { get; set; }
        public string CustomerName { get; set; } = "Walk-in Customer";
        public string? CustomerPhone { get; set; }
        public string DocumentType { get; set; } = "AADHAAR_CR80";
        public string? TemplateId { get; set; }
        public string PrinterName { get; set; } = "Default Printer";
        public int Quantity { get; set; } = 1;
        public long UnitPricePaise { get; set; }
        public long DiscountPaise { get; set; } = 0;
        public bool IsTestMode { get; set; } = false;
        public long InitialPaymentPaise { get; set; } = 0;
        public string InitialPaymentMethod { get; set; } = "CASH";
        public string? ReferenceNumber { get; set; }
    }

    public class AddPaymentCommand
    {
        public string TransactionId { get; set; } = string.Empty;
        public long AmountPaise { get; set; }
        public string PaymentMethod { get; set; } = "CASH"; // CASH, UPI, CARD
        public string? ReferenceNumber { get; set; }
        public bool IsTestMode { get; set; } = false;
        public string OperatorId { get; set; } = "Admin";
        public string? Notes { get; set; }
    }

    public class AddRefundCommand
    {
        public string TransactionId { get; set; } = string.Empty;
        public long AmountPaise { get; set; }
        public string PaymentMethod { get; set; } = "CASH";
        public string MandatoryReason { get; set; } = string.Empty;
        public bool IsTestMode { get; set; } = false;
        public string OperatorId { get; set; } = "Admin";
    }

    public class VoidTransactionCommand
    {
        public string TransactionId { get; set; } = string.Empty;
        public string MandatoryReason { get; set; } = string.Empty;
        public bool RefundAllCollected { get; set; } = true;
        public string RefundPaymentMethod { get; set; } = "CASH";
        public bool IsTestMode { get; set; } = false;
        public string OperatorId { get; set; } = "Admin";
    }

    public class RefundExceedsCollectedException : InvalidOperationException
    {
        public RefundExceedsCollectedException(string message) : base(message) { }
    }

    public class OverpaymentNotPermittedException : InvalidOperationException
    {
        public OverpaymentNotPermittedException(string message) : base(message) { }
    }

    public class SecurityIsolationException : InvalidOperationException
    {
        public SecurityIsolationException(string message) : base(message) { }
    }

    public static class IndianBusinessDay
    {
        private static readonly TimeZoneInfo IstZone;

        static IndianBusinessDay()
        {
            try
            {
                IstZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            }
            catch
            {
                // Fallback for non-Windows or POSIX systems
                IstZone = TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "India Standard Time", "IST");
            }
        }

        public static (string StartUtcString, string NextDayStartUtcString) GetIstDayUtcBounds(DateOnly istDate)
        {
            var istStart = istDate.ToDateTime(TimeOnly.MinValue);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(istStart, IstZone);

            var nextDayIstStart = istDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var nextDayStartUtc = TimeZoneInfo.ConvertTimeToUtc(nextDayIstStart, IstZone);

            return (
                startUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                nextDayStartUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            );
        }
    }
}
