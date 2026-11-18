$code = @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public class PrinterCapabilityDiscovery {
    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int DeviceCapabilities(string pDevice, string pPort, short fwCapability, IntPtr pOutput, IntPtr pDevMode);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    public const short DC_PAPERS = 2;
    public const short DC_PAPERSIZE = 3;
    public const short DC_MINEXTENT = 4;
    public const short DC_MAXEXTENT = 5;
    public const short DC_BINS = 6;
    public const short DC_DUPLEX = 7;
    public const short DC_BINNAMES = 12;
    public const short DC_PAPERNAMES = 16;
    public const short DC_COLORDEVICE = 32;

    public const int PHYSICALOFFSETX = 112;
    public const int PHYSICALOFFSETY = 113;
    public const int LOGPIXELSX = 88;
    public const int LOGPIXELSY = 90;

    public class DynamicPrinterCaps {
        public string PrinterName;
        public int PaperCount;
        public List<string> PaperNames;
        public int BinCount;
        public List<string> BinNames;
        public bool SupportsDuplex;
        public bool IsColor;
        public int PhysicalDpiX;
        public int PhysicalDpiY;
        public int PhysicalMarginLeftPx;
        public int PhysicalMarginTopPx;
        public double PhysicalMarginLeftMm;
        public double PhysicalMarginTopMm;
        public string DetectedCategory;

        public DynamicPrinterCaps() {
            PaperNames = new List<string>();
            BinNames = new List<string>();
        }
    }

    public static DynamicPrinterCaps InspectPrinter(string printerName, string portName) {
        DynamicPrinterCaps caps = new DynamicPrinterCaps();
        caps.PrinterName = printerName;

        int isColor = DeviceCapabilities(printerName, portName, DC_COLORDEVICE, IntPtr.Zero, IntPtr.Zero);
        caps.IsColor = (isColor == 1);

        int duplex = DeviceCapabilities(printerName, portName, DC_DUPLEX, IntPtr.Zero, IntPtr.Zero);
        caps.SupportsDuplex = (duplex == 1);

        int numPapers = DeviceCapabilities(printerName, portName, DC_PAPERNAMES, IntPtr.Zero, IntPtr.Zero);
        caps.PaperCount = numPapers;
        if (numPapers > 0) {
            IntPtr pNames = Marshal.AllocHGlobal(numPapers * 64 * Marshal.SystemDefaultCharSize);
            try {
                if (DeviceCapabilities(printerName, portName, DC_PAPERNAMES, pNames, IntPtr.Zero) > 0) {
                    for (int i = 0; i < numPapers; i++) {
                        IntPtr strPtr = new IntPtr(pNames.ToInt64() + i * 64 * Marshal.SystemDefaultCharSize);
                        string paperName = Marshal.PtrToStringAuto(strPtr);
                        if (!string.IsNullOrEmpty(paperName)) {
                            caps.PaperNames.Add(paperName.TrimEnd('\0'));
                        }
                    }
                }
            } finally {
                Marshal.FreeHGlobal(pNames);
            }
        }

        int numBins = DeviceCapabilities(printerName, portName, DC_BINNAMES, IntPtr.Zero, IntPtr.Zero);
        caps.BinCount = numBins;
        if (numBins > 0) {
            IntPtr pBins = Marshal.AllocHGlobal(numBins * 24 * Marshal.SystemDefaultCharSize);
            try {
                if (DeviceCapabilities(printerName, portName, DC_BINNAMES, pBins, IntPtr.Zero) > 0) {
                    for (int i = 0; i < numBins; i++) {
                        IntPtr strPtr = new IntPtr(pBins.ToInt64() + i * 24 * Marshal.SystemDefaultCharSize);
                        string binName = Marshal.PtrToStringAuto(strPtr);
                        if (!string.IsNullOrEmpty(binName)) {
                            caps.BinNames.Add(binName.TrimEnd('\0'));
                        }
                    }
                }
            } finally {
                Marshal.FreeHGlobal(pBins);
            }
        }

        IntPtr hdc = CreateDC("winspool", printerName, null, IntPtr.Zero);
        if (hdc != IntPtr.Zero) {
            try {
                caps.PhysicalDpiX = GetDeviceCaps(hdc, LOGPIXELSX);
                caps.PhysicalDpiY = GetDeviceCaps(hdc, LOGPIXELSY);
                caps.PhysicalMarginLeftPx = GetDeviceCaps(hdc, PHYSICALOFFSETX);
                caps.PhysicalMarginTopPx = GetDeviceCaps(hdc, PHYSICALOFFSETY);
                if (caps.PhysicalDpiX > 0)
                    caps.PhysicalMarginLeftMm = (caps.PhysicalMarginLeftPx * 25.4) / caps.PhysicalDpiX;
                if (caps.PhysicalDpiY > 0)
                    caps.PhysicalMarginTopMm = (caps.PhysicalMarginTopPx * 25.4) / caps.PhysicalDpiY;
            } finally {
                DeleteDC(hdc);
            }
        }

        string nameLower = printerName.ToLower();
        if (nameLower.Contains("l8050") || nameLower.Contains("l18050")) {
            caps.DetectedCategory = "EPSON_ECOTANK_PHOTO_NEW_SERIES";
        } else if (nameLower.Contains("l805") || nameLower.Contains("l800") || nameLower.Contains("l850")) {
            caps.DetectedCategory = "EPSON_CD_DVD_TRAY_CLASSIC";
        } else if (nameLower.Contains("canon") && (nameLower.Contains("g570") || nameLower.Contains("g670") || nameLower.Contains("disc"))) {
            caps.DetectedCategory = "CANON_MULTI_TRAY_SERIES";
        } else if (nameLower.Contains("zebra") || nameLower.Contains("evolis") || nameLower.Contains("magicard") || nameLower.Contains("fargo")) {
            caps.DetectedCategory = "CR80_DIRECT_CARD_PRINTER";
        } else if (nameLower.Contains("pdf")) {
            caps.DetectedCategory = "VIRTUAL_PDF_SPOOLER";
        } else {
            caps.DetectedCategory = "GENERIC_WINDOWS_PRINTER";
        }

        return caps;
    }
}
"@

Add-Type -TypeDefinition $code -Language CSharp

$printers = Get-Printer
Write-Host "=== DYNAMIC PRINTER CAPABILITY DISCOVERY REPORT ==="
foreach ($p in $printers) {
    $caps = [PrinterCapabilityDiscovery]::InspectPrinter($p.Name, $p.PortName)
    Write-Host "`nPrinter Name: $($caps.PrinterName)"
    Write-Host "Detected Category: $($caps.DetectedCategory)"
    Write-Host "Color Support: $($caps.IsColor)"
    Write-Host "Duplex Support: $($caps.SupportsDuplex)"
    Write-Host "Hardware DPI: $($caps.PhysicalDpiX) x $($caps.PhysicalDpiY)"
    Write-Host "Hardware Margins: Left: $($caps.PhysicalMarginLeftMm.ToString('F2')) mm, Top: $($caps.PhysicalMarginTopMm.ToString('F2')) mm"
    Write-Host "Supported Paper Sizes: $($caps.PaperCount)"
    if ($caps.PaperCount -gt 0) {
        $samplePapers = $caps.PaperNames | Select-Object -First 6
        Write-Host "  Sample Sizes: $($samplePapers -join ', ')..."
    }
    Write-Host "Supported Bins/Trays: $($caps.BinCount)"
    if ($caps.BinCount -gt 0) {
        Write-Host "  Tray Names: $($caps.BinNames -join ', ')"
    } else {
        Write-Host "  Tray Names: None reported directly by driver"
    }
}
