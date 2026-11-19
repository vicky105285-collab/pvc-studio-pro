using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SkiaSharp;
using PVCStudioPro.Core.Services;

namespace PVCStudioPro.App.ViewModels
{
    public partial class DocumentProcessingViewModel : ObservableObject
    {
        private readonly IVisionService _visionService;
        private SKBitmap? _originalBitmap;
        private SKBitmap? _rectifiedBitmap;

        [ObservableProperty]
        private ImageSource? _sourceImage;

        [ObservableProperty]
        private ImageSource? _rectifiedImage;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusMessage = "Load a photographed or scanned physical card to auto-deskew.";

        public ICommand OpenScanCommand { get; }
        public ICommand AutoDeskewCommand { get; }

        public DocumentProcessingViewModel(IVisionService visionService)
        {
            _visionService = visionService ?? throw new ArgumentNullException(nameof(visionService));

            OpenScanCommand = new RelayCommand(OpenScannedDocument);
            AutoDeskewCommand = new AsyncRelayCommand(AutoDeskewAndCropAsync);
        }

        private void OpenScannedDocument()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All Files (*.*)|*.*",
                Title = "Open Photographed or Scanned Physical Card"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    _originalBitmap?.Dispose();
                    _originalBitmap = SKBitmap.Decode(dlg.FileName);
                    if (_originalBitmap != null)
                    {
                        SourceImage = SkBitmapToImageSource(_originalBitmap);
                        StatusMessage = $"Loaded image ({_originalBitmap.Width}x{_originalBitmap.Height} px). Click 'Auto-Detect Corners & Rectify'.";
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to load image: {ex.Message}";
                }
            }
        }

        private async Task AutoDeskewAndCropAsync()
        {
            if (_originalBitmap == null)
            {
                StatusMessage = "Please load a document first.";
                return;
            }

            IsProcessing = true;
            StatusMessage = "Analyzing card contour and computing perspective warp...";

            try
            {
                _rectifiedBitmap?.Dispose();
                _rectifiedBitmap = await _visionService.DeskewAndCropCardAsync(_originalBitmap);
                RectifiedImage = SkBitmapToImageSource(_rectifiedBitmap);
                StatusMessage = "Rectified to exact CR80 physical proportions (2022 x 1275 px at 600 DPI). Ready for printing.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Rectification error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private static BitmapSource SkBitmapToImageSource(SKBitmap skBitmap)
        {
            using var image = SKImage.FromBitmap(skBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream();
            data.SaveTo(stream);
            stream.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
