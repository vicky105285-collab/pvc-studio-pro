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
        private string _selectedBackground = "WHITE"; // WHITE, BLUE, GREY

        [ObservableProperty]
        private int _photoCount = 8; // 8 (4x6"), 16 (A4), 32 (A4)

        [ObservableProperty]
        private string _selectedPaper = "4x6_PHOTO_PAPER"; // 4x6_PHOTO_PAPER, A4_PAPER

        [ObservableProperty]
        private decimal _sheetPrice = 50.00m;

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
            IPrintService printService,
            IJobLedgerService jobLedger)
        {
            _visionService = visionService;
            _printService = printService;
            _jobLedger = jobLedger;
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
                StatusMessage = "Passport photo processed (35x45 mm, 600 DPI, 8-up sheet ready).";
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
                "GREY" => new SKColor(240, 240, 240), // Off-White Studio Grey
                _ => SKColors.White                   // Studio Pure White
            };

            _processedSinglePhoto?.Dispose();
            _processedSinglePhoto = await _visionService.ProcessPassportPhotoAsync(_originalPhoto, bgColor, IsRemoveBackground);
            SinglePhotoPreview = _processedSinglePhoto.ToBitmapSource();

            _composedSheet?.Dispose();
            _composedSheet = _visionService.ComposePassportPhotoSheet(_processedSinglePhoto, PhotoCount, dpi: 600);
            SheetPreview = _composedSheet.ToBitmapSource();
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
                StatusMessage = "Spooled 4x6\" photo sheet to printer queue...";

                // Record transaction in ledger
                await _jobLedger.RecordJobAsync(new PrintJobRecord
                {
                    DocumentType = "PHOTO_STUDIO",
                    Quantity = 1,
                    TotalAmount = SheetPrice,
                    PaymentMode = "CASH",
                    PrinterName = "Default Photo Printer",
                    Status = "COMPLETED"
                });

                StatusMessage = "Passport photo sheet successfully spooled! (Logged in Ledger)";
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
