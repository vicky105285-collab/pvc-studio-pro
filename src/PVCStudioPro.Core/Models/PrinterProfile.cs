using System;

namespace PVCStudioPro.Core.Models
{
    public class PrinterProfile
    {
        public string ProfileId { get; set; } = Guid.NewGuid().ToString();
        public string ProfileName { get; set; } = "Default Profile";
        public string PrinterDeviceName { get; set; } = string.Empty;
        public string TrayType { get; set; } = "EPSON_L805_TRAY";
        public string AttachmentType { get; set; } = "AftermarketTray"; // AftermarketTray, ManufacturerAttachment, CustomTray, DirectCR80
        public string Description { get; set; } = string.Empty;

        public double OffsetXMm { get; set; } = 0.0;
        public double OffsetYMm { get; set; } = 0.0;
        public double CardGapMm { get; set; } = 5.60;
        public int CardsPerTray { get; set; } = 2;
        public double TrayWidthMm { get; set; } = 140.0;
        public double TrayHeightMm { get; set; } = 297.0;

        public int TargetDpi { get; set; } = 600;
        public bool MirrorMode { get; set; } = false;
        public bool IsDefault { get; set; } = false;
        public bool IsPhysicallyVerified { get; set; } = false;
        public DateTime? LastCalibratedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
