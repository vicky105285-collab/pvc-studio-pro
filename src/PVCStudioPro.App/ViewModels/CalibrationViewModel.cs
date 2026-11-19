using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        private readonly IPrinterTrayService _trayService;

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
        private bool _isPhysicallyVerified = false;

        [ObservableProperty]
        private string _statusMessage = "Ready | Print the calibration test sheet and measure any edge shift with a precision caliper or millimeter ruler.";

        [ObservableProperty]
        private bool _isBusy = false;

        public CalibrationViewModel(
            IPrinterDiscoveryService printerDiscovery,
            IPrintService printService,
            IPrinterTrayService trayService)
        {
            _printerDiscovery = printerDiscovery;
            _printService = printService;
            _trayService = trayService;

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
                await _trayService.InitializeAsync();
                var printers = await _printerDiscovery.GetInstalledPrintersAsync();
                PrinterList.Clear();
                foreach (var p in printers)
                {
                    PrinterList.Add(p.PrinterName);
                }

                if (PrinterList.Count > 0)
                {
                    SelectedPrinter = PrinterList[0];
                    await LoadExistingCalibrationAsync();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Printer discovery error: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task LoadExistingCalibrationAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedPrinter)) return;

            try
            {
                var profiles = await _trayService.GetProfilesForPrinterAsync(SelectedPrinter);
                var match = profiles.FirstOrDefault(p => p.TrayType == SelectedTray);
                if (match != null)
                {
                    OffsetX = match.OffsetXMm;
                    OffsetY = match.OffsetYMm;
                    IsPhysicallyVerified = match.IsPhysicallyVerified;
                    StatusMessage = $"Loaded profile '{match.ProfileName}' (Offset X: {OffsetX:F2}mm, Y: {OffsetY:F2}mm, Verified: {IsPhysicallyVerified}).";
                }
                else
                {
                    OffsetX = 0.0;
                    OffsetY = 0.0;
                    IsPhysicallyVerified = false;
                    StatusMessage = $"Default zero offset for {SelectedTray} on {SelectedPrinter}.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load profile error: {ex.Message}";
            }
        }

        [RelayCommand]
        public void NudgeOffset(string direction)
        {
            switch (direction)
            {
                case "LEFT":
                    OffsetX = Math.Round(OffsetX - 0.10, 2);
                    break;
                case "RIGHT":
                    OffsetX = Math.Round(OffsetX + 0.10, 2);
                    break;
                case "UP":
                    OffsetY = Math.Round(OffsetY - 0.10, 2);
                    break;
                case "DOWN":
                    OffsetY = Math.Round(OffsetY + 0.10, 2);
                    break;
                case "RESET":
                    OffsetX = 0.0;
                    OffsetY = 0.0;
                    break;
            }
        }

        [RelayCommand]
        public async Task PrintTestSheetAsync()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Generating 600 DPI calibration pattern with millimeter vernier scales...";

                using var sheet = _printService.GenerateCalibrationSheet(SelectedTray, dpi: 600);

                if (string.IsNullOrWhiteSpace(SelectedPrinter))
                {
                    string exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Calibration_Sheet.png");
                    string saved = await _printService.ExportPreviewAsync(sheet, exportPath);
                    StatusMessage = $"Physical printer not selected. Calibration sheet exported to desktop: {saved}";
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
                    StatusMessage = "Calibration sheet sent to printer successfully! Check alignment with caliper.";
                }
                else
                {
                    StatusMessage = $"Print failed: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Print error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task SaveCalibrationAsync()
        {
            try
            {
                IsBusy = true;
                var profiles = await _trayService.GetProfilesForPrinterAsync(SelectedPrinter);
                var match = profiles.FirstOrDefault(p => p.TrayType == SelectedTray);

                if (match != null)
                {
                    match.OffsetXMm = OffsetX;
                    match.OffsetYMm = OffsetY;
                    match.IsPhysicallyVerified = IsPhysicallyVerified;
                    await _trayService.SaveProfileAsync(match);
                }
                else
                {
                    var newProfile = new PrinterProfile
                    {
                        ProfileName = $"{SelectedPrinter} - {SelectedTray}",
                        PrinterDeviceName = SelectedPrinter,
                        TrayType = SelectedTray,
                        OffsetXMm = OffsetX,
                        OffsetYMm = OffsetY,
                        IsPhysicallyVerified = IsPhysicallyVerified,
                        TargetDpi = 600
                    };
                    await _trayService.SaveProfileAsync(newProfile);
                }

                StatusMessage = $"Calibration successfully saved to database! Offset X: {OffsetX:F2} mm, Y: {OffsetY:F2} mm for {SelectedTray} on {SelectedPrinter}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
