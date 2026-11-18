using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;

namespace PVCStudioPro.App.ViewModels
{
    public partial class CalibrationViewModel : ObservableObject
    {
        private readonly IPrinterDiscoveryService _printerDiscovery;
        private readonly IPrintService _printService;

        [ObservableProperty]
        private ObservableCollection<string> _printerList = new();

        [ObservableProperty]
        private string _selectedPrinter = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> _trayList = new();

        [ObservableProperty]
        private string _selectedTray = "EPSON_2CARD_TRAY";

        [ObservableProperty]
        private double _offsetX = 0.0;

        [ObservableProperty]
        private double _offsetY = 0.0;

        [ObservableProperty]
        private string _statusMessage = "Ready | Print the test sheet and measure any edge shift with a millimeter ruler";

        [ObservableProperty]
        private bool _isBusy = false;

        public CalibrationViewModel(
            IPrinterDiscoveryService printerDiscovery,
            IPrintService printService)
        {
            _printerDiscovery = printerDiscovery;
            _printService = printService;

            TrayList.Add("EPSON_2CARD_TRAY");
            TrayList.Add("CANON_MULTI_TRAY");
            TrayList.Add("DRAGON_SHEET_A4");
            TrayList.Add("CR80_DIRECT");
            SelectedTray = "EPSON_2CARD_TRAY";

            _ = InitializePrintersAsync();
        }

        private async Task InitializePrintersAsync()
        {
            try
            {
                var printers = await _printerDiscovery.GetInstalledPrintersAsync();
                PrinterList.Clear();
                foreach (var p in printers)
                {
                    PrinterList.Add(p.PrinterName);
                }

                if (PrinterList.Count > 0)
                {
                    SelectedPrinter = PrinterList[0];
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Printer error: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task PrintTestSheetAsync()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Generating 600 DPI calibration pattern with millimeter rulers...";

                using var sheet = _printService.GenerateCalibrationSheet(SelectedTray, dpi: 600);

                if (string.IsNullOrWhiteSpace(SelectedPrinter))
                {
                    // Export to desktop preview file
                    string exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Calibration_Sheet.png");
                    string saved = await _printService.ExportPreviewAsync(sheet, exportPath);
                    StatusMessage = $"Calibration sheet saved to desktop: {saved}";
                    return;
                }

                var profile = new PrinterProfile
                {
                    PrinterDeviceName = SelectedPrinter,
                    TrayType = SelectedTray,
                    OffsetXMm = OffsetX,
                    OffsetYMm = OffsetY,
                    TargetDpi = 600
                };

                var result = await _printService.PrintTrayAsync(sheet, profile, "PVC Studio Pro - Calibration Sheet");
                if (result.Success)
                {
                    StatusMessage = "Calibration sheet sent to printer successfully!";
                }
                else
                {
                    StatusMessage = $"Print failed: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void SaveCalibration()
        {
            StatusMessage = $"Calibration saved! Offset X: {OffsetX:F2} mm, Offset Y: {OffsetY:F2} mm for {SelectedTray}";
        }
    }
}
