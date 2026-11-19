using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SkiaSharp;
using PVCStudioPro.App.Converters;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;

namespace PVCStudioPro.App.ViewModels
{
    public partial class PhotoStudioViewModel : ObservableObject
    {
        private readonly IVisionService _visionService;
        private readonly IPassportPhotoEngine _passportEngine;
        private readonly IPrintService _printService;
        private readonly IJobLedgerService _jobLedger;

        [ObservableProperty]
        private BitmapSource? _singlePhotoPreview;

        [ObservableProperty]
        private BitmapSource? _sheetPreview;

        [ObservableProperty]
        private string _photoPath = string.Empty;

        [ObservableProperty]
        private bool _isRemoveBackground = true;

        [ObservableProperty]
        private string _selectedBackground = "WHITE"; // WHITE, BLUE, GREY, CREAM

        [ObservableProperty]
        private int _photoCount = 8; // 8 (4x6"), 16 (A4), 32 (A4)

        [ObservableProperty]
        private string _selectedPaper = "4x6_PHOTO_PAPER"; // 4x6_PHOTO_PAPER, A4_PAPER

        [ObservableProperty]
        private bool _showBiometricGuide = true;

        [ObservableProperty]
        private decimal _sheetPrice = 50.00m;

        [ObservableProperty]
        private string _paymentMode = "CASH";

        [ObservableProperty]
        private double _panOffsetX = 0.0;

        [ObservableProperty]
        private double _panOffsetY = 0.0;

        [ObservableProperty]
        private double _zoomScale = 1.0;

        [ObservableProperty]
        private System.Collections.Generic.List<string> _availablePrinters = new();

        [ObservableProperty]
        private string _selectedPrinter = "Default Photo Printer";

        [ObservableProperty]
        private string _statusMessage = "Ready | Load customer portrait photograph to generate passport photo sheet";

        [ObservableProperty]
        private bool _isProcessing = false;

        [ObservableProperty]
        private bool _hasLoadedPhoto = false;

        private SKBitmap? _originalPhoto;
        private SKBitmap? _processedSinglePhoto;
        private SKBitmap? _composedSheet;

        public PhotoStudioViewModel(
            IVisionService visionService,
            IPassportPhotoEngine passportEngine,
            IPrintService printService,
            IJobLedgerService jobLedger)
        {
            _visionService = visionService;
            _passportEngine = passportEngine;
            _printService = printService;
            _jobLedger = jobLedger;

            LoadPrinters();
        }

        private void LoadPrinters()
        {
            try
            {
                var list = new System.Collections.Generic.List<string>();
                foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    list.Add(printer);
                }

                if (list.Count > 0)
                {
                    AvailablePrinters = list;
                    SelectedPrinter = list[0];
                }
                else
                {
                    AvailablePrinters = new System.Collections.Generic.List<string> { "Default Photo Printer", "Virtual Tray Spooler" };
                    SelectedPrinter = "Default Photo Printer";
                }
            }
            catch
            {
                AvailablePrinters = new System.Collections.Generic.List<string> { "Default Photo Printer", "Virtual Tray Spooler" };
                SelectedPrinter = "Default Photo Printer";
            }
        }

        [RelayCommand]
        public async Task LoadPhotoAsync()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Customer Photographs (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All Files (*.*)|*.*",
                Title = "Select Customer Photograph"
            };

