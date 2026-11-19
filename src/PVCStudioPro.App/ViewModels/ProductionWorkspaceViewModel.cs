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
    public partial class ProductionWorkspaceViewModel : ObservableObject
    {
        private readonly IDocumentImportService _importService;
        private readonly IPdfDocumentService _pdfDocumentService;
        private readonly IDocumentTypeDetectionService _detectionService;
        private readonly ICardDetectionService _cardDetection;
        private readonly ICardDetectionEngine _cardDetectionEngine;
        private readonly IManualCropService _cropService;
        private readonly IPhysicalPageRenderService _renderService;
        private readonly IPhysicalPageLayoutEngine _layoutEngine;
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
        private bool _isMaskApplied = false;

        [ObservableProperty]
        private BitmapSource? _frontCardImage;

        [ObservableProperty]
        private BitmapSource? _backCardImage;

        [ObservableProperty]
        private bool _hasFrontCard = false;

        [ObservableProperty]
        private bool _hasBackCard = false;

        [ObservableProperty]
        private PrintJobLifecycleStatus _currentPrintStatus = PrintJobLifecycleStatus.SubmittedToSpooler;

        [ObservableProperty]
        private string _printStatusText = "IDLE";

        [ObservableProperty]
        private bool _isReprintVerificationPromptVisible = false;

        [ObservableProperty]
        private PrintSpoolJob? _activePrintJob;

        [ObservableProperty]
        private PrintJobAttempt? _activePrintAttempt;

        [ObservableProperty]
        private ObservableCollection<TemplateOptionItem> _templateOptions = new();

        [ObservableProperty]
        private TemplateOptionItem? _selectedTemplateOption;

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
        private bool _showFoldLine = false;

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
        private string _statusMessage = "PVC Studio Pro Ready | 100% Offline Local Processing";

        [ObservableProperty]
        private bool _printSuccessAlert = false;

        #endregion

        #region Integrated Crop Drawer State

        [ObservableProperty]
        private bool _isCropDrawerOpen = false;

        [ObservableProperty]
        private BitmapSource? _cropSourceImage;

        [ObservableProperty]
        private string _cropTargetFace = "Front Card (Slot 1)";

        [ObservableProperty]
        private double _cropX = 210.0;

        [ObservableProperty]
        private double _cropY = 2595.0;

        [ObservableProperty]
        private double _cropWidth = 1045.0;

        [ObservableProperty]
        private double _cropHeight = 665.0;

        [ObservableProperty]
        private double _straightenAngle = 0.0;

        [ObservableProperty]
        private CropAspectRatio _selectedCropRatio = CropAspectRatio.Cr80Card;

        [ObservableProperty]
        private bool _isPerspectiveCropMode = false;

        [ObservableProperty]
        private float _cropCorner1X = 210f;
        [ObservableProperty]
        private float _cropCorner1Y = 2595f;
        [ObservableProperty]
        private float _cropCorner2X = 1255f;
        [ObservableProperty]
        private float _cropCorner2Y = 2595f;
        [ObservableProperty]
        private float _cropCorner3X = 1255f;
        [ObservableProperty]
        private float _cropCorner3Y = 3260f;
        [ObservableProperty]
        private float _cropCorner4X = 210f;
        [ObservableProperty]
        private float _cropCorner4Y = 3260f;

        [ObservableProperty]
        private double _customCardWidthMm = 85.60;

        [ObservableProperty]
        private double _customCardHeightMm = 53.98;

        [ObservableProperty]
        private bool _isCustomDimensionsVisible = false;

        [ObservableProperty]
        private string _customPresetName = string.Empty;

        [ObservableProperty]
        private bool _isSavePresetDialogOpen = false;

        [ObservableProperty]
        private ObservableCollection<string> _cropFaceOptions = new()
        {
            "Front Card (Slot 1)",
            "Back Card (Slot 2)",
            "Single Card (Both Slots)"
        };

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
            IDocumentSessionManager? sessionManager = null,
            IPhysicalPageLayoutEngine? layoutEngine = null)
        {
            _importService = importService;
            _pdfDocumentService = pdfDocumentService;
            _detectionService = detectionService;
            _cardDetection = cardDetection;
            _cardDetectionEngine = cardDetection as ICardDetectionEngine ?? new CardDetectionService(_templateService, _detectionService);
            _cropService = cropService;
            _renderService = renderService;
            _layoutEngine = layoutEngine ?? new PhysicalPageLayoutEngine();
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
            LoadTemplateOptions();
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

        public static bool IsVirtualPrinter(string? printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName)) return true;
            return printerName.Contains("OneNote", StringComparison.OrdinalIgnoreCase) ||
                   printerName.Contains("Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase) ||
                   printerName.Contains("Fax", StringComparison.OrdinalIgnoreCase) ||
                   printerName.Contains("XPS Document Writer", StringComparison.OrdinalIgnoreCase) ||
                   printerName.Contains("Virtual", StringComparison.OrdinalIgnoreCase);
        }

        public bool CanStartPrinting => HasLoadedDocument && !string.IsNullOrWhiteSpace(SelectedPrinter) && !IsVirtualPrinter(SelectedPrinter);

        partial void OnHasLoadedDocumentChanged(bool value)
        {
            StartPrintingCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedPrinterChanged(string value)
        {
            StartPrintingCommand.NotifyCanExecuteChanged();
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

                    if (preferred == null)
                    {
                        foreach (var p in PrinterList)
                        {
                            if (!IsVirtualPrinter(p))
                            {
                                preferred = p;
                                break;
                            }
                        }
                    }

                    // Never default to OneNote or virtual printers. If no physical hardware is detected,
                    // keep SelectedPrinter empty so user is guided to select or attach hardware.
                    SelectedPrinter = preferred ?? string.Empty;
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
                        docInfo.PageHeightMm,
                        applyMask: IsMaskAadhaar);

                    _detectedKind = extraction.DetectedKind;
                    DetectedDocTitle = extraction.MatchedTemplate?.DisplayName ?? $"{_detectedKind}";
                    DetectedBadge = $"{extraction.DetectedKind.ToString().ToUpperInvariant()} • AUTO-DETECTED";

                    ApplyDetectionConfidence(extraction.Confidence, extraction.FrontRect);
                    SetExtractedCards(extraction.FrontCard, extraction.BackCard, extraction.IsMaskApplied);
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
                        string.Empty,
                        applyMask: IsMaskAadhaar);

                    _detectedKind = extraction.DetectedKind;
                    DetectedDocTitle = extraction.MatchedTemplate?.DisplayName ?? $"{_detectedKind}";
                    DetectedBadge = $"{extraction.DetectedKind.ToString().ToUpperInvariant()} • IMAGE SCAN";

                    ApplyDetectionConfidence(extraction.Confidence, extraction.FrontRect);
                    if (extraction.FrontCard != null)
                    {
                        SetExtractedCards(extraction.FrontCard, extraction.BackCard, extraction.IsMaskApplied);
                    }
                    else
                    {
                        // Single card image
                        SetExtractedCards(_cachedPageBitmap.Copy(), null, isMaskApplied: false);
                    }
                    if (extraction.MatchedTemplate != null)
                    {
                        var opt = TemplateOptions.FirstOrDefault(o => o.TemplateId == extraction.MatchedTemplate.TemplateId);
                        if (opt != null)
                        {
                            SelectedTemplateOption = opt;
                        }
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

        private void SetExtractedCards(SKBitmap? front, SKBitmap? back, bool isMaskApplied = false)
        {
            _cachedFrontBitmap?.Dispose();
            _cachedBackBitmap?.Dispose();

            IsMaskApplied = isMaskApplied;

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
            return NormalizeCardBitmap(source, CropAspectRatio.Cr80Card);
        }

        private static SKBitmap NormalizeCardBitmap(
            SKBitmap source,
            CropAspectRatio ratio = CropAspectRatio.Cr80Card,
            double customW = 85.60,
            double customH = 53.98)
        {
            if (ratio == CropAspectRatio.Freeform)
            {
                if (source.ColorType == SKColorType.Bgra8888) return source;
                var bgra = new SKBitmap(source.Width, source.Height, SKColorType.Bgra8888, source.AlphaType);
                using (var c = new SKCanvas(bgra))
                {
                    c.DrawBitmap(source, 0, 0, SKSamplingOptions.Default, null);
                    c.Flush();
                }
                source.Dispose();
                return bgra;
            }

            int targetW;
            int targetH;
            switch (ratio)
            {
                case CropAspectRatio.PassportPhoto:
                    targetW = (int)Math.Round(35.0 * 600.0 / 25.4); // 827
                    targetH = (int)Math.Round(45.0 * 600.0 / 25.4); // 1063
                    break;
                case CropAspectRatio.StampPhoto:
                    targetW = (int)Math.Round(25.0 * 600.0 / 25.4); // 591
                    targetH = (int)Math.Round(30.0 * 600.0 / 25.4); // 709
                    break;
                case CropAspectRatio.AadhaarFullPouch:
                    targetW = (int)Math.Round(88.50 * 600.0 / 25.4); // 2091 px @ 600 DPI
                    targetH = (int)Math.Round(56.50 * 600.0 / 25.4); // 1335 px @ 600 DPI
                    break;
                case CropAspectRatio.Cr80Card:
                default:
                    if (Math.Abs(customW - 85.60) > 0.05 || Math.Abs(customH - 53.98) > 0.05)
                    {
                        targetW = Math.Max(100, (int)Math.Round(customW * 600.0 / 25.4));
                        targetH = Math.Max(100, (int)Math.Round(customH * 600.0 / 25.4));
                    }
                    else
                    {
                        targetW = 2022; // 85.60 mm @ 600 DPI
                        targetH = 1275; // 53.98 mm @ 600 DPI
                    }
                    break;
            }

            if (source.Width == targetW && source.Height == targetH && source.ColorType == SKColorType.Bgra8888)
            {
                return source;
            }

            var dest = new SKBitmap(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(dest);
            canvas.Clear(SKColors.White);

            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            var destRect = new SKRect(0, 0, targetW, targetH);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(source, destRect, sampling, paint);
            canvas.Flush();

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
            StraightenAngle = 0.0;
            CropCorner1X = (float)CropX;
            CropCorner1Y = (float)CropY;
            CropCorner2X = (float)(CropX + CropWidth);
            CropCorner2Y = (float)CropY;
            CropCorner3X = (float)(CropX + CropWidth);
            CropCorner3Y = (float)(CropY + CropHeight);
            CropCorner4X = (float)CropX;
            CropCorner4Y = (float)(CropY + CropHeight);
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
            StraightenAngle = 0.0;
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
            CropCorner1X = (float)CropX;
            CropCorner1Y = (float)CropY;
            CropCorner2X = (float)(CropX + CropWidth);
            CropCorner2Y = (float)CropY;
            CropCorner3X = (float)(CropX + CropWidth);
            CropCorner3Y = (float)(CropY + CropHeight);
            CropCorner4X = (float)CropX;
            CropCorner4Y = (float)(CropY + CropHeight);
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

            SKBitmap workingSource = _cachedPageBitmap;
            bool disposeWorkingSource = false;

            if (Math.Abs(StraightenAngle) > 0.001)
            {
                workingSource = _cropService.RotateBitmap(_cachedPageBitmap, StraightenAngle, expandBounds: false);
                disposeWorkingSource = true;
            }

            try
            {
                SKBitmap extracted;
                int w = (int)CropWidth;
                int h = (int)CropHeight;

                if (IsPerspectiveCropMode)
                {
                    var quad = new QuadrilateralPoints(
                        new SKPoint(CropCorner1X, CropCorner1Y),
                        new SKPoint(CropCorner2X, CropCorner2Y),
                        new SKPoint(CropCorner3X, CropCorner3Y),
                        new SKPoint(CropCorner4X, CropCorner4Y)
                    );
                    extracted = _cropService.CropPerspective(workingSource, quad, SelectedCropRatio, dpi: 600);
                }
                else
                {
                    int imgW = workingSource.Width;
                    int imgH = workingSource.Height;

                    int x = Math.Clamp((int)CropX, 0, imgW - 10);
                    int y = Math.Clamp((int)CropY, 0, imgH - 10);
                    w = Math.Clamp((int)CropWidth, 10, imgW - x);
                    h = Math.Clamp((int)CropHeight, 10, imgH - y);

                    var rect = new SKRectI(x, y, x + w, y + h);
                    extracted = new SKBitmap(rect.Width, rect.Height, workingSource.ColorType, workingSource.AlphaType);
                    if (!workingSource.ExtractSubset(extracted, rect))
                    {
                        using var canvas = new SKCanvas(extracted);
                        canvas.DrawBitmap(workingSource, rect, new SKRect(0, 0, w, h), SKSamplingOptions.Default);
                    }
                }

                var normalized = NormalizeCardBitmap(extracted, SelectedCropRatio, CustomCardWidthMm, CustomCardHeightMm);

                if (CropTargetFace.Contains("Front", StringComparison.OrdinalIgnoreCase))
                {
                    _cachedFrontBitmap?.Dispose();
                    _cachedFrontBitmap = normalized;
                    FrontCardImage = _cachedFrontBitmap.ToBitmapSource();
                    HasFrontCard = true;
                }
                else if (CropTargetFace.Contains("Back", StringComparison.OrdinalIgnoreCase))
                {
                    _cachedBackBitmap?.Dispose();
                    _cachedBackBitmap = normalized;
                    BackCardImage = _cachedBackBitmap.ToBitmapSource();
                    HasBackCard = true;
                }
                else
                {
                    // Single card or both slots
                    _cachedFrontBitmap?.Dispose();
                    _cachedFrontBitmap = normalized;
                    FrontCardImage = _cachedFrontBitmap.ToBitmapSource();
                    HasFrontCard = true;
                }

                BuildPhysicalSheetLayout();
                StatusMessage = $"✓ Applied custom crop to {CropTargetFace} ({w}×{h} px, Tilt: {StraightenAngle:F1}°, Ratio: {SelectedCropRatio}).";
            }
            finally
            {
                if (disposeWorkingSource)
                {
                    workingSource.Dispose();
                }
            }

            IsCropDrawerOpen = false;
        }

        [RelayCommand]
        public void RotateCropCw()
        {
            StraightenAngle = ((StraightenAngle + 90.0) % 360);
            StatusMessage = $"Straighten tilt: {StraightenAngle:F1}°";
        }

        [RelayCommand]
        public void RotateCropCcw()
        {
            StraightenAngle = ((StraightenAngle - 90.0) % 360);
            StatusMessage = $"Straighten tilt: {StraightenAngle:F1}°";
        }

        [RelayCommand]
        public void ResetStraightenAngle()
        {
            StraightenAngle = 0.0;
            StatusMessage = "Straighten angle reset to 0.0°";
        }

        [RelayCommand]
        public void FlipCropHorizontal()
        {
            if (_cachedPageBitmap != null)
            {
                var flipped = _cropService.FlipHorizontal(_cachedPageBitmap);
                _cachedPageBitmap.Dispose();
                _cachedPageBitmap = flipped;
                CropSourceImage = _cachedPageBitmap.ToBitmapSource();
                StatusMessage = "Flipped scan horizontally.";
            }
        }

        [RelayCommand]
        public void SetCropRatio(string ratioName)
        {
            switch (ratioName.ToUpperInvariant())
            {
                case "FREE":
                case "FREEFORM":
                    SelectedCropRatio = CropAspectRatio.Freeform;
                    break;
                case "AADHAAR":
                case "AADHAAR_POUCH":
                case "FULL_AADHAAR":
                    SelectedCropRatio = CropAspectRatio.AadhaarFullPouch;
                    CustomCardWidthMm = 88.50;
                    CustomCardHeightMm = 56.50;
                    break;
                case "PASSPORT":
                    SelectedCropRatio = CropAspectRatio.PassportPhoto;
                    CustomCardWidthMm = 35.00;
                    CustomCardHeightMm = 45.00;
                    break;
                case "STAMP":
                    SelectedCropRatio = CropAspectRatio.StampPhoto;
                    CustomCardWidthMm = 25.00;
                    CustomCardHeightMm = 30.00;
                    break;
                case "CR80":
                default:
                    SelectedCropRatio = CropAspectRatio.Cr80Card;
                    CustomCardWidthMm = 85.60;
                    CustomCardHeightMm = 53.98;
                    break;
            }
            StatusMessage = $"Crop aspect ratio: {SelectedCropRatio}";
        }

        [RelayCommand]
        public void TogglePerspectiveCropMode()
        {
            IsPerspectiveCropMode = !IsPerspectiveCropMode;
            if (IsPerspectiveCropMode)
            {
                CropCorner1X = (float)CropX;
                CropCorner1Y = (float)CropY;
                CropCorner2X = (float)(CropX + CropWidth);
                CropCorner2Y = (float)CropY;
                CropCorner3X = (float)(CropX + CropWidth);
                CropCorner3Y = (float)(CropY + CropHeight);
                CropCorner4X = (float)CropX;
                CropCorner4Y = (float)(CropY + CropHeight);
            }
            StatusMessage = IsPerspectiveCropMode ? "4-Corner Perspective Mode enabled." : "Standard 8-Handle Rectangular Mode enabled.";
        }

        [RelayCommand]
        public void OpenSavePresetDialog()
        {
            CustomPresetName = $"Custom {CustomCardWidthMm:0.0}x{CustomCardHeightMm:0.0} mm";
            IsSavePresetDialogOpen = true;
        }

        [RelayCommand]
        public void CloseSavePresetDialog()
        {
            IsSavePresetDialogOpen = false;
        }

        [RelayCommand]
        public async Task ConfirmSavePresetAsync()
        {
            if (string.IsNullOrWhiteSpace(CustomPresetName))
            {
                StatusMessage = "Please enter a valid name for the custom layout preset.";
                return;
            }

            try
            {
                var template = new CardTemplate
                {
                    TemplateId = "custom_" + Guid.NewGuid().ToString("N")[..8],
                    DisplayName = CustomPresetName.Trim(),
                    Category = "CUSTOM_PRESETS",
                    DocumentType = "CUSTOM",
                    Faces = new CardFaces
                    {
                        Front = new FaceDefinition
                        {
                            SourcePage = 0,
                            CropRectMm = new MillimeterRect
                            {
                                X = 15.0,
                                Y = 15.0,
                                Width = CustomCardWidthMm,
                                Height = CustomCardHeightMm
                            }
                        },
                        Back = new FaceDefinition
                        {
                            SourcePage = 0,
                            CropRectMm = new MillimeterRect
                            {
                                X = 15.0 + CustomCardWidthMm + 8.8,
                                Y = 15.0,
                                Width = CustomCardWidthMm,
                                Height = CustomCardHeightMm
                            }
                        }
                    }
                };

                await _templateService.SaveCustomTemplateAsync(template);
                LoadTemplateOptions();

                var match = TemplateOptions.FirstOrDefault(o => o.TemplateId == template.TemplateId);
                if (match != null)
                {
                    SelectedTemplateOption = match;
                }

                IsSavePresetDialogOpen = false;
                BuildPhysicalSheetLayout();
                StatusMessage = $"✓ Successfully saved custom layout preset '{template.DisplayName}' ({CustomCardWidthMm:0.0}×{CustomCardHeightMm:0.0} mm).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save custom preset: {ex.Message}";
            }
        }

        [RelayCommand]
        public void SelectOtherCardOption(string templateId)
        {
            var opt = TemplateOptions.FirstOrDefault(o => string.Equals(o.TemplateId, templateId, StringComparison.OrdinalIgnoreCase));
            if (opt != null)
            {
                SelectedTemplateOption = opt;
                return;
            }

            var otherTemplates = _templateService.GetOtherCardTemplates();
            var target = otherTemplates.FirstOrDefault(t => string.Equals(t.TemplateId, templateId, StringComparison.OrdinalIgnoreCase))
                ?? otherTemplates.FirstOrDefault();

            if (target != null && HasLoadedDocument)
            {
                ApplyTemplateCoordinates(target);
                IsCustomDimensionsVisible = templateId.Contains("custom", StringComparison.OrdinalIgnoreCase);
                StatusMessage = $"✓ Selected preset: {target.DisplayName}.";
            }
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

        partial void OnShowCutMarksChanged(bool value) => BuildPhysicalSheetLayout();
        partial void OnShowFoldLineChanged(bool value) => BuildPhysicalSheetLayout();
        partial void OnMirrorModeChanged(bool value) => BuildPhysicalSheetLayout();
        partial void OnOffsetXMmChanged(double value) => BuildPhysicalSheetLayout();
        partial void OnOffsetYMmChanged(double value) => BuildPhysicalSheetLayout();
        partial void OnCustomCardWidthMmChanged(double value) => BuildPhysicalSheetLayout();
        partial void OnCustomCardHeightMmChanged(double value) => BuildPhysicalSheetLayout();

        public void BuildPhysicalSheetLayout()
        {
            var model = _layoutEngine.CreateLayout(
                SelectedSheetLayout,
                _cachedFrontBitmap,
                _cachedBackBitmap,
                ShowCutMarks,
                ShowFoldLine,
                MirrorMode,
                OffsetXMm,
                OffsetYMm,
                targetDpi: 600);

            PageDimensionsText = $"{model.Title}: {model.PageWidthMm:0.0} x {model.PageHeightMm:0.0} mm";
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

        partial void OnSelectedTemplateOptionChanged(TemplateOptionItem? value)
        {
            IsCustomDimensionsVisible = value != null && value.TemplateId.Contains("custom", StringComparison.OrdinalIgnoreCase);

            if (value?.Template != null && HasLoadedDocument)
            {
                ApplyTemplateCoordinates(value.Template);
            }
        }

        public void LoadTemplateOptions()
        {
            var options = _templateService.GetTemplateOptions();
            TemplateOptions = new ObservableCollection<TemplateOptionItem>(options);
            if (TemplateOptions.Count > 0 && SelectedTemplateOption == null)
            {
                SelectedTemplateOption = TemplateOptions[0];
            }
        }

        public void ApplyTemplateCoordinates(CardTemplate template)
        {
            if (template == null || _cachedPageBitmap == null) return;

            try
            {
                IsProcessing = true;
                double pageW = 210.0;
                double pageH = 297.0;
                double scaleX = (double)_cachedPageBitmap.Width / pageW;
                double scaleY = (double)_cachedPageBitmap.Height / pageH;

                var frontMm = template.Faces.Front.CropRectMm;
                int fx = Math.Clamp((int)Math.Round(frontMm.X * scaleX), 0, _cachedPageBitmap.Width - 10);
                int fy = Math.Clamp((int)Math.Round(frontMm.Y * scaleY), 0, _cachedPageBitmap.Height - 10);
                int fw = Math.Clamp((int)Math.Round(frontMm.Width * scaleX), 10, _cachedPageBitmap.Width - fx);
                int fh = Math.Clamp((int)Math.Round(frontMm.Height * scaleY), 10, _cachedPageBitmap.Height - fy);

                var frontRect = new SKRectI(fx, fy, fx + fw, fy + fh);
                using var frontSubset = new SKBitmap(fw, fh, _cachedPageBitmap.ColorType, _cachedPageBitmap.AlphaType);
                using (var c = new SKCanvas(frontSubset))
                {
                    c.DrawBitmap(_cachedPageBitmap, frontRect, new SKRect(0, 0, fw, fh), SKSamplingOptions.Default);
                }

                SKBitmap? backSubset = null;
                if (template.Faces.Back != null)
                {
                    var backMm = template.Faces.Back.CropRectMm;
                    int bx = Math.Clamp((int)Math.Round(backMm.X * scaleX), 0, _cachedPageBitmap.Width - 10);
                    int by = Math.Clamp((int)Math.Round(backMm.Y * scaleY), 0, _cachedPageBitmap.Height - 10);
                    int bw = Math.Clamp((int)Math.Round(backMm.Width * scaleX), 10, _cachedPageBitmap.Width - bx);
                    int bh = Math.Clamp((int)Math.Round(backMm.Height * scaleY), 10, _cachedPageBitmap.Height - by);

                    var backRect = new SKRectI(bx, by, bx + bw, by + bh);
                    backSubset = new SKBitmap(bw, bh, _cachedPageBitmap.ColorType, _cachedPageBitmap.AlphaType);
                    using var cb = new SKCanvas(backSubset);
                    cb.DrawBitmap(_cachedPageBitmap, backRect, new SKRect(0, 0, bw, bh), SKSamplingOptions.Default);
                }

                SetExtractedCards(frontSubset, backSubset, isMaskApplied: IsMaskApplied);
                backSubset?.Dispose();

                DetectedDocTitle = template.DisplayName;
                DetectedBadge = $"{template.Category.Replace("_", " ")} • OVERRIDE";
                BuildPhysicalSheetLayout();
                StatusMessage = $"✓ Applied template '{template.DisplayName}' ({frontMm.Width:F1}x{frontMm.Height:F1} mm).";
            }
            finally
            {
                IsProcessing = false;
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

        [RelayCommand(CanExecute = nameof(CanStartPrinting))]
        public async Task StartPrintingAsync()
        {
            if (IsMaskAadhaar && !IsMaskApplied)
            {
                StatusMessage = "Operation Blocked: UIDAI Masked Aadhaar was requested, but mask verification failed. Export and printing blocked to prevent accidental PII exposure.";
                return;
            }

            if (_cachedFrontBitmap == null && ActivePageLayout.Items.Count == 0)
            {
                StatusMessage = "No document loaded to print.";
                return;
            }

            try
            {
                IsProcessing = true;
                string printer = string.IsNullOrEmpty(SelectedPrinter) ? "Virtual Tray Spooler" : SelectedPrinter;
                StatusMessage = $"Spooling 600 DPI print job to '{printer}'...";

                ActivePrintJob = new PrintSpoolJob
                {
                    DocumentTitle = !string.IsNullOrEmpty(FileName) ? FileName : "PVC Studio Pro Card Print",
                    CustomerName = "Walk-in Customer"
                };

                ActivePrintAttempt = await _printService.ExecutePrintJobAsync(
                    ActivePrintJob,
                    ActivePageLayout,
                    printer,
                    isReprint: false);

                CurrentPrintStatus = ActivePrintAttempt.Status;

                if (ActivePrintAttempt.Status == PrintJobLifecycleStatus.SpoolerProcessingCompleted)
                {
                    IsReprintVerificationPromptVisible = true;
                    PrintStatusText = "SPOOLER COMPLETED • PLEASE VERIFY OUTPUT";
                    StatusMessage = $"Spooler transmitted job to '{printer}'. Please visually verify printed output.";
                }
                else if (ActivePrintAttempt.Status == PrintJobLifecycleStatus.SpoolerErrorOrCancelled)
                {
                    IsReprintVerificationPromptVisible = false;
                    PrintStatusText = $"ERROR: {ActivePrintAttempt.DefectReason}";
                    StatusMessage = $"Print failed: {ActivePrintAttempt.DefectReason}";
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

        [RelayCommand]
        public async Task ConfirmPrintOutputAsync()
        {
            if (ActivePrintAttempt == null) return;

            await _printService.RecordOperatorConfirmationAsync(ActivePrintAttempt, confirmedSuccessful: true);
            CurrentPrintStatus = PrintJobLifecycleStatus.PhysicalOutputConfirmedByOperator;
            IsReprintVerificationPromptVisible = false;
            PrintStatusText = "OUTPUT CONFIRMED";

            // Record transaction in ledger only upon operator visual confirmation!
            if (ActivePrintAttempt.IsBillable)
            {
                var record = new PrintJobRecord
                {
                    JobId = ActivePrintJob?.JobId ?? Guid.NewGuid().ToString("N"),
                    JobNumber = $"JOB-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                    CreatedAt = DateTime.UtcNow,
                    DocumentType = _detectedKind.ToString(),
                    Quantity = Copies * (_cachedBackBitmap != null ? 2 : 1),
                    PrinterName = ActivePrintAttempt.PrinterName,
                    TotalAmount = CardFee * Copies,
                    AmountPaid = CardFee * Copies,
                    PaymentMode = SelectedPaymentMode,
                    Status = "COMPLETED"
                };
                await _jobLedger.RecordJobAsync(record);
            }

            PrintSuccessAlert = true;
            StatusMessage = $"✓ Physical card output confirmed on '{ActivePrintAttempt.PrinterName}'. Total: ₹{CardFee * Copies} ({SelectedPaymentMode}).";
        }

        [RelayCommand]
        public async Task ReportDefectAndReprintAsync(string? defectReason = null)
        {
            if (ActivePrintAttempt == null || ActivePrintJob == null) return;

            string reason = string.IsNullOrWhiteSpace(defectReason) ? "Defective Print Output" : defectReason;
            await _printService.RecordOperatorConfirmationAsync(ActivePrintAttempt, confirmedSuccessful: false, reason);
            CurrentPrintStatus = PrintJobLifecycleStatus.PhysicalOutputNotVerified;

            IsProcessing = true;
            try
            {
                string printer = ActivePrintAttempt.PrinterName;
                StatusMessage = $"Spooling non-billable hardware reprint to '{printer}' ({reason})...";

                ActivePrintAttempt = await _printService.ExecutePrintJobAsync(
                    ActivePrintJob,
                    ActivePageLayout,
                    printer,
                    isReprint: true,
                    defectReason: reason);

                CurrentPrintStatus = ActivePrintAttempt.Status;

                if (ActivePrintAttempt.Status == PrintJobLifecycleStatus.SpoolerProcessingCompleted)
                {
                    IsReprintVerificationPromptVisible = true;
                    PrintStatusText = $"REPRINT #{ActivePrintAttempt.AttemptNumber} SPOOLED • PLEASE VERIFY OUTPUT";
                    StatusMessage = $"Reprint #{ActivePrintAttempt.AttemptNumber} transmitted ({reason}). Please verify physical output.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Reprint error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        public async Task ExportPdfAsync()
        {
            if (IsMaskAadhaar && !IsMaskApplied)
            {
                StatusMessage = "Operation Blocked: UIDAI Masked Aadhaar was requested, but mask verification failed. Export and printing blocked to prevent accidental PII exposure.";
                return;
            }

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
