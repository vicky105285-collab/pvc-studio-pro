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
        private readonly IDocumentImportService _importService;
        private readonly IPdfDocumentService _pdfDocumentService;
        private readonly IDocumentTypeDetectionService _detectionService;
        private readonly ICardDetectionService _cardDetection;
        private readonly IManualCropService _cropService;
        private readonly IPdfService _pdfService;
        private readonly ITemplateService _templateService;
        private readonly IPrinterDiscoveryService _printerDiscovery;
        private readonly IPrintService _printService;
        private readonly IJobLedgerService _jobLedger;

        // Navigation callbacks wired by MainViewModel
        public Action<SKBitmap, string>? OpenInCropStudioAction { get; set; }
        public Action<SKBitmap?, SKBitmap?, DocumentKind>? OpenInPrintPreviewAction { get; set; }
        public Action<SKBitmap?, SKBitmap?, string>? OpenInPageDesignerAction { get; set; }

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
        private string _selectedTray = "EPSON_L805_TRAY";

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
        private string _statusMessage = "Ready | Select or drop an Indian government document (PDF, JPG, PNG)";

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

        private SKBitmap? _cachedPageBitmap;
        private SKBitmap? _cachedFrontBitmap;
        private SKBitmap? _cachedBackBitmap;
        private CardTemplate? _activeTemplate;
        private byte[]? _activeDocumentBytes;
        private DocumentKind _detectedKind = DocumentKind.Aadhaar;

        public CardPrintViewModel(
            IDocumentImportService importService,
            IPdfDocumentService pdfDocumentService,
            IDocumentTypeDetectionService detectionService,
            ICardDetectionService cardDetection,
            IManualCropService cropService,
            IPdfService pdfService,
            ITemplateService templateService,
            IPrinterDiscoveryService printerDiscovery,
            IPrintService printService,
            IJobLedgerService jobLedger)
        {
            _importService = importService;
            _pdfDocumentService = pdfDocumentService;
            _detectionService = detectionService;
            _cardDetection = cardDetection;
            _cropService = cropService;
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

        [RelayCommand]
        public void EditInCropStudio()
        {
            if (_cachedPageBitmap != null)
            {
                OpenInCropStudioAction?.Invoke(_cachedPageBitmap, Path.GetFileName(DocumentPath));
            }
            else if (_cachedFrontBitmap != null)
            {
                OpenInCropStudioAction?.Invoke(_cachedFrontBitmap, "Front Card");
            }
            else
            {
                StatusMessage = "No document page available to edit in Crop Studio.";
            }
        }

        [RelayCommand]
        public void SendToPageDesigner()
        {
            if (_cachedFrontBitmap == null)
            {
                StatusMessage = "Load or extract cards before sending to Page Designer.";
                return;
            }

            OpenInPageDesignerAction?.Invoke(_cachedFrontBitmap, _cachedBackBitmap, DetectedTemplateName);
        }

        [RelayCommand]
        public void SendToPrintPreview()
        {
            if (_cachedFrontBitmap == null)
            {
                StatusMessage = "Load or extract cards before opening True Print Preview.";
                return;
            }

            OpenInPrintPreviewAction?.Invoke(_cachedFrontBitmap, _cachedBackBitmap, _detectedKind);
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
                Filter = "All Supported Identity Documents (*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.webp)|*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.webp|PDF Documents (*.pdf)|*.pdf|Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All Files (*.*)|*.*",
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

                var sniff = _importService.SniffFile(filePath);
                _activeDocumentBytes = await File.ReadAllBytesAsync(filePath);

                if (sniff.MagicKind == FileMagicKind.Pdf)
                {
                    using var stream = new MemoryStream(_activeDocumentBytes);
                    var unlock = await _pdfDocumentService.TryUnlockPasswordAsync(stream, password);
                    if (unlock.Status == PdfPasswordStatus.PasswordRequired || unlock.Status == PdfPasswordStatus.InvalidPassword)
                    {
                        IsPasswordPromptVisible = true;
                        StatusMessage = unlock.Status == PdfPasswordStatus.InvalidPassword 
                            ? "Invalid password. Please re-enter the 8-character PDF password."
                            : "Password-protected PDF detected. Enter password to unlock.";
                        return;
                    }

                    // Render Page 0 at 300 DPI
                    stream.Position = 0;
                    _cachedPageBitmap?.Dispose();
                    _cachedPageBitmap = await _pdfService.RenderPageAsync(stream, 0, 300, password);

                    // Text sample
                    stream.Position = 0;
                    var docInfo = await _pdfService.InspectDocumentAsync(stream, password);
                    string textSample = docInfo.ExtractedTextSample;

                    // Match template via TemplateService first
                    var matchResult = _templateService.MatchTemplate(textSample, docInfo.PageWidthMm, docInfo.PageHeightMm);
                    if (matchResult.IsSuccess && matchResult.Template != null && matchResult.ConfidenceScore > 0.65)
                    {
                        _activeTemplate = matchResult.Template;
                        DetectedTemplateName = $"{_activeTemplate.DisplayName} (CR80 600 DPI)";
                        StatusMessage = $"Recognized: {_activeTemplate.DisplayName}";

                        using var extractStream = new MemoryStream(_activeDocumentBytes);
                        var (front, back) = await _pdfService.ExtractCardFacesAsync(extractStream, _activeTemplate, password, applyMask: IsMaskAadhaar);
                        SetCardBitmaps(front, back);
                        HasLoadedCards = true;
                        return;
                    }

                    // If text-based template match fails (common for UIDAI Identity-H font PDFs):
                    // Fall back to robust multi-factor detection service + cut guide detector
                    if (_cachedPageBitmap != null)
                    {
                        var detection = await _detectionService.DetectDocumentTypeAsync(_cachedPageBitmap, textSample, docInfo.PageWidthMm, docInfo.PageHeightMm);
                        _detectedKind = detection.DetectedKind;
                        DetectedTemplateName = $"{detection.DisplayName} ({detection.Confidence:P0})";
                        StatusMessage = $"Visual layout detected: {detection.DisplayName} ({detection.DetectionNotes})";

                        var (front, back, _, _) = await _cardDetection.DetectAndExtractDualCardsAsync(_cachedPageBitmap, detection.DetectedKind, detection.AadhaarGen);
                        if (front != null)
                        {
                            SetCardBitmaps(front, back);
                            HasLoadedCards = true;
                            return;
                        }
                    }

                    DetectedTemplateName = "UNSUPPORTED / UNKNOWN DOCUMENT";
                    StatusMessage = "Could not automatically isolate card boundaries. Click 'Edit in Crop Studio' to crop manually.";
                    HasLoadedCards = false;
                }
                else
                {
                    // Raster Image input (JPG, PNG, BMP, TIFF, WebP)
                    _cachedPageBitmap?.Dispose();
                    _cachedPageBitmap = SKBitmap.Decode(_activeDocumentBytes);
                    if (_cachedPageBitmap == null)
                    {
                        StatusMessage = "Failed to decode image data.";
                        return;
                    }

                    var detection = await _detectionService.DetectDocumentTypeAsync(_cachedPageBitmap, string.Empty);
                    _detectedKind = detection.DetectedKind;
                    DetectedTemplateName = $"{detection.DisplayName} (Image Input)";

                    var (front, back, _, _) = await _cardDetection.DetectAndExtractDualCardsAsync(_cachedPageBitmap, detection.DetectedKind, detection.AadhaarGen);
                    if (front != null)
                    {
                        SetCardBitmaps(front, back);
                        HasLoadedCards = true;
                    }
                    else
                    {
                        // Assume whole image is the card face
                        SetCardBitmaps(_cachedPageBitmap.Copy(), null);
                        HasLoadedCards = true;
                        StatusMessage = "Image loaded as single card face. Open Crop Studio to adjust if needed.";
                    }
                }
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

        private void SetCardBitmaps(SKBitmap? front, SKBitmap? back)
        {
            _cachedFrontBitmap?.Dispose();
            _cachedBackBitmap?.Dispose();

            _cachedFrontBitmap = front;
            _cachedBackBitmap = back;

            FrontCardImage = _cachedFrontBitmap?.ToBitmapSource();
            BackCardImage = _cachedBackBitmap?.ToBitmapSource();
        }

        [RelayCommand]
        public async Task ToggleMaskAsync()
        {
            if (_cachedFrontBitmap == null) return;
            IsMaskAadhaar = !IsMaskAadhaar;
            if (_cachedPageBitmap != null)
            {
                await LoadDocumentFileAsync(DocumentPath, PasswordInput);
            }
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
                        DocumentType = _detectedKind.ToString(),
                        Quantity = Copies,
                        TotalAmount = CardPrice * Copies,
                        PaymentMode = SelectedPaymentMode,
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