            if (dlg.ShowDialog() == true)
            {
                await ProcessPhotoFileAsync(dlg.FileName);
            }
        }

        public async Task ProcessPhotoFileAsync(string filePath)
        {
            try
            {
                IsProcessing = true;
                StatusMessage = "Analyzing portrait and processing 35x45 mm passport frame...";
                PhotoPath = filePath;

                byte[] bytes = await File.ReadAllBytesAsync(filePath);
                using var stream = new MemoryStream(bytes);

                _originalPhoto?.Dispose();
                _originalPhoto = SKBitmap.Decode(stream);

                if (_originalPhoto == null)
                {
                    StatusMessage = "Failed to decode image file. Please provide a valid JPG or PNG portrait.";
                    return;
                }

                await RegeneratePassportPhotosAsync();

                HasLoadedPhoto = true;
                StatusMessage = $"Passport photo processed (35x45 mm, 600 DPI, {PhotoCount}-up sheet ready).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Photo processing error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task RegeneratePassportPhotosAsync()
        {
            if (_originalPhoto == null) return;

            SKColor bgColor = SelectedBackground switch
            {
                "BLUE" => new SKColor(74, 144, 226),  // Studio Passport Blue
                "GREY" => new SKColor(230, 230, 230), // Off-White Studio Grey
                "CREAM" => new SKColor(254, 252, 232), // Studio Warm Cream
                _ => SKColors.White                   // Studio Pure White
            };

            var options = new PassportPhotoOptions
            {
                BackgroundColor = bgColor,
                RemoveBackground = IsRemoveBackground,
                PanOffsetXRatio = PanOffsetX,
                PanOffsetYRatio = PanOffsetY,
                ZoomScale = ZoomScale,
                Dpi = 600
            };

            _processedSinglePhoto?.Dispose();
            _processedSinglePhoto = await _passportEngine.ProcessPassportPhotoAsync(_originalPhoto, options);
            SinglePhotoPreview = _processedSinglePhoto.ToBitmapSource();

            var layout = PhotoCount switch
            {
                16 => PhotoSheetLayout.A4_16Up,
                32 => PhotoSheetLayout.A4_32Up,
                _ => PhotoSheetLayout.Sheet4x6_8Up
            };

            _composedSheet?.Dispose();
            _composedSheet = _passportEngine.ComposeMultiUpSheet(_processedSinglePhoto, layout, dpi: 600);
            SheetPreview = _composedSheet.ToBitmapSource();
        }

        [RelayCommand]
        public async Task NudgeUpAsync()
        {
            PanOffsetY -= 0.02;
            await RegeneratePassportPhotosAsync();
        }

        [RelayCommand]
        public async Task NudgeDownAsync()
        {
            PanOffsetY += 0.02;
            await RegeneratePassportPhotosAsync();
        }

        [RelayCommand]
        public async Task NudgeLeftAsync()
        {
            PanOffsetX -= 0.02;
            await RegeneratePassportPhotosAsync();
        }

        [RelayCommand]
        public async Task NudgeRightAsync()
        {
            PanOffsetX += 0.02;
            await RegeneratePassportPhotosAsync();
        }

        [RelayCommand]
        public async Task ZoomInAsync()
        {
            ZoomScale = Math.Min(2.5, ZoomScale + 0.05);
            await RegeneratePassportPhotosAsync();
        }

        [RelayCommand]
        public async Task ZoomOutAsync()
        {
            ZoomScale = Math.Max(0.5, ZoomScale - 0.05);
            await RegeneratePassportPhotosAsync();
        }

        [RelayCommand]
        public async Task ResetFaceAlignmentAsync()
        {
            PanOffsetX = 0.0;
            PanOffsetY = 0.0;
            ZoomScale = 1.0;
            await RegeneratePassportPhotosAsync();
        }

        [RelayCommand]
        public async Task ToggleBackgroundRemovalAsync()
        {
            if (_originalPhoto == null) return;
            IsProcessing = true;
            StatusMessage = IsRemoveBackground ? "Executing GrabCut segmentation..." : "Restoring original background...";
            try
            {
                await RegeneratePassportPhotosAsync();
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        public async Task ChangeBackgroundAsync(string colorName)
        {
            SelectedBackground = colorName;
            if (_originalPhoto != null)
            {
                IsProcessing = true;
                try
                {
                    await RegeneratePassportPhotosAsync();
                }
                finally
                {
                    IsProcessing = false;
                }
            }
        }

        [RelayCommand]
        public async Task SetLayoutAsync(string layout)
        {
            switch (layout)
            {
                case "4x6_8":
                    PhotoCount = 8;
                    SelectedPaper = "4x6_PHOTO_PAPER";
                    SheetPrice = 50.00m;
                    break;
                case "A4_16":
                    PhotoCount = 16;
                    SelectedPaper = "A4_PAPER";
                    SheetPrice = 80.00m;
                    break;
                case "A4_32":
                    PhotoCount = 32;
                    SelectedPaper = "A4_PAPER";
                    SheetPrice = 120.00m;
                    break;
            }

            if (_originalPhoto != null)
            {
                IsProcessing = true;
                try
                {
                    await RegeneratePassportPhotosAsync();
                    StatusMessage = $"Updated sheet to {PhotoCount}-up layout ({SelectedPaper}).";
                }
                finally
                {
                    IsProcessing = false;
                }
            }
        }

        [RelayCommand]
        public void ToggleBiometricGuide()
        {
            ShowBiometricGuide = !ShowBiometricGuide;
        }

        [RelayCommand]
        public async Task ExportSinglePhotoAsync()
        {
            if (_processedSinglePhoto == null)
            {
                StatusMessage = "No photo available to export.";
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg",
                FileName = "Passport_Photo_35x45mm.png",
                Title = "Save Single 35x45 mm Passport Photo"
            };

            if (dlg.ShowDialog() == true)
            {
                using var image = SKImage.FromBitmap(_processedSinglePhoto);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(dlg.FileName);
                data.SaveTo(stream);
                await stream.FlushAsync();
                StatusMessage = $"Photo exported successfully: {dlg.FileName}";
            }
        }

        [RelayCommand]
        public async Task PrintPhotoSheetAsync()
        {
            if (_composedSheet == null)
            {
                StatusMessage = "No photo sheet generated to print.";
                return;
            }

            try
            {
                IsProcessing = true;
                string targetPrinter = !string.IsNullOrWhiteSpace(SelectedPrinter) ? SelectedPrinter : "Default Photo Printer";
                StatusMessage = $"Spooling {PhotoCount}-up photo sheet to '{targetPrinter}'...";

                var profile = new PrinterProfile
                {
                    ProfileName = targetPrinter,
                    PrinterDeviceName = targetPrinter,
                    TrayType = SelectedPaper,
                    TargetDpi = 600
                };

                var printResult = await _printService.PrintTrayAsync(
                    _composedSheet,
                    profile,
                    $"Passport Photo Sheet ({PhotoCount}-up)");

                if (!printResult.Success)
                {
                    StatusMessage = $"Spooling warning: {printResult.ErrorMessage ?? "Driver rejected print request."}";
                    return;
                }

                // Record transaction in ledger
                await _jobLedger.RecordJobAsync(new PrintJobRecord
                {
                    DocumentType = $"PHOTO_STUDIO_{PhotoCount}UP",
                    Quantity = 1,
                    TotalAmount = SheetPrice,
                    PaymentMode = PaymentMode,
                    PrinterName = targetPrinter,
                    Status = "COMPLETED"
                });

                StatusMessage = $"Passport photo sheet ({PhotoCount}-up) successfully spooled to {targetPrinter}! (Logged in Ledger: ₹{SheetPrice})";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Print error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }
}
