using System.Collections.Generic;

namespace PVCStudioPro.Core.Models
{
    public class PrinterCapabilities
    {
        public string PrinterName { get; set; } = string.Empty;
        public string PortName { get; set; } = string.Empty;
        public string DetectedCategory { get; set; } = "GENERIC_WINDOWS_PRINTER";
        public bool IsColor { get; set; } = true;
        public bool SupportsDuplex { get; set; } = false;
        public int HardwareDpiX { get; set; } = 600;
        public int HardwareDpiY { get; set; } = 600;
        public double PhysicalMarginLeftMm { get; set; } = 0.0;
        public double PhysicalMarginTopMm { get; set; } = 0.0;
        public List<string> SupportedPaperNames { get; set; } = new();
        public List<string> SupportedTrayNames { get; set; } = new();
    }
}
