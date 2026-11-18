using System;

namespace PVCStudioPro.Core.Models
{
    public class PrinterProfile
    {
        public string ProfileId { get; set; } = Guid.NewGuid().ToString();
        public string ProfileName { get; set; } = "Default Profile";
        public string PrinterDeviceName { get; set; } = string.Empty;
        public string TrayType { get; set; } = "EPSON_2CARD_TRAY";
        public double OffsetXMm { get; set; } = 0.0;
        public double OffsetYMm { get; set; } = 0.0;
        public double CardGapMm { get; set; } = 5.60;
        public int TargetDpi { get; set; } = 600;
        public bool MirrorMode { get; set; } = false;
        public bool IsDefault { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
