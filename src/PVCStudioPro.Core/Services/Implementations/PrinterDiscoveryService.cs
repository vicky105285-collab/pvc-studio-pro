using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    [SupportedOSPlatform("windows")]
    public class PrinterDiscoveryService : IPrinterDiscoveryService
    {
        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int DeviceCapabilities(string pDevice, string pPort, short fwCapability, IntPtr pOutput, IntPtr pDevMode);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string? lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private const short DC_PAPERNAMES = 16;
        private const short DC_BINNAMES = 12;
        private const short DC_DUPLEX = 7;
        private const short DC_COLORDEVICE = 32;

        private const int PHYSICALOFFSETX = 112;
        private const int PHYSICALOFFSETY = 113;
        private const int LOGPIXELSX = 88;
        private const int LOGPIXELSY = 90;

        public Task<IReadOnlyList<PrinterCapabilities>> GetInstalledPrintersAsync()
        {
            return Task.Run<IReadOnlyList<PrinterCapabilities>>(() =>
            {
                var list = new List<PrinterCapabilities>();
                try
                {
                    foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                    {
                        list.Add(QueryPrinterCapabilities(printer));
                    }
                }
                catch
                {
                    // Fallback to basic list if spooler is busy
                }
                return list;
            });
        }

        public PrinterCapabilities QueryPrinterCapabilities(string printerName, string portName = "")
        {
            var caps = new PrinterCapabilities { PrinterName = printerName, PortName = portName };

            try
            {
                // 1. Color Support
                caps.IsColor = (DeviceCapabilities(printerName, portName, DC_COLORDEVICE, IntPtr.Zero, IntPtr.Zero) == 1);

                // 2. Duplex Support
                caps.SupportsDuplex = (DeviceCapabilities(printerName, portName, DC_DUPLEX, IntPtr.Zero, IntPtr.Zero) == 1);

                // 3. Supported Paper Sizes
                int numPapers = DeviceCapabilities(printerName, portName, DC_PAPERNAMES, IntPtr.Zero, IntPtr.Zero);
                if (numPapers > 0)
                {
                    IntPtr pNames = Marshal.AllocHGlobal(numPapers * 64 * Marshal.SystemDefaultCharSize);
                    try
                    {
                        if (DeviceCapabilities(printerName, portName, DC_PAPERNAMES, pNames, IntPtr.Zero) > 0)
                        {
                            for (int i = 0; i < numPapers; i++)
                            {
                                IntPtr strPtr = new IntPtr(pNames.ToInt64() + i * 64 * Marshal.SystemDefaultCharSize);
                                string? paperName = Marshal.PtrToStringAuto(strPtr);
                                if (!string.IsNullOrEmpty(paperName))
                                {
                                    caps.SupportedPaperNames.Add(paperName.TrimEnd('\0'));
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pNames);
                    }
                }

                // 4. Supported Trays / Input Bins
                int numBins = DeviceCapabilities(printerName, portName, DC_BINNAMES, IntPtr.Zero, IntPtr.Zero);
                if (numBins > 0)
                {
                    IntPtr pBins = Marshal.AllocHGlobal(numBins * 24 * Marshal.SystemDefaultCharSize);
                    try
                    {
                        if (DeviceCapabilities(printerName, portName, DC_BINNAMES, pBins, IntPtr.Zero) > 0)
                        {
                            for (int i = 0; i < numBins; i++)
                            {
                                IntPtr strPtr = new IntPtr(pBins.ToInt64() + i * 24 * Marshal.SystemDefaultCharSize);
                                string? binName = Marshal.PtrToStringAuto(strPtr);
                                if (!string.IsNullOrEmpty(binName))
                                {
                                    caps.SupportedTrayNames.Add(binName.TrimEnd('\0'));
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pBins);
                    }
                }

                // 5. Hardware Margins & DPI
                IntPtr hdc = CreateDC("winspool", printerName, null, IntPtr.Zero);
                if (hdc != IntPtr.Zero)
                {
                    try
                    {
                        caps.HardwareDpiX = GetDeviceCaps(hdc, LOGPIXELSX);
                        caps.HardwareDpiY = GetDeviceCaps(hdc, LOGPIXELSY);
                        int offX = GetDeviceCaps(hdc, PHYSICALOFFSETX);
                        int offY = GetDeviceCaps(hdc, PHYSICALOFFSETY);
                        if (caps.HardwareDpiX > 0)
                            caps.PhysicalMarginLeftMm = (offX * 25.4) / caps.HardwareDpiX;
                        if (caps.HardwareDpiY > 0)
                            caps.PhysicalMarginTopMm = (offY * 25.4) / caps.HardwareDpiY;
                    }
                    finally
                    {
                        DeleteDC(hdc);
                    }
                }

                // 6. Device Classification
                string nameLower = printerName.ToLower();
                if (nameLower.Contains("l8050") || nameLower.Contains("l18050"))
                    caps.DetectedCategory = "EPSON_ECOTANK_PHOTO_NEW_SERIES";
                else if (nameLower.Contains("l805") || nameLower.Contains("l800") || nameLower.Contains("l850"))
                    caps.DetectedCategory = "EPSON_CD_DVD_TRAY_CLASSIC";
                else if (nameLower.Contains("canon") && (nameLower.Contains("g570") || nameLower.Contains("g670") || nameLower.Contains("disc")))
                    caps.DetectedCategory = "CANON_MULTI_TRAY_SERIES";
                else if (nameLower.Contains("zebra") || nameLower.Contains("evolis") || nameLower.Contains("magicard") || nameLower.Contains("fargo"))
                    caps.DetectedCategory = "CR80_DIRECT_CARD_PRINTER";
                else if (nameLower.Contains("pdf"))
                    caps.DetectedCategory = "VIRTUAL_PDF_SPOOLER";
                else
                    caps.DetectedCategory = "GENERIC_WINDOWS_PRINTER";
            }
            catch
            {
                // Graceful fallback for non-responsive printers
            }

            return caps;
        }

        public bool ValidateMediaTrayMatch(PrinterCapabilities caps, string selectedTray)
        {
            if (caps.DetectedCategory == "VIRTUAL_PDF_SPOOLER") return true;

            // If selected tray is CD/DVD tray, ensure the driver is not set to standard plain letter/A4
            if (selectedTray == "EPSON_2CARD_TRAY")
            {
                return caps.DetectedCategory.StartsWith("EPSON");
            }
            if (selectedTray == "CANON_MULTI_TRAY")
            {
                return caps.DetectedCategory.StartsWith("CANON");
            }
            return true;
        }
    }
}
