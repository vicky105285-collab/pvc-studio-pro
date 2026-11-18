using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class PrintService : IPrintService
    {
        private const double MmPerInch = 25.4;

        #region Win32 GDI & Winspool P/Invoke
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DOCINFO
        {
            public int cbSize;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpszDocName;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string? lpszOutput;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string? lpszDatatype;
            public int fwType;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string? lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int StartDoc(IntPtr hdc, ref DOCINFO lpdi);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int EndDoc(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int StartPage(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int EndPage(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int StretchDIBits(
            IntPtr hdc,
            int xDest, int yDest, int DestWidth, int DestHeight,
            int xSrc, int ySrc, int SrcWidth, int SrcHeight,
            IntPtr lpBits,
            ref BITMAPINFOHEADER lpbmi,
            uint iUsage,
            uint rop);

        private const int BI_RGB = 0;
        private const uint DIB_RGB_COLORS = 0;
        private const uint SRCCOPY = 0x00CC0020;
        #endregion

        public SKBitmap ComposeTrayCanvas(
            SKBitmap card1,
            SKBitmap? card2,
            PrinterProfile profile,
            TrayLayoutMode layoutMode = TrayLayoutMode.BothCards)
        {
            int dpi = profile.TargetDpi > 0 ? profile.TargetDpi : 600;
            double trayWidthMm;
            double trayHeightMm;
            double slot1XMm;
            double slot1YMm;
            double slot2XMm;
            double slot2YMm;

            switch (profile.TrayType.ToUpperInvariant())
            {
                case "CANON_MULTI_TRAY":
                    trayWidthMm = 130.0;
                    trayHeightMm = 254.0;
                    slot1XMm = 22.20 + profile.OffsetXMm;
                    slot1YMm = 25.00 + profile.OffsetYMm;
                    slot2XMm = 22.20 + profile.OffsetXMm;
                    slot2YMm = 95.00 + profile.OffsetYMm;
                    break;

                case "CR80_DIRECT":
                    trayWidthMm = 85.60;
                    trayHeightMm = 53.98;
                    slot1XMm = 0.0 + profile.OffsetXMm;
                    slot1YMm = 0.0 + profile.OffsetYMm;
                    slot2XMm = 0.0;
                    slot2YMm = 0.0;
                    break;

                case "DRAGON_SHEET_A4":
                    trayWidthMm = 210.0;
                    trayHeightMm = 297.0;
                    slot1XMm = 14.0 + profile.OffsetXMm;
                    slot1YMm = 15.0 + profile.OffsetYMm;
                    slot2XMm = 110.0 + profile.OffsetXMm;
                    slot2YMm = 15.0 + profile.OffsetYMm;
                    break;

                case "EPSON_2CARD_TRAY":
                default:
                    // Standard Epson CD/DVD 2-Card Tray (L805, L8050, L18050)
                    trayWidthMm = 140.0;
                    trayHeightMm = 297.0;
                    slot1XMm = 27.20 + profile.OffsetXMm;
                    slot1YMm = 18.00 + profile.OffsetYMm;
                    slot2XMm = 27.20 + profile.OffsetXMm;
                    slot2YMm = 18.00 + 54.00 + profile.CardGapMm + profile.OffsetYMm;
                    break;
            }

            int trayWidthPx = (int)Math.Round((trayWidthMm * dpi) / MmPerInch);
            int trayHeightPx = (int)Math.Round((trayHeightMm * dpi) / MmPerInch);

            var trayBitmap = new SKBitmap(trayWidthPx, trayHeightPx, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(trayBitmap);
            canvas.Clear(SKColors.White);

            int cardWidthPx = (int)Math.Round((85.60 * dpi) / MmPerInch);
            int cardHeightPx = (int)Math.Round((53.98 * dpi) / MmPerInch);

            // Draw Slot 1 (Card 1)
            if (layoutMode == TrayLayoutMode.BothCards || layoutMode == TrayLayoutMode.Card1Only)
            {
                int s1X = (int)Math.Round((slot1XMm * dpi) / MmPerInch);
                int s1Y = (int)Math.Round((slot1YMm * dpi) / MmPerInch);
                DrawCardOnCanvas(canvas, card1, s1X, s1Y, cardWidthPx, cardHeightPx, profile.MirrorMode);
            }

            // Draw Slot 2 (Card 2)
            if ((layoutMode == TrayLayoutMode.BothCards || layoutMode == TrayLayoutMode.Card2Only) && card2 != null)
            {
                int s2X = (int)Math.Round((slot2XMm * dpi) / MmPerInch);
                int s2Y = (int)Math.Round((slot2YMm * dpi) / MmPerInch);
                DrawCardOnCanvas(canvas, card2, s2X, s2Y, cardWidthPx, cardHeightPx, profile.MirrorMode);
            }

            canvas.Flush();
            return trayBitmap;
        }

        private static void DrawCardOnCanvas(
            SKCanvas canvas,
            SKBitmap card,
            int x,
            int y,
            int width,
            int height,
            bool mirrorMode)
        {
            var destRect = new SKRect(x, y, x + width, y + height);

            if (mirrorMode)
            {
                canvas.Save();
                canvas.Translate(x + (width / 2f), y + (height / 2f));
                canvas.Scale(-1, 1);
                canvas.Translate(-(x + (width / 2f)), -(y + (height / 2f)));
            }

            canvas.DrawBitmap(card, destRect, new SKSamplingOptions(SKCubicResampler.Mitchell));

            if (mirrorMode)
            {
                canvas.Restore();
            }
        }

        public SKBitmap GenerateCalibrationSheet(string trayType, int dpi = 600)
        {
            var tempProfile = new PrinterProfile
            {
                TrayType = trayType,
                TargetDpi = dpi,
                OffsetXMm = 0.0,
                OffsetYMm = 0.0
            };

            int cardWidthPx = (int)Math.Round((85.60 * dpi) / MmPerInch);
            int cardHeightPx = (int)Math.Round((53.98 * dpi) / MmPerInch);

            // Generate a calibration pattern card with millimeter markings and crosshairs
            using var patternCard = new SKBitmap(cardWidthPx, cardHeightPx, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var pCanvas = new SKCanvas(patternCard))
            {
                pCanvas.Clear(SKColors.White);

                using var borderPaint = new SKPaint
                {
                    Color = SKColors.DarkBlue,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 3,
                    IsAntialias = true
                };

                // Draw exact CR80 outer boundary with 3.18mm rounded corners
                float rPx = (float)((3.18 * dpi) / MmPerInch);
                pCanvas.DrawRoundRect(new SKRect(2, 2, cardWidthPx - 2, cardHeightPx - 2), rPx, rPx, borderPaint);

                // Draw Center Crosshair
                using var crossPaint = new SKPaint
                {
                    Color = SKColors.Red,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2,
                    IsAntialias = true
                };
                pCanvas.DrawLine(cardWidthPx / 2f, 0, cardWidthPx / 2f, cardHeightPx, crossPaint);
                pCanvas.DrawLine(0, cardHeightPx / 2f, cardWidthPx, cardHeightPx / 2f, crossPaint);

                // Millimeter Ticks along top and left edges
                using var tickPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1,
                    IsAntialias = true
                };

                using var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal), 16.0f * (dpi / 300.0f));
                using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

                for (int mm = 0; mm <= 85; mm++)
                {
                    float tx = (float)((mm * dpi) / MmPerInch);
                    float tLen = (mm % 10 == 0) ? 20 : ((mm % 5 == 0) ? 12 : 6);
                    pCanvas.DrawLine(tx, 0, tx, tLen, tickPaint);

                    if (mm % 10 == 0 && mm > 0 && mm < 85)
                    {
                        pCanvas.DrawText(mm.ToString(), tx - 10, tLen + 20, SKTextAlign.Left, font, textPaint);
                    }
                }

                for (int mm = 0; mm <= 53; mm++)
                {
                    float ty = (float)((mm * dpi) / MmPerInch);
                    float tLen = (mm % 10 == 0) ? 20 : ((mm % 5 == 0) ? 12 : 6);
                    pCanvas.DrawLine(0, ty, tLen, ty, tickPaint);

                    if (mm % 10 == 0 && mm > 0 && mm < 53)
                    {
                        pCanvas.DrawText(mm.ToString(), tLen + 5, ty + 6, SKTextAlign.Left, font, textPaint);
                    }
                }

                pCanvas.Flush();
            }

            return ComposeTrayCanvas(patternCard, patternCard, tempProfile, TrayLayoutMode.BothCards);
        }

        public async Task<string> ExportPreviewAsync(SKBitmap trayBitmap, string outputPath)
        {
            return await Task.Run(() =>
            {
                string? dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using var image = SKImage.FromBitmap(trayBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(outputPath);
                data.SaveTo(stream);

                return Path.GetFullPath(outputPath);
            });
        }

        public Task<PrintResult> PrintTrayAsync(
            SKBitmap trayBitmap,
            PrinterProfile profile,
            string documentTitle = "PVC Studio Pro Card Print")
        {
            return Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(profile.PrinterDeviceName))
                {
                    return new PrintResult
                    {
                        Success = false,
                        ErrorMessage = "No printer device specified in profile."
                    };
                }

                IntPtr hdc = CreateDC("winspool", profile.PrinterDeviceName, null, IntPtr.Zero);
                if (hdc == IntPtr.Zero)
                {
                    return new PrintResult
                    {
                        Success = false,
                        ErrorMessage = $"Failed to acquire device context for printer '{profile.PrinterDeviceName}'."
                    };
                }

                try
                {
                    var docInfo = new DOCINFO
                    {
                        cbSize = Marshal.SizeOf<DOCINFO>(),
                        lpszDocName = documentTitle,
                        lpszOutput = null,
                        lpszDatatype = "RAW"
                    };

                    int printJobId = StartDoc(hdc, ref docInfo);
                    if (printJobId <= 0)
                    {
                        return new PrintResult
                        {
                            Success = false,
                            ErrorMessage = $"StartDoc failed with Win32 error code {Marshal.GetLastWin32Error()}."
                        };
                    }

                    try
                    {
                        if (StartPage(hdc) <= 0)
                        {
                            return new PrintResult
                            {
                                Success = false,
                                ErrorMessage = $"StartPage failed with Win32 error code {Marshal.GetLastWin32Error()}."
                            };
                        }

                        // Prepare DIB for StretchDIBits
                        var bmi = new BITMAPINFOHEADER
                        {
                            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                            biWidth = trayBitmap.Width,
                            biHeight = -trayBitmap.Height, // Top-down DIB
                            biPlanes = 1,
                            biBitCount = 32,
                            biCompression = BI_RGB,
                            biSizeImage = trayBitmap.ByteCount
                        };

                        IntPtr pPixels = trayBitmap.GetPixels();
                        int result = StretchDIBits(
                            hdc,
                            0, 0, trayBitmap.Width, trayBitmap.Height,
                            0, 0, trayBitmap.Width, trayBitmap.Height,
                            pPixels,
                            ref bmi,
                            DIB_RGB_COLORS,
                            SRCCOPY);

                        EndPage(hdc);
                        EndDoc(hdc);

                        return new PrintResult
                        {
                            Success = (result != -1),
                            JobId = printJobId.ToString(),
                            ErrorMessage = (result == -1) ? "StretchDIBits returned error." : null
                        };
                    }
                    catch (Exception ex)
                    {
                        EndDoc(hdc);
                        return new PrintResult
                        {
                            Success = false,
                            ErrorMessage = $"Print execution exception: {ex.Message}"
                        };
                    }
                }
                finally
                {
                    DeleteDC(hdc);
                }
            });
        }
    }
}
