using System;
using System.Collections.ObjectModel;
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
    public partial class CardPrintViewModel : ObservableObject
    {
        private readonly IPdfService _pdfService;
        private readonly ITemplateService _templateService;
        private readonly IPrinterDiscoveryService _printerDiscovery;
        private readonly IPrintService _printService;
        private readonly IJobLedgerService _jobLedger;

        [ObservableProperty]
        private BitmapSource? _frontCardImage;

        [ObservableProperty]
        private BitmapSource? _backCardImage;

        [ObservableProperty]
        private string _detectedTemplateName = "No document loaded";

        [ObservableProperty]
        private string _documentPath = string.Empty;

        [ObservableProperty]
        private bool _isMaskAadhaar = false;

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
        private decimal _cardPrice = 70.00m;

        [ObservableProperty]
        private int _copies = 1;

        [ObservableProperty]
        private bool _isMirrorMode = false;

        [ObservableProperty]
        private string _statusMessage = "Ready | Select or drop an Indian government document PDF";

        [ObservableProperty]
        private bool _isProcessing = false;

        [ObservableProperty]
        private bool _hasLoadedCards = false;

        [ObservableProperty]
        private bool _isPasswordPromptVisible = false;

        [ObservableProperty]
        private string _passwordInput = string.Empty;

        [ObservableProperty]
        private string _selectedPaymentMode = "CASH";

        [ObservableProperty]
        private ObservableCollection<string> _paymentModes = new() { "CASH", "UPI", "CARD" };

        private SKBitmap? _cachedFrontBitmap;
        private SKBitmap? _cachedBackBitmap;
        private CardTemplate? _activeTemplate;
        private byte[]? _activeDocumentBytes;

        public CardPrintViewModel(
            IPdfService pdfService,
            ITemplateService templateService,
            IPrinterDiscoveryService printerDiscovery,
            IPrintService printService,
            IJobLedgerService jobLedger)
        {
            _pdfService = pdfService;
            _templateService = templateService;
            _printerDiscovery = printerDiscovery;
            _printService = printService;
            _jobLedger = jobLedger;

            TrayList.Add("EPSON_L805_TRAY");
            TrayList.Add("EPSON_L8050_TRAY");
            TrayList.Add("CANON_MULTI_TRAY");
            TrayList.Add("DRAGON_SHEET_A4");
            TrayList.Add("CR80_DIRECT");
            SelectedTray = "EPSON_L805_TRAY";

            _ = InitializePrintersAsync();
        }

        [RelayCommand]
        public async Task UnlockPasswordProtectedAsync()
        {
            if (string.IsNullOrEmpty(DocumentPath) || string.IsNullOrEmpty(PasswordInput)) return;
            IsPasswordPromptVisible = false;
            await LoadDocumentFileAsync(DocumentPath, PasswordInput);
        }

        [RelayCommand]
        public void Rotate90()
        {
            if (_cachedFrontBitmap == null) return;

            try
            {
                var rotatedFront = new SKBitmap(_cachedFrontBitmap.Height, _cachedFrontBitmap.Width, _cachedFrontBitmap.ColorType, _cachedFrontBitmap.AlphaType);
                using (var canvas = new SKCanvas(rotatedFront))
                {
                    canvas.Translate(rotatedFront.Width, 0);
                    canvas.RotateDegrees(90);
                    canvas.DrawBitmap(_cachedFrontBitmap, 0, 0, SKSamplingOptions.Default, null);
                }
                _cachedFrontBitmap.Dispose();
                _cachedFrontBitmap = rotatedFront;
                FrontCardImage = _cachedFrontBitmap.ToBitmapSource();

                if (_cachedBackBitmap != null)
                {
                    var rotatedBack = new SKBitmap(_cachedBackBitmap.Height, _cachedBackBitmap.Width, _cachedBackBitmap.ColorType, _cachedBackBitmap.AlphaType);
                    using (var canvas = new SKCanvas(rotatedBack))
                    {
                        canvas.Translate(rotatedBack.Width, 0);
                        canvas.RotateDegrees(90);
                        canvas.DrawBitmap(_cachedBackBitmap, 0, 0, SKSamplingOptions.Default, null);
                    }
                    _cachedBackBitmap.Dispose();
                    _cachedBackBitmap = rotatedBack;
                    BackCardImage = _cachedBackBitmap.ToBitmapSource();
                }
                StatusMessage = "Rotated cards by 90 degrees.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Rotation error: {ex.Message}";
            }
        }

        [RelayCommand]
        public void FlipFaces()
        {
            if (_cachedFrontBitmap == null) return;

            var tempBmp = _cachedFrontBitmap;
            _cachedFrontBitmap = _cachedBackBitmap ?? _cachedFrontBitmap;
            _cachedBackBitmap = tempBmp;

            FrontCardImage = _cachedFrontBitmap?.ToBitmapSource();
            BackCardImage = _cachedBackBitmap?.ToBitmapSource();
            StatusMessage = "Swapped Front and Back card faces.";
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
                    // Prefer Epson photo printers if present
                    string? epson = null;
                    foreach (var name in PrinterList)
                    {
                        if (name.Contains("L805", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("L8050", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("L18050", StringComparison.OrdinalIgnoreCase))
                        {
                            epson = name;
                            break;
                        }
                    }
                    SelectedPrinter = epson ?? PrinterList[0];
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Printer discovery notice: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task OpenDocumentAsync()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Indian Gov ID Documents (*.pdf)|*.pdf|All Files (*.*)|*.*",
                Title = "Select Identity Card Document"
            };

            if (dlg.ShowDialog() == true)
            {
                await LoadDocumentFileAsync(dlg.FileName);
            }
        }

        public async Task LoadDocumentFileAsync(string filePath, string? password = null)
        {
            try
            {
                IsProcessing = true;
                StatusMessage = "Reading and analyzing document...";
                DocumentPath = filePath;

                _activeDocumentBytes = await File.ReadAllBytesAsync(filePath);
                using var stream = new MemoryStream(_activeDocumentBytes);

                var docInfo = await _pdfService.InspectDocumentAsync(stream, password);
                if (docInfo.IsEncrypted && string.IsNullOrEmpty(password))
                {
                    IsPasswordPromptVisible = true;
                    StatusMessage = "Password-protected PDF detected. Enter password to unlock.";
                    return;
                }

                // Match against 10 Indian Government card templates
                var matchResult = _templateService.MatchTemplate(docInfo.ExtractedTextSample, docInfo.PageWidthMm, docInfo.PageHeightMm);
                if (!matchResult.IsSuccess || matchResult.Template == null)
                {
                    DetectedTemplateName = "UNSUPPORTED / UNKNOWN DOCUMENT";
                    StatusMessage = $"Document rejected: {matchResult.MatchReason}";
                    HasLoadedCards = false;
                    FrontCardImage = null;
                    BackCardImage = null;
                    return;
                }

                _activeTemplate = matchResult.Template;
                if (matchResult.RequiresOperatorConfirmation)
                {
                    DetectedTemplateName = $"{_activeTemplate.DisplayName} (Confidence: {matchResult.ConfidenceScore:P0})";
                    StatusMessage = $"Please confirm: {matchResult.MatchReason}";
                }
                else
                {
                    DetectedTemplateName = $"{_activeTemplate.DisplayName} ({_activeTemplate.DocumentType})";
                    StatusMessage = $"Document Loaded: {_activeTemplate.DisplayName} (CR80 600 DPI Vector Ready)";
                }

                await ExtractAndDisplayFacesAsync();
                HasLoadedCards = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading document: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task ExtractAndDisplayFacesAsync()
        {
            if (_activeDocumentBytes == null || _activeTemplate == null) return;

            using var stream = new MemoryStream(_activeDocumentBytes);
            var (front, back) = await _pdfService.ExtractCardFacesAsync(stream, _activeTemplate, applyMask: IsMaskAadhaar);

            _cachedFrontBitmap?.Dispose();
            _cachedBackBitmap?.Dispose();

            _cachedFrontBitmap = front;
            _cachedBackBitmap = back;

            FrontCardImage = _cachedFrontBitmap.ToBitmapSource();
            BackCardImage = _cachedBackBitmap?.ToBitmapSource();
        }

        [RelayCommand]
        public async Task ToggleMaskAsync()
        {
            if (_cachedFrontBitmap == null) return;
            IsMaskAadhaar = !IsMaskAadhaar;
            await ExtractAndDisplayFacesAsync();
        }

        [RelayCommand]
        public async Task PrintCardAsync()
        {
            if (_cachedFrontBitmap == null)
            {
                StatusMessage = "No card loaded to print.";
                return;
            }

            try
            {
                IsProcessing = true;
                StatusMessage = "Preparing 600 DPI tray canvas...";

                var profile = new PrinterProfile
                {
                    PrinterDeviceName = SelectedPrinter,
                    TrayType = SelectedTray,
                    OffsetXMm = OffsetX,
                    OffsetYMm = OffsetY,
                    TargetDpi = 600,
                    MirrorMode = IsMirrorMode
                };

                using var trayCanvas = _printService.ComposeTrayCanvas(_cachedFrontBitmap, _cachedBackBitmap, profile);

                StatusMessage = $"Sending job to printer '{SelectedPrinter}'...";
                var result = await _printService.PrintTrayAsync(trayCanvas, profile, $"PVC Studio - {DetectedTemplateName}");

                if (result.Success)
                {
                    StatusMessage = $"Print successfully spooled! Job ID: {result.JobId}";

                    // Record transaction in zero-PII ledger
                    await _jobLedger.RecordJobAsync(new PrintJobRecord
                    {
                        DocumentType = _activeTemplate?.DocumentType ?? "OTHER",
                        Quantity = Copies,
                        TotalAmount = CardPrice * Copies,
                        PaymentMode = "CASH",
                        PrinterName = SelectedPrinter,
                        Status = "COMPLETED"
                    });
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
                IsProcessing = false;
            }
        }
    }
}
