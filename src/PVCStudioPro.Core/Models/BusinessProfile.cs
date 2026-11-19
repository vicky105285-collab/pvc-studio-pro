using System;

namespace PVCStudioPro.Core.Models
{
    public class BusinessProfile
    {
        // Business Identity
        public string ShopName { get; set; } = "e-Sevai Maiyam (ஈ-சேவை மையம்)";
        public string OperatorName { get; set; } = "Centre Operator";
        public string BusinessType { get; set; } = "e-Sevai & CSC Centre";
        public string AddressLine1 { get; set; } = "Main Road, Opp. Bus Stand";
        public string AddressLine2 { get; set; } = string.Empty;
        public string District { get; set; } = "Karur";
        public string State { get; set; } = "Tamil Nadu";
        public string PinCode { get; set; } = "639001";
        public string PhoneNumber { get; set; } = "9876543210";
        public string EmailAddress { get; set; } = "esevai.karur@gmail.com";
        public string WebsiteUrl { get; set; } = string.Empty;
        public string LogoPath { get; set; } = string.Empty;

        // Billing & Invoicing Configuration
        public string CurrencySymbol { get; set; } = "₹";
        public string ReceiptPrefix { get; set; } = "INV";
        public int NextInvoiceNumber { get; set; } = 1001;
        public string DefaultPaymentMode { get; set; } = "CASH"; // CASH, UPI, CARD
        public bool EnableTaxGst { get; set; } = false;
        public string GstNumber { get; set; } = string.Empty;
        public decimal TaxRatePercent { get; set; } = 0.0m;
        public string ReceiptFooterMessage { get; set; } = "Thank you for visiting! Local e-Sevai Authorized Services.";
        public string ReceiptPaperSize { get; set; } = "58mm"; // 58mm, 80mm, A4

        // Hardware & Production Defaults
        public string DefaultPrinter { get; set; } = string.Empty;
        public string DefaultTrayProfileId { get; set; } = string.Empty;
        public string DefaultDocumentTemplateId { get; set; } = "aadhaar_v2024";
        public int DefaultCopies { get; set; } = 1;
        public decimal DefaultCardPrice { get; set; } = 70.00m;
        public decimal DefaultPhotoSheetPrice { get; set; } = 50.00m;

        // Appearance & System Preferences
        public string ThemePreference { get; set; } = "System"; // Light, Dark, System
        public string LanguagePreference { get; set; } = "en"; // en, ta
        public int UiScalePercent { get; set; } = 100; // 100, 125, 150
        public string LocalStorageDirectory { get; set; } = string.Empty;
        public string AutoBackupFrequency { get; set; } = "Daily"; // Daily, Weekly, Manual
        public int AuditLogRetentionDays { get; set; } = 90;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
