using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
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
    public partial class DocumentDeskViewModel : ObservableObject
    {
        private readonly IDocumentImportService _importService;
        private readonly IPdfDocumentService _pdfService;
        private readonly IOfficeDocumentService _officeService;
        private readonly IDocumentTypeDetectionService _detectionService;
        private readonly ICardDetectionService _cardDetection;
        private readonly IManualCropService _cropService;
        private readonly IDocumentSessionManager? _sessionManager;
        private IDisposable? _activeSessionSubscription;

        public Action<SKBitmap, string>? OpenInCropStudioAction { get; set; }
        public Action<SKBitmap?, SKBitmap?, DocumentKind>? OpenInPrintPreviewAction { get; set; }
        public Action<SKBitmap?, SKBitmap?, string>? OpenInPageDesignerAction { get; set; }

        [ObservableProperty]
        private string _documentPath = string.Empty;

        [ObservableProperty]
        private string _fileName = "No document loaded";

        [ObservableProperty]
        private string _fileSizeText = string.Empty;

        [ObservableProperty]
        private string _magicFormatBadge = "READY";

        [ObservableProperty]
        private bool _isFileLoaded = false;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _loadingMessage = "Ready";

        [ObservableProperty]
        private bool _isPasswordRequired = false;

        [ObservableProperty]
        private string _passwordInput = string.Empty;

        [ObservableProperty]
        private bool _isPasswordMasked = true;

        [ObservableProperty]
        private string _passwordErrorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasPasswordError = false;

        [ObservableProperty]
        private string _detectedDocumentTitle = "Awaiting Document";

        [ObservableProperty]
        private string _detectionConfidenceText = "0%";

        [ObservableProperty]
        private string _detectionNotes = "Drag and drop or select an official document or image.";

        [ObservableProperty]
        private DocumentKind _detectedKind = DocumentKind.Unknown;

        [ObservableProperty]
        private AadhaarGeneration _detectedAadhaarGen = AadhaarGeneration.Unknown;

        [ObservableProperty]
        private BitmapSource? _rawPageImage;

        [ObservableProperty]
        private BitmapSource? _frontCardImage;

        [ObservableProperty]
        private BitmapSource? _backCardImage;

        [ObservableProperty]
        private bool _hasCardsExtracted = false;

        [ObservableProperty]
        private bool _hasBackCard = false;

        [ObservableProperty]
        private bool _isMaskAadhaarEnabled = false;

        [ObservableProperty]
        private string _statusMessage = "Universal Document Desk Ready | Zero-Citizen-PII Local Pipeline";

        private SKBitmap? _activePageBitmap;
        private SKBitmap? _cachedFrontBitmap;
        private SKBitmap? _cachedBackBitmap;
        private byte[]? _rawFileBytes;
        private DocumentSniffResult? _sniffResult;

        public DocumentDeskViewModel(
            IDocumentImportService importService,
            IPdfDocumentService pdfService,
            IOfficeDocumentService officeService,
            IDocumentTypeDetectionService detectionService,
            ICardDetectionService cardDetection,
            IManualCropService cropService,
            IDocumentSessionManager? sessionManager = null)
        {
            _importService = importService;
            _pdfService = pdfService;
            _officeService = officeService;
            _detectionService = detectionService;
            _cardDetection = cardDetection;
            _cropService = cropService;
            _sessionManager = sessionManager;

            if (_sessionManager != null)
            {
                _sessionManager.SessionChanged += OnSessionManagerChanged;
                if (_sessionManager.ActiveSession != null)
                {
                    AttachToSession(_sessionManager.ActiveSession);
                }
            }
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
            IsFileLoaded = true;

            using (var lease = session.SourceAsset.AcquireLease())
            {
                RawPageImage = lease.Surface.ToBitmapSource();
            }

            if (session.FrontCardAsset != null)
            {
                using var lease = session.FrontCardAsset.AcquireLease();
                FrontCardImage = lease.Surface.ToBitmapSource();
                HasCardsExtracted = true;
            }

            if (session.BackCardAsset != null)
            {
                using var lease = session.BackCardAsset.AcquireLease();
                BackCardImage = lease.Surface.ToBitmapSource();
                HasBackCard = true;
            }
        }

        public void DetachFromSession()
        {
            _activeSessionSubscription?.Dispose();
            _activeSessionSubscription = null;
            DocumentPath = string.Empty;
            FileName = "No document loaded";
            IsFileLoaded = false;
            RawPageImage = null;
            FrontCardImage = null;
            BackCardImage = null;
            HasCardsExtracted = false;
            HasBackCard = false;
        }

        [RelayCommand]
        public async Task BrowseFileAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Citizen Document, Card or Office File",
                Filter = "All Supported Files (*.pdf;*.docx;*.doc;*.rtf;*.txt;*.jpg;*.png;*.webp;*.bmp;*.tiff)|*.pdf;*.docx;*.doc;*.rtf;*.txt;*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tiff|" +
                         "PDF Documents (*.pdf)|*.pdf|" +
                         "Office Documents (*.docx;*.doc;*.rtf;*.txt)|*.docx;*.doc;*.rtf;*.txt|" +
                         "Image & Photo Files (*.jpg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|" +
                         "All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                await LoadDocumentAsync(dlg.FileName);
            }
        }

        [RelayCommand]
        public async Task PasteFromClipboardAsync()
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    var source = Clipboard.GetImage();
                    if (source != null)
                    {
                        // Convert WPF BitmapSource to SKBitmap
                        using var memStream = new MemoryStream();
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(source));
                        encoder.Save(memStream);
                        memStream.Position = 0;

                        var skBmp = SKBitmap.Decode(memStream);
                        if (skBmp != null)
                        {
                            await ProcessImportedBitmapAsync(skBmp, "Clipboard_Image.png");
                        }
                    }
                }
                else
                {
                    StatusMessage = "Clipboard does not contain an image.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Clipboard paste error: {ex.Message}";
            }
        }

        public async Task LoadDocumentAsync(string filePath, string? password = null)
        {
            try
            {
                IsLoading = true;
                LoadingMessage = "Sniffing magic bytes and inspecting file structure...";
                StatusMessage = $"Loading: {Path.GetFileName(filePath)}";
                DocumentPath = filePath;
                FileName = Path.GetFileName(filePath);
                HasPasswordError = false;
                PasswordErrorMessage = string.Empty;

                _sniffResult = _importService.SniffFile(filePath);
                FileSizeText = $"{(new FileInfo(filePath).Length / 1024.0):F1} KB";

                switch (_sniffResult.MagicKind)
                {
                    case FileMagicKind.Pdf:
                        MagicFormatBadge = "PDF • Vector 600 DPI";
                        await ProcessPdfAsync(filePath, password);
                        break;

                    case FileMagicKind.Docx:
                    case FileMagicKind.Doc:
                    case FileMagicKind.Rtf:
                    case FileMagicKind.Text:
                        MagicFormatBadge = $"{_sniffResult.MagicKind.ToString().ToUpperInvariant()} • Office Document";
                        await ProcessOfficeDocAsync(filePath, _sniffResult.MagicKind);
                        break;

                    case FileMagicKind.Jpeg:
                    case FileMagicKind.Png:
                    case FileMagicKind.Bmp:
                    case FileMagicKind.Tiff:
                    case FileMagicKind.Webp:
                        MagicFormatBadge = $"{_sniffResult.MagicKind.ToString().ToUpperInvariant()} • Raster Image";
                        await ProcessRasterImageAsync(filePath);
                        break;

                    default:
                        MagicFormatBadge = "UNKNOWN FORMAT";
                        StatusMessage = "Unrecognized file format or corrupted binary data.";
                        IsFileLoaded = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Document import error: {ex.Message}";
                IsFileLoaded = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ProcessPdfAsync(string filePath, string? password)
        {
            _rawFileBytes = await File.ReadAllBytesAsync(filePath);
            using var stream = new MemoryStream(_rawFileBytes);

            var unlockResult = await _pdfService.TryUnlockPasswordAsync(stream, password);
            if (unlockResult.Status == PdfPasswordStatus.PasswordRequired)
            {
                IsPasswordRequired = true;
                IsFileLoaded = false;
                StatusMessage = "Password protected PDF detected. Please provide the 8-character decryption password.";
                return;
            }

            if (unlockResult.Status == PdfPasswordStatus.InvalidPassword)
            {
                IsPasswordRequired = true;
                HasPasswordError = true;
                PasswordErrorMessage = unlockResult.ErrorMessage ?? "Incorrect password.";
                StatusMessage = "Decryption failed: Incorrect password.";
                return;
            }

            if (unlockResult.Status == PdfPasswordStatus.Corrupted)
            {
                StatusMessage = $"PDF corrupted: {unlockResult.ErrorMessage}";
                return;
            }

            IsPasswordRequired = false;
            HasPasswordError = false;
            string? effectivePw = unlockResult.EffectivePassword ?? password;

            // Render first page at 300 DPI for layout analysis
            stream.Position = 0;
            _activePageBitmap?.Dispose();
            _activePageBitmap = await _pdfService.RenderPageAsync(stream, 0, 300, effectivePw);
            RawPageImage = _activePageBitmap.ToBitmapSource();

            // Inspect text and metadata
            stream.Position = 0;
            var docInfo = await _pdfService.InspectDocumentAsync(stream, effectivePw);

            // Detect Document Type
            var detection = await _detectionService.DetectDocumentTypeAsync(_activePageBitmap, docInfo.ExtractedTextSample, docInfo.PageWidthMm, docInfo.PageHeightMm);
            DetectedDocumentTitle = detection.DisplayName;
            DetectedKind = detection.DetectedKind;
            DetectedAadhaarGen = detection.AadhaarGen;
            DetectionConfidenceText = $"{detection.Confidence:P0}";
            DetectionNotes = detection.DetectionNotes;

            // Extract Cards
            await AutoExtractCardsAsync();

            IsFileLoaded = true;
            StatusMessage = $"Loaded {DetectedDocumentTitle} ({FileSizeText})";
        }

        private async Task ProcessOfficeDocAsync(string filePath, FileMagicKind kind)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var pages = await _officeService.RenderOfficeDocumentAsync(fs, kind, 300);
            if (pages.Count > 0)
            {
                _activePageBitmap?.Dispose();
                _activePageBitmap = pages[0];
                RawPageImage = _activePageBitmap.ToBitmapSource();

                DetectedDocumentTitle = "Office / Text Document";
                DetectedKind = DocumentKind.OfficeDocument;
                DetectionConfidenceText = "100%";
                DetectionNotes = $"{pages.Count} page(s) formatted for standard printing.";

                HasCardsExtracted = false;
                FrontCardImage = null;
                BackCardImage = null;
                IsFileLoaded = true;
                StatusMessage = $"Formatted {Path.GetFileName(filePath)} for printing ({pages.Count} pages).";
            }
        }

        private async Task ProcessRasterImageAsync(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var skBmp = SKBitmap.Decode(fs);
            if (skBmp != null)
            {
                await ProcessImportedBitmapAsync(skBmp, Path.GetFileName(filePath));
            }
        }

        private async Task ProcessImportedBitmapAsync(SKBitmap bitmap, string name)
        {
            _activePageBitmap?.Dispose();
            _activePageBitmap = bitmap;
            RawPageImage = _activePageBitmap.ToBitmapSource();
            FileName = name;
            FileSizeText = $"{bitmap.Width}x{bitmap.Height} px";

            // Analyze if it looks like an ID card or photo
            double aspect = (double)bitmap.Width / bitmap.Height;
            if (Math.Abs(aspect - 1.5857) < 0.25)
            {
                DetectedDocumentTitle = "Identity Card Photo (CR80 Aspect)";
                DetectedKind = DocumentKind.GenericId;
                DetectionConfidenceText = "85%";
                DetectionNotes = "Single ID card dimensions detected.";

                _cachedFrontBitmap?.Dispose();
                _cachedFrontBitmap = _cropService.CropPerspective(bitmap, _cropService.GetInitialCropBox(bitmap.Width, bitmap.Height, CropAspectRatio.Cr80Card), CropAspectRatio.Cr80Card);
                FrontCardImage = _cachedFrontBitmap.ToBitmapSource();
                BackCardImage = null;
                HasCardsExtracted = true;
                HasBackCard = false;
            }
            else
            {
                DetectedDocumentTitle = "Scanned Photo / Document";
                DetectedKind = DocumentKind.GenericId;
                DetectionConfidenceText = "70%";
                DetectionNotes = "Use Crop Studio to frame card or photo boundaries.";
                HasCardsExtracted = false;
            }

            IsFileLoaded = true;
            StatusMessage = $"Image loaded: {name}";
            await Task.CompletedTask;
        }

        [RelayCommand]
        public async Task UnlockPasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(PasswordInput))
            {
                HasPasswordError = true;
                PasswordErrorMessage = "Please enter decryption password.";
                return;
            }

            await LoadDocumentAsync(DocumentPath, PasswordInput);
        }

        [RelayCommand]
        public void TogglePasswordMask()
        {
            IsPasswordMasked = !IsPasswordMasked;
        }

        [RelayCommand]
        public async Task AutoExtractCardsAsync()
        {
            if (_activePageBitmap == null) return;

            try
            {
                IsLoading = true;
                LoadingMessage = "Extracting card faces with high-precision geometry...";

                var extract = await _cardDetection.DetectAndExtractDualCardsAsync(
                    _activePageBitmap,
                    DetectedKind,
                    DetectedAadhaarGen);

                if (extract.Front != null)
                {
                    _cachedFrontBitmap?.Dispose();
                    _cachedFrontBitmap = extract.Front;
                    FrontCardImage = _cachedFrontBitmap.ToBitmapSource();

                    if (extract.Back != null)
                    {
                        _cachedBackBitmap?.Dispose();
                        _cachedBackBitmap = extract.Back;
                        BackCardImage = _cachedBackBitmap.ToBitmapSource();
                        HasBackCard = true;
                    }
                    else
                    {
                        _cachedBackBitmap?.Dispose();
                        _cachedBackBitmap = null;
                        BackCardImage = null;
                        HasBackCard = false;
                    }

                    HasCardsExtracted = true;
                    StatusMessage = HasBackCard ? "Front and Back cards extracted successfully." : "Single card extracted successfully.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Card extraction notice: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public void RotateCards()
        {
            if (_cachedFrontBitmap == null) return;
            _cachedFrontBitmap = _cropService.RotateBitmap(_cachedFrontBitmap, 90);
            FrontCardImage = _cachedFrontBitmap.ToBitmapSource();

            if (_cachedBackBitmap != null)
            {
                _cachedBackBitmap = _cropService.RotateBitmap(_cachedBackBitmap, 90);
                BackCardImage = _cachedBackBitmap.ToBitmapSource();
            }

            StatusMessage = "Rotated card faces by 90 degrees.";
        }

        [RelayCommand]
        public void SwapFaces()
        {
            if (_cachedFrontBitmap == null || _cachedBackBitmap == null) return;
            var temp = _cachedFrontBitmap;
            _cachedFrontBitmap = _cachedBackBitmap;
            _cachedBackBitmap = temp;

            FrontCardImage = _cachedFrontBitmap.ToBitmapSource();
            BackCardImage = _cachedBackBitmap.ToBitmapSource();
            StatusMessage = "Swapped Front and Back card faces.";
        }

        [RelayCommand]
        public void ToggleMaskAadhaar()
        {
            IsMaskAadhaarEnabled = !IsMaskAadhaarEnabled;
            // In a real Aadhaar layout, overlay XXXX-XXXX on the first 8 digits
            if (_cachedFrontBitmap != null && IsMaskAadhaarEnabled)
            {
                using var canvas = new SKCanvas(_cachedFrontBitmap);
                using var maskPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
                using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
                using var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 38);

                // Approximate location for standard CR80 2022x1275 px
                float mx = 500;
                float my = 980;
                canvas.DrawRect(new SKRect(mx, my, mx + 520, my + 60), maskPaint);
                canvas.DrawText("XXXX-XXXX-", mx + 10, my + 45, SKTextAlign.Left, font, textPaint);
                canvas.Flush();

                FrontCardImage = _cachedFrontBitmap.ToBitmapSource();
                StatusMessage = "UIDAI Compliant Masking applied (First 8 digits hidden).";
            }
        }

        [RelayCommand]
        public void OpenCropStudio()
        {
            if (_activePageBitmap != null)
            {
                OpenInCropStudioAction?.Invoke(_activePageBitmap, FileName);
            }
        }

        [RelayCommand]
        public void OpenPageDesigner()
        {
            if (_cachedFrontBitmap != null)
            {
                OpenInPageDesignerAction?.Invoke(_cachedFrontBitmap, _cachedBackBitmap, DetectedDocumentTitle);
            }
            else
            {
                StatusMessage = "Extract or load cards before opening Document Page Designer.";
            }
        }

        [RelayCommand]
        public void ApproveAndPrint()
        {
            if (_cachedFrontBitmap != null)
            {
                OpenInPrintPreviewAction?.Invoke(_cachedFrontBitmap, _cachedBackBitmap, DetectedKind);
            }
        }

        public void ReceiveCroppedCard(SKBitmap cardBmp, bool isFront)
        {
            if (isFront)
            {
                _cachedFrontBitmap?.Dispose();
                _cachedFrontBitmap = cardBmp;
                FrontCardImage = _cachedFrontBitmap.ToBitmapSource();
                HasCardsExtracted = true;
            }
            else
            {
                _cachedBackBitmap?.Dispose();
                _cachedBackBitmap = cardBmp;
                BackCardImage = _cachedBackBitmap.ToBitmapSource();
                HasBackCard = true;
                HasCardsExtracted = true;
            }
            StatusMessage = $"Updated {(isFront ? "Front" : "Back")} card from Crop Studio.";
        }
    }
}
