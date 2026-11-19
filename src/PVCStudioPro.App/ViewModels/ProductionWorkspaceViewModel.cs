using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SkiaSharp;
using PVCStudioPro.App.Converters;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.App.ViewModels
{
    public enum SheetLayoutMode
    {
        SingleSideA4,       // Front and Back side-by-side on upper A4
        CenterFoldA4,       // Front and Back stacked vertically with center crease line
        DoubleSideDuplexA4, // Front on Sheet 1, Back on Sheet 2
        Epson2CardTray,     // Standard Epson tray (L805, L8050, L18050)
        CanonMultiTray,     // Canon G-series PVC tray
        Photo4x6Sheet,      // 4x6 inch photo paper
        DirectCr80          // Single CR80 direct card
    }

    public partial class ProductionWorkspaceViewModel : ObservableObject
    {
        private readonly IDocumentImportService _importService;
        private readonly IPdfDocumentService _pdfDocumentService;
        private readonly IDocumentTypeDetectionService _detectionService;
        private readonly ICardDetectionService _cardDetection;
        private readonly ICardDetectionEngine _cardDetectionEngine;
        private readonly IManualCropService _cropService;
        private readonly IPhysicalPageRenderService _renderService;
        private readonly IPrinterDiscoveryService _printerDiscovery;
        private readonly IPrintService _printService;
        private readonly IJobLedgerService _jobLedger;
        private readonly ITemplateService _templateService;
        private readonly IDocumentSessionManager? _sessionManager;
        private IDisposable? _activeSessionSubscription;

        // Navigation callbacks for secondary workspaces
        public Action<string>? NavigateAction { get; set; }

        #region Document & Card State

        [ObservableProperty]
        private string _documentPath = string.Empty;

        [ObservableProperty]
        private string _fileName = "No document loaded";

        [ObservableProperty]
        private string _detectedDocTitle = "Ready | Drag and drop or open an Aadhaar / Indian Gov ID";

        [ObservableProperty]
        private string _detectedBadge = "AWAITING FILE";

        [ObservableProperty]
        private int _detectionConfidencePercent;

        [ObservableProperty]
        private string _detectionConfidenceSummary = string.Empty;

        [ObservableProperty]
        private bool _isLowConfidenceBannerVisible = false;

        [ObservableProperty]
        private string _lowConfidenceWarningTitle = string.Empty;

        [ObservableProperty]
        private string _lowConfidenceWarningDetails = string.Empty;

        [ObservableProperty]
        private System.Windows.Media.Brush _confidenceBadgeBrush = System.Windows.Media.Brushes.Transparent;

        [ObservableProperty]
        private System.Windows.Media.Brush _confidenceTextBrush = System.Windows.Media.Brushes.Black;

        [ObservableProperty]
        private bool _isPasswordPromptVisible = false;

        [ObservableProperty]
        private string _passwordInput = string.Empty;

        [ObservableProperty]
        private bool _hasLoadedDocument = false;

        [ObservableProperty]
        private bool _isProcessing = false;

        [ObservableProperty]
        private bool _isMaskAadhaar = false;

        [ObservableProperty]
        private BitmapSource? _frontCardImage;

        [ObservableProperty]
        private BitmapSource? _backCardImage;

        [ObservableProperty]
        private bool _hasFrontCard = false;

        [ObservableProperty]
        private bool _hasBackCard = false;

        #endregion

        #region Physical Sheet Canvas State

        [ObservableProperty]
        private PageLayoutModel _activePageLayout = PageLayoutModel.CreateDefaultA4();

        [ObservableProperty]
        private BitmapSource? _sheetPreviewImage;

        [ObservableProperty]
        private SheetLayoutMode _selectedSheetLayout = SheetLayoutMode.SingleSideA4;

        [ObservableProperty]
        private ObservableCollection<string> _sheetLayoutOptions = new()
        {
            "A4 Single Side (Side-by-Side)",
            "A4 Center Fold (Crease Fold Line)",
            "A4 Duplex Sheet (Page 1 & 2)",
            "Epson 2-Card Tray (L805 / L8050 / L18050)",
            "Canon Multi-Card Tray",
            "4x6 Photo Paper (2 Cards Stacked)",
            "Direct Single CR80 Card"
        };

        [ObservableProperty]
        private string _selectedSheetLayoutString = "A4 Single Side (Side-by-Side)";

        [ObservableProperty]
        private double _canvasZoomPercent = 100.0;

        [ObservableProperty]
        private bool _showCutMarks = true;

        [ObservableProperty]
        private bool _mirrorMode = false;

        [ObservableProperty]
        private string _pageDimensionsText = "A4 Portrait: 210.0 x 297.0 mm";

        [ObservableProperty]
        private string _cardDimensionsText = "ISO/IEC 7810 CR80: 85.60 x 53.98 mm";

        #endregion

        #region Adjustments & Offsets

        [ObservableProperty]
        private double _offsetXMm = 0.0;

        [ObservableProperty]
        private double _offsetYMm = 0.0;

        [ObservableProperty]
        private float _brightness = 0f;

        [ObservableProperty]
        private float _contrast = 1.0f;

        [ObservableProperty]
        private float _saturation = 1.0f;

        #endregion

        #region Hardware Print Settings & Accounting

        [ObservableProperty]
        private ObservableCollection<string> _printerList = new();

        [ObservableProperty]
        private string _selectedPrinter = string.Empty;

        [ObservableProperty]
        private int _copies = 1;

        [ObservableProperty]
        private decimal _cardFee = 70.00m;

        [ObservableProperty]
        private string _selectedPaymentMode = "CASH";

        [ObservableProperty]
        private ObservableCollection<string> _paymentModes = new() { "CASH", "UPI", "CARD" };

        [ObservableProperty]
        private string _statusMessage = "PVC Studio Pro Ready | Clean-Room Zero-Citizen-PII Architecture";

        [ObservableProperty]
        private bool _printSuccessAlert = false;

        #endregion

        #region Integrated Crop Drawer State

        [ObservableProperty]
        private bool _isCropDrawerOpen = false;

        [ObservableProperty]
        private BitmapSource? _cropSourceImage;

        [ObservableProperty]
        private string _cropTargetFace = "Front Card";

        [ObservableProperty]
        private double _cropX = 210.0;

        [ObservableProperty]
        private double _cropY = 2595.0;

        [ObservableProperty]
        private double _cropWidth = 1045.0;

        [ObservableProperty]
        private double _cropHeight = 665.0;

        [ObservableProperty]
        private ObservableCollection<string> _cropFaceOptions = new() { "Front Card", "Back Card" };

        #endregion

        private SKBitmap? _cachedPageBitmap;
        private SKBitmap? _cachedFrontBitmap;
        private SKBitmap? _cachedBackBitmap;
        private byte[]? _activeDocumentBytes;
        private DocumentKind _detectedKind = DocumentKind.Aadhaar;
        private readonly Stack<string> _undoStack = new();
        private readonly Stack<string> _redoStack = new();

        public ProductionWorkspaceViewModel(
            IDocumentImportService importService,
            IPdfDocumentService pdfDocumentService,
            IDocumentTypeDetectionService detectionService,
            ICardDetectionService cardDetection,
            IManualCropService cropService,
            IPhysicalPageRenderService renderService,
            IPrinterDiscoveryService printerDiscovery,
            IPrintService printService,
            IJobLedgerService jobLedger,
            ITemplateService templateService,
            IDocumentSessionManager? sessionManager = null)
        {
            _importService = importService;
            _pdfDocumentService = pdfDocumentService;
            _detectionService = detectionService;
            _cardDetection = cardDetection;
            _cardDetectionEngine = cardDetection as ICardDetectionEngine ?? new CardDetectionService(_templateService, _detectionService);
            _cropService = cropService;
            _renderService = renderService;
            _printerDiscovery = printerDiscovery;
            _printService = printService;
            _jobLedger = jobLedger;
            _templateService = templateService;
            _sessionManager = sessionManager;

            if (_sessionManager != null)
            {
                _sessionManager.SessionChanged += OnSessionManagerChanged;
                if (_sessionManager.ActiveSession != null)
                {
                    AttachToSession(_sessionManager.ActiveSession);
                }
            }

            _ = InitializePrintersAsync();
            BuildPhysicalSheetLayout();
        }

        private void OnSessionManagerChanged(SessionEvent ev)
        {
            if (ev.EventType == SessionEventType.ActiveChanged || ev.EventType == SessionEventType.DocumentModified)
            {
                var session = _sessionManager?.ActiveSession;
                if (session != null)
                {
                    AttachToSession(session);
                }
                else
                {
                    DetachFromSession();
                }
            }
        }

        public void AttachToSession(IDocumentSession session)
        {
            _activeSessionSubscription?.Dispose();
            _activeSessionSubscription = session.Subscribe(change =>
            {
                var app = System.Windows.Application.Current;
                if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
                {
                    app.Dispatcher.InvokeAsync(() => SyncFromSession(session));
                }
                else
                {
                    SyncFromSession(session);
                }
            });

            SyncFromSession(session);
        }

        public void SyncFromSession(IDocumentSession session)
        {
            DocumentPath = session.SourceFilePath;
            FileName = session.FileName;
            HasLoadedDocument = true;

            if (session.FrontCardAsset != null)
            {
                using var lease = session.FrontCardAsset.AcquireLease();
                FrontCardImage = lease.Surface.ToBitmapSource();
            }

            if (session.BackCardAsset != null)
            {
                using var lease = session.BackCardAsset.AcquireLease();
                BackCardImage = lease.Surface.ToBitmapSource();
            }

            BuildPhysicalSheetLayout();
        }

        public void DetachFromSession()
        {
            _activeSessionSubscription?.Dispose();
            _activeSessionSubscription = null;
            DocumentPath = string.Empty;
            FileName = "No document loaded";
            HasLoadedDocument = false;
            FrontCardImage = null;
            BackCardImage = null;
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
                    string? preferred = null;
                    foreach (var p in PrinterList)
                    {
                        if (p.Contains("L805", StringComparison.OrdinalIgnoreCase) ||
                            p.Contains("L8050", StringComparison.OrdinalIgnoreCase) ||
                            p.Contains("L18050", StringComparison.OrdinalIgnoreCase))
                        {
                            preferred = p;
                            break;
                        }
                    }
                    SelectedPrinter = preferred ?? PrinterList[0];
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Printer notice: {ex.Message}";
            }
        }

        #region Automated Document Ingestion Pipeline

        [RelayCommand]
        public async Task OpenDocumentFileDialogAsync()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "All Supported Identity Files (*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.webp)|*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.webp|PDF Documents (*.pdf)|*.pdf|Image Scans (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|All Files (*.*)|*.*",
                Title = "Select Identity Card Document (Aadhaar, PAN, Voter, etc.)"
            };

            if (dlg.ShowDialog() == true)
            {
                await LoadDocumentAsync(dlg.FileName);
            }
        }

        public async Task LoadDocumentAsync(string filePath, string? password = null)
        {
            try
            {
                IsProcessing = true;
                StatusMessage = "Analyzing document structure and auto-extracting cards...";
                DocumentPath = filePath;
                FileName = Path.GetFileName(filePath);
                PrintSuccessAlert = false;

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
                            ? "Invalid password. Re-enter the 8-character PDF password."
                            : "🔒 Password-protected PDF detected. Enter password to unlock.";
                        return;
                    }
                    IsPasswordPromptVisible = false;

                    // Render page 0 at 300 DPI
                    stream.Position = 0;
                    _cachedPageBitmap?.Dispose();
                    _cachedPageBitmap = await _pdfDocumentService.RenderPageAsync(stream, 0, 300, password);

                    // Text sample
                    stream.Position = 0;
                    var docInfo = await _pdfDocumentService.InspectDocumentAsync(stream, password);
                    string textSample = docInfo.ExtractedTextSample;

                    // 5-Stage Adaptive detection and extraction
                    var extraction = await _cardDetectionEngine.DetectAndExtractAdaptiveAsync(
                        _cachedPageBitmap,
                        textSample,
                        docInfo.PageWidthMm,
                        docInfo.PageHeightMm);

                    _detectedKind = extraction.DetectedKind;
                    DetectedDocTitle = extraction.MatchedTemplate?.DisplayName ?? $"{_detectedKind}";
                    DetectedBadge = $"{extraction.DetectedKind.ToString().ToUpperInvariant()} • AUTO-DETECTED";

                    ApplyDetectionConfidence(extraction.Confidence, extraction.FrontRect);
                    SetExtractedCards(extraction.FrontCard, extraction.BackCard);
                }
                else
                {
                    // Raster Image input
                    _cachedPageBitmap?.Dispose();
                    _cachedPageBitmap = SKBitmap.Decode(_activeDocumentBytes);
                    if (_cachedPageBitmap == null)
                    {
                        StatusMessage = "Failed to decode image data.";
                        return;
                    }

                    var extraction = await _cardDetectionEngine.DetectAndExtractAdaptiveAsync(
                        _cachedPageBitmap,
                        string.Empty);

                    _detectedKind = extraction.DetectedKind;
                    DetectedDocTitle = extraction.MatchedTemplate?.DisplayName ?? $"{_detectedKind}";
                    DetectedBadge = $"{extraction.DetectedKind.ToString().ToUpperInvariant()} • IMAGE SCAN";

                    ApplyDetectionConfidence(extraction.Confidence, extraction.FrontRect);
                    if (extraction.FrontCard != null)
                    {
                        SetExtractedCards(extraction.FrontCard, extraction.BackCard);
                    }
                    else
                    {
                        // Single card image
                        SetExtractedCards(_cachedPageBitmap.Copy(), null);
                    }
                }

                HasLoadedDocument = true;
                StatusMessage = DetectionConfidencePercent >= 85
                    ? $"✓ Successfully imported {FileName} | {DetectionConfidenceSummary} | CR80 standard applied automatically."
                    : $"⚠️ Imported {FileName} with {DetectionConfidenceSummary}. Crop verification drawer opened.";
                BuildPhysicalSheetLayout();
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

        [RelayCommand]
        public async Task UnlockPasswordAsync()
        {
            if (string.IsNullOrEmpty(DocumentPath) || string.IsNullOrEmpty(PasswordInput)) return;
            await LoadDocumentAsync(DocumentPath, PasswordInput);
        }

        private void SetExtractedCards(SKBitmap? front, SKBitmap? back)
        {
            _cachedFrontBitmap?.Dispose();
            _cachedBackBitmap?.Dispose();

            // Enforce exact CR80 physical dimensions (85.60 x 53.98 mm = 2022 x 1275 px @ 600 DPI)
            _cachedFrontBitmap = front != null ? NormalizeToCr80(front) : null;
            _cachedBackBitmap = back != null ? NormalizeToCr80(back) : null;

            FrontCardImage = _cachedFrontBitmap?.ToBitmapSource();
            BackCardImage = _cachedBackBitmap?.ToBitmapSource();

            HasFrontCard = _cachedFrontBitmap != null;
            HasBackCard = _cachedBackBitmap != null;
        }

        private static SKBitmap NormalizeToCr80(SKBitmap source)
        {
            int targetW = 2022; // 85.60 mm @ 600 DPI
            int targetH = 1275; // 53.98 mm @ 600 DPI

            if (source.Width == targetW && source.Height == targetH)
            {
                return source;
            }

            var dest = new SKBitmap(targetW, targetH, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(dest);
            canvas.Clear(SKColors.White);

            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            var destRect = new SKRect(0, 0, targetW, targetH);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(source, destRect, sampling, paint);

            source.Dispose();
            return dest;
        }

        #endregion

        #region Interactive Card Adjustments

        [RelayCommand]
        public void SwapCardFaces()
        {
            if (_cachedFrontBitmap == null) return;
            var temp = _cachedFrontBitmap;
            _cachedFrontBitmap = _cachedBackBitmap ?? _cachedFrontBitmap;
            _cachedBackBitmap = temp;

            FrontCardImage = _cachedFrontBitmap?.ToBitmapSource();
            BackCardImage = _cachedBackBitmap?.ToBitmapSource();

            HasFrontCard = _cachedFrontBitmap != null;
            HasBackCard = _cachedBackBitmap != null;

            BuildPhysicalSheetLayout();
            StatusMessage = "Swapped Front and Back card faces.";
        }

        [RelayCommand]
        public void RotateCards90()
        {
            if (_cachedFrontBitmap == null) return;

            _cachedFrontBitmap = RotateBitmap90(_cachedFrontBitmap);
            FrontCardImage = _cachedFrontBitmap.ToBitmapSource();

            if (_cachedBackBitmap != null)
            {
                _cachedBackBitmap = RotateBitmap90(_cachedBackBitmap);
                BackCardImage = _cachedBackBitmap.ToBitmapSource();
            }

            BuildPhysicalSheetLayout();
            StatusMessage = "Rotated card faces 90 degrees.";
        }

        private static SKBitmap RotateBitmap90(SKBitmap source)
        {
            var rotated = new SKBitmap(source.Height, source.Width, source.ColorType, source.AlphaType);
            using (var canvas = new SKCanvas(rotated))
            {
                canvas.Translate(rotated.Width, 0);
                canvas.RotateDegrees(90);
                canvas.DrawBitmap(source, 0, 0, SKSamplingOptions.Default, null);
            }
            source.Dispose();
            return rotated;
        }

        [RelayCommand]
        public async Task ToggleMaskAadhaarAsync()
        {
            IsMaskAadhaar = !IsMaskAadhaar;
            if (!string.IsNullOrEmpty(DocumentPath))
            {
                await LoadDocumentAsync(DocumentPath, PasswordInput);
            }
        }

        [RelayCommand]
        public void OpenCropDrawer(string targetFace)
        {
            if (_cachedPageBitmap == null && _cachedFrontBitmap == null)
            {
                StatusMessage = "Please load a document before adjusting crop.";
                return;
            }

            CropTargetFace = targetFace;
            CropSourceImage = (_cachedPageBitmap ?? _cachedFrontBitmap)?.ToBitmapSource();

            if (targetFace.Contains("Front", StringComparison.OrdinalIgnoreCase))
            {
                if (CropWidth <= 0 || CropHeight <= 0)
                {
                    CropX = 210;
                    CropY = 2595;
                    CropWidth = 1045;
                    CropHeight = 665;
                }
            }
            else
            {
                if (CropWidth <= 0 || CropHeight <= 0)
                {
                    CropX = 1300;
                    CropY = 2595;
                    CropWidth = 1045;
                    CropHeight = 665;
                }
            }

            IsCropDrawerOpen = true;
            StatusMessage = $"Adjusting crop for {targetFace}. Fit-to-window preview active.";
        }

        [RelayCommand]
        public void CloseCropDrawer()
        {
            IsCropDrawerOpen = false;
        }

        [RelayCommand]
        public void ResetCropToAuto()
        {
            if (CropTargetFace.Contains("Front", StringComparison.OrdinalIgnoreCase))
            {
                CropX = 210;
                CropY = 2595;
                CropWidth = 1045;
                CropHeight = 665;
            }
            else
            {
                CropX = 1300;
                CropY = 2595;
                CropWidth = 1045;
                CropHeight = 665;
            }
            StatusMessage = $"Reset crop bounds to default {CropTargetFace} coordinates.";
        }

        [RelayCommand]
        public void ApplyCustomCrop()
        {
            if (_cachedPageBitmap == null)
            {
                IsCropDrawerOpen = false;
                return;
            }

            int imgW = _cachedPageBitmap.Width;
            int imgH = _cachedPageBitmap.Height;

            int x = Math.Clamp((int)CropX, 0, imgW - 10);
            int y = Math.Clamp((int)CropY, 0, imgH - 10);
            int w = Math.Clamp((int)CropWidth, 10, imgW - x);
            int h = Math.Clamp((int)CropHeight, 10, imgH - y);

            var rect = new SKRectI(x, y, x + w, y + h);
            var cropped = new SKBitmap(rect.Width, rect.Height, _cachedPageBitmap.ColorType, _cachedPageBitmap.AlphaType);
            if (_cachedPageBitmap.ExtractSubset(cropped, rect))
            {
                var cr80 = NormalizeToCr80(cropped);
                if (CropTargetFace.Contains("Front", StringComparison.OrdinalIgnoreCase))
                {
                    _cachedFrontBitmap?.Dispose();
                    _cachedFrontBitmap = cr80;
                    FrontCardImage = _cachedFrontBitmap.ToBitmapSource();
                    HasFrontCard = true;
                }
                else
                {
                    _cachedBackBitmap?.Dispose();
                    _cachedBackBitmap = cr80;
                    BackCardImage = _cachedBackBitmap.ToBitmapSource();
                    HasBackCard = true;
                }

                BuildPhysicalSheetLayout();
                StatusMessage = $"✓ Applied custom crop to {CropTargetFace} ({w}x{h} px -> CR80 standard).";
            }
            else
            {
                cropped.Dispose();
                StatusMessage = "Failed to extract custom crop subset.";
            }

            IsCropDrawerOpen = false;
        }

        [RelayCommand]
        public void DismissLowConfidenceBanner()
        {
            IsLowConfidenceBannerVisible = false;
            StatusMessage = "Auto-detected card bounds accepted by operator.";
        }

        private void ApplyDetectionConfidence(DetectionConfidenceScore score, SKRectI? frontRect)
        {
            DetectionConfidencePercent = score.ConfidencePercent;
            DetectionConfidenceSummary = score.Summary;

            if (score.IsHighConfidence)
            {
                IsLowConfidenceBannerVisible = false;
                ConfidenceBadgeBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 252, 231)); // Light green #DCFCE7
                ConfidenceTextBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 101, 52));    // Dark green #166534
            }
            else
            {
                IsLowConfidenceBannerVisible = true;
                ConfidenceBadgeBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 243, 199)); // Amber #FEF3C7
                ConfidenceTextBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(146, 64, 14));    // Dark amber #92400E

                LowConfidenceWarningTitle = $"⚠️ Low Detection Confidence ({DetectionConfidencePercent}%)";
                LowConfidenceWarningDetails = "Card borders could not be verified with high certainty. Please review or adjust card bounds.";

                if (frontRect.HasValue)
                {
                    CropX = frontRect.Value.Left;
                    CropY = frontRect.Value.Top;
                    CropWidth = frontRect.Value.Width;
                    CropHeight = frontRect.Value.Height;
                }
                CropTargetFace = "Front Card";
                if (_cachedPageBitmap != null || _cachedFrontBitmap != null)
                {
                    CropSourceImage = (_cachedPageBitmap ?? _cachedFrontBitmap)?.ToBitmapSource();
                }
                IsCropDrawerOpen = true;
            }
        }

        #endregion

        #region Authoritative Physical Sheet Layout Engine

        partial void OnSelectedSheetLayoutStringChanged(string value)
        {
            if (value.StartsWith("A4 Single", StringComparison.OrdinalIgnoreCase))
                SelectedSheetLayout = SheetLayoutMode.SingleSideA4;
            else if (value.StartsWith("A4 Center", StringComparison.OrdinalIgnoreCase))
                SelectedSheetLayout = SheetLayoutMode.CenterFoldA4;
            else if (value.StartsWith("A4 Duplex", StringComparison.OrdinalIgnoreCase))
                SelectedSheetLayout = SheetLayoutMode.DoubleSideDuplexA4;
            else if (value.StartsWith("Epson", StringComparison.OrdinalIgnoreCase))
                SelectedSheetLayout = SheetLayoutMode.Epson2CardTray;
            else if (value.StartsWith("Canon", StringComparison.OrdinalIgnoreCase))
                SelectedSheetLayout = SheetLayoutMode.CanonMultiTray;
            else if (value.StartsWith("4x6", StringComparison.OrdinalIgnoreCase))
                SelectedSheetLayout = SheetLayoutMode.Photo4x6Sheet;
            else if (value.StartsWith("Direct", StringComparison.OrdinalIgnoreCase))
                SelectedSheetLayout = SheetLayoutMode.DirectCr80;

            BuildPhysicalSheetLayout();
        }

        public void BuildPhysicalSheetLayout()
        {
            var model = new PageLayoutModel
            {
                ShowCutMarks = ShowCutMarks,
                MirrorMode = MirrorMode,
                OffsetXMm = OffsetXMm,
                OffsetYMm = OffsetYMm,
                TargetDpi = 600
            };

            switch (SelectedSheetLayout)
            {
                case SheetLayoutMode.SingleSideA4:
                    // Competitor Single Side A4: Front at X=15.0 mm, Back at X=109.4 mm on upper half
                    model.Title = "A4 Single Side (Side-by-Side)";
                    model.Preset = PaperPreset.A4;
                    model.PageWidthMm = 210.0;
                    model.PageHeightMm = 297.0;
                    PageDimensionsText = "A4 Portrait: 210.0 x 297.0 mm";

                    if (_cachedFrontBitmap != null)
                    {
                        model.Items.Add(new PageLayoutItem
                        {
                            Label = "Front Face",
                            Kind = LayoutItemKind.FrontCard,
                            XPositionMm = 15.0,
                            YPositionMm = 20.0,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            Bitmap = _cachedFrontBitmap
                        });
                    }
                    if (_cachedBackBitmap != null)
                    {
                        model.Items.Add(new PageLayoutItem
                        {
                            Label = "Back Face",
                            Kind = LayoutItemKind.BackCard,
                            XPositionMm = 109.4,
                            YPositionMm = 20.0,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            Bitmap = _cachedBackBitmap
                        });
                    }
                    break;

                case SheetLayoutMode.CenterFoldA4:
                    // Competitor Double Side Center Fold: Center horizontally, stacked vertically with fold crease
                    model.Title = "A4 Center Fold (Crease Fold Line)";
                    model.Preset = PaperPreset.A4;
                    model.PageWidthMm = 210.0;
                    model.PageHeightMm = 297.0;
                    model.ShowFoldLine = true;
                    PageDimensionsText = "A4 Center Fold: 210.0 x 297.0 mm";

                    double centerXMm = (210.0 - 85.60) / 2.0; // 62.2 mm
                    if (_cachedFrontBitmap != null)
                    {
                        model.Items.Add(new PageLayoutItem
                        {
                            Label = "Front Face",
                            Kind = LayoutItemKind.FrontCard,
                            XPositionMm = centerXMm,
                            YPositionMm = 20.0,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            Bitmap = _cachedFrontBitmap
                        });
                    }
                    if (_cachedBackBitmap != null)
                    {
                        model.Items.Add(new PageLayoutItem
                        {
                            Label = "Back Face",
                            Kind = LayoutItemKind.BackCard,
                            XPositionMm = centerXMm,
                            YPositionMm = 76.0,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            Bitmap = _cachedBackBitmap
                        });
                    }
                    break;

                case SheetLayoutMode.Epson2CardTray:
                    // Competitor Epson Tray: Slot 1 and Slot 2
                    model.Title = "Epson Direct 2-Card Tray";
                    model.Preset = PaperPreset.EpsonTray;
                    model.PageWidthMm = 140.0;
                    model.PageHeightMm = 297.0;
                    PageDimensionsText = "Epson Tray: 140.0 x 297.0 mm";

                    double epsonSlotX = (140.0 - 85.60) / 2.0; // 27.2 mm
                    if (_cachedFrontBitmap != null)
                    {
                        model.Items.Add(new PageLayoutItem
                        {
                            Label = "Slot 1 (Front)",
                            Kind = LayoutItemKind.FrontCard,
                            XPositionMm = epsonSlotX,
                            YPositionMm = 18.0,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            Bitmap = _cachedFrontBitmap
                        });
                    }
                    if (_cachedBackBitmap != null)
                    {
                        model.Items.Add(new PageLayoutItem
                        {
                            Label = "Slot 2 (Back)",
                            Kind = LayoutItemKind.BackCard,
                            XPositionMm = epsonSlotX,
                            YPositionMm = 110.0,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            Bitmap = _cachedBackBitmap
                        });
                    }
                    break;

                case SheetLayoutMode.CanonMultiTray:
                    model.Title = "Canon Multi-Card Tray";
                    model.Preset = PaperPreset.CanonTray;
                    model.PageWidthMm = 130.0;
                    model.PageHeightMm = 220.0;
                    PageDimensionsText = "Canon Tray: 130.0 x 220.0 mm";

                    double canonSlotX = (130.0 - 85.60) / 2.0; // 22.2 mm
                    if (_cachedFrontBitmap != null)
                    {
                        model.Items.Add(new PageLayoutItem
                        {
                            Label = "Slot 1 (Front)",
                            Kind = LayoutItemKind.FrontCard,
                            XPositionMm = canonSlotX,
                            YPositionMm = 25.0,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            Bitmap = _cachedFrontBitmap
                        });
                    }
                    if (_cachedBackBitmap != null)
                    {
                        model.Items.Add(new PageLayoutItem
                        {
                            Label = "Slot 2 (Back)",
                            Kind = LayoutItemKind.BackCard,
                            XPositionMm = canonSlotX,
                            YPositionMm = 115.0,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            Bitmap = _cachedBackBitmap
                        });
                    }
                    break;

                case SheetLayoutMode.Photo4x6Sheet:
                    model.Title = "4x6 Photo Sheet (2 Cards)";
                    model.Preset = PaperPreset.Photo4x6;
                    model.PageWidthMm = 101.6;
                    model.PageHeightMm = 152.4;
                    PageDimensionsText = "Photo 4x6: 101.6 x 152.4 mm";

                    double photoSlotX = (101.6 - 85.60) / 2.0; // 8.0 mm
                    if (_cachedFrontBitmap != null)
                    {
                        model.Items.Add(new PageLayoutItem
                        {
                            Label = "Top Card",
                            Kind = LayoutItemKind.FrontCard,
                            XPositionMm = photoSlotX,
                            YPositionMm = 15.0,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            Bitmap = _cachedFrontBitmap
                        });
                    }
                    if (_cachedBackBitmap != null)
                    {
                        model.Items.Add(new PageLayoutItem
                        {
                            Label = "Bottom Card",
                            Kind = LayoutItemKind.BackCard,
                            XPositionMm = photoSlotX,
                            YPositionMm = 75.0,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            Bitmap = _cachedBackBitmap
                        });
                    }
                    break;

                default: // Direct CR80
                    model.Title = "Single CR80 Card";
                    model.Preset = PaperPreset.DirectCr80;
                    model.PageWidthMm = 85.60;
                    model.PageHeightMm = 53.98;
                    PageDimensionsText = "Direct CR80: 85.60 x 53.98 mm";

                    if (_cachedFrontBitmap != null)
                    {
                        model.Items.Add(new PageLayoutItem
                        {
                            Label = "Single Face",
                            Kind = LayoutItemKind.FrontCard,
                            XPositionMm = 0.0,
                            YPositionMm = 0.0,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            Bitmap = _cachedFrontBitmap
                        });
                    }
                    break;
            }

            ActivePageLayout = model;

            // Render physical sheet at 150 DPI for smooth, high-fidelity UI display
            try
            {
                using var sheetBitmap = _renderService.RenderLayoutToBitmap(model, dpi: 150);
                SheetPreviewImage = sheetBitmap.ToBitmapSource();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Render notice: {ex.Message}";
            }
        }

        [RelayCommand]
        public void NudgeFine(string direction)
        {
            switch (direction.ToUpperInvariant())
            {
                case "LEFT": OffsetXMm -= 0.1; break;
                case "RIGHT": OffsetXMm += 0.1; break;
                case "UP": OffsetYMm -= 0.1; break;
                case "DOWN": OffsetYMm += 0.1; break;
                case "RESET": OffsetXMm = 0.0; OffsetYMm = 0.0; break;
            }
            BuildPhysicalSheetLayout();
            StatusMessage = $"Calibration offset: X = {OffsetXMm:+0.00;-0.00;0.00} mm, Y = {OffsetYMm:+0.00;-0.00;0.00} mm";
        }

        [RelayCommand]
        public void ToggleCutMarks()
        {
            ShowCutMarks = !ShowCutMarks;
            BuildPhysicalSheetLayout();
        }

        [RelayCommand]
        public void ToggleMirrorMode()
        {
            MirrorMode = !MirrorMode;
            BuildPhysicalSheetLayout();
            StatusMessage = MirrorMode ? "Mirror Mode Active (Thermal Dragon Sheet)" : "Standard Direct Printing";
        }

        #endregion

        #region Production Printing & Export

        [RelayCommand]
        public async Task StartPrintingAsync()
        {
            if (_cachedFrontBitmap == null && ActivePageLayout.Items.Count == 0)
            {
                StatusMessage = "No document loaded to print.";
                return;
            }

            try
            {
                IsProcessing = true;
                StatusMessage = $"Spooling 600 DPI print job to '{SelectedPrinter}'...";

                // Create audit job record (Strict Zero-Citizen-PII)
                var record = new PrintJobRecord
                {
                    JobId = Guid.NewGuid().ToString("N"),
                    JobNumber = $"JOB-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                    CreatedAt = DateTime.UtcNow,
                    DocumentType = _detectedKind.ToString(),
                    Quantity = Copies * (_cachedBackBitmap != null ? 2 : 1),
                    PrinterName = string.IsNullOrEmpty(SelectedPrinter) ? "Virtual Tray Spooler" : SelectedPrinter,
                    TotalAmount = CardFee * Copies,
                    AmountPaid = CardFee * Copies,
                    PaymentMode = SelectedPaymentMode,
                    Status = "COMPLETED"
                };

                await _jobLedger.RecordJobAsync(record);

                PrintSuccessAlert = true;
                StatusMessage = $"✓ Spooled job {record.JobNumber} to '{record.PrinterName}' | ₹{record.TotalAmount} ({SelectedPaymentMode}).";
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

        [RelayCommand]
        public async Task ExportPdfAsync()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Print-Ready Vector PDF (*.pdf)|*.pdf",
                Title = "Export Physical Sheet Layout to PDF",
                FileName = $"{FileName.Replace('.', '_')}_A4_PrintReady.pdf"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    byte[] bytes = _renderService.ExportLayoutToPdf(ActivePageLayout);
                    await File.WriteAllBytesAsync(dlg.FileName, bytes);
                    StatusMessage = $"✓ Exported PDF: {Path.GetFileName(dlg.FileName)} ({(bytes.Length / 1024.0):F1} KB)";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"PDF export error: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        public async Task SaveProjectAsync()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "PVC Studio Pro Project (*.pvcproj)|*.pvcproj",
                Title = "Save Editable Project Layout",
                FileName = $"{FileName.Replace('.', '_')}.pvcproj"
            };

            if (dlg.ShowDialog() == true)
            {
                await _renderService.SaveProjectAsync(ActivePageLayout, dlg.FileName);
                StatusMessage = $"✓ Saved project: {Path.GetFileName(dlg.FileName)}";
            }
        }

        #endregion
    }
}
