using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using PVCStudioPro.App.Converters;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;

namespace PVCStudioPro.App.ViewModels
{
    public partial class CropStudioViewModel : ObservableObject
    {
        private readonly IManualCropService _cropService;
        private readonly ICardDetectionService _cardDetection;

        public Action<SKBitmap, bool>? ReturnToDeskAction { get; set; }
        public Action? CloseAction { get; set; }

        [ObservableProperty]
        private string _sourceTitle = "No Image Loaded";

        [ObservableProperty]
        private BitmapSource? _sourceImage;

        [ObservableProperty]
        private BitmapSource? _previewCroppedImage;

        [ObservableProperty]
        private CropAspectRatio _selectedAspectRatio = CropAspectRatio.Cr80Card;

        // Viewport Transformation
        [ObservableProperty]
        private double _zoomFactor = 1.0;

        [ObservableProperty]
        private double _panOffsetX = 0.0;

        [ObservableProperty]
        private double _panOffsetY = 0.0;

        [ObservableProperty]
        private double _viewportWidth = 800;

        [ObservableProperty]
        private double _viewportHeight = 600;

        // Crop Box in native image pixel coordinates
        [ObservableProperty]
        private float _cropX;

        [ObservableProperty]
        private float _cropY;

        [ObservableProperty]
        private float _cropWidth;

        [ObservableProperty]
        private float _cropHeight;

        // Corner Points (For perspective quad warp mode)
        [ObservableProperty]
        private float _corner1X;

        [ObservableProperty]
        private float _corner1Y;

        [ObservableProperty]
        private float _corner2X;

        [ObservableProperty]
        private float _corner2Y;

        [ObservableProperty]
        private float _corner3X;

        [ObservableProperty]
        private float _corner3Y;

        [ObservableProperty]
        private float _corner4X;

        [ObservableProperty]
        private float _corner4Y;

        [ObservableProperty]
        private bool _isPerspectiveMode = false;

        // Image Adjustments
        [ObservableProperty]
        private float _brightness = 0f; // -100 to +100

        [ObservableProperty]
        private float _contrast = 1.0f; // 0.5 to 2.0

        [ObservableProperty]
        private float _saturation = 1.0f; // 0.0 to 2.0

        [ObservableProperty]
        private float _sharpness = 0.0f; // 0.0 to 2.0

        [ObservableProperty]
        private int _imageWidth;

        [ObservableProperty]
        private int _imageHeight;

        [ObservableProperty]
        private string _pixelDimensionsText = "2022 × 1275 px";

        [ObservableProperty]
        private string _physicalDimensionsText = "85.60 × 53.98 mm @ 600 DPI";

        [ObservableProperty]
        private string _aspectRatioText = "CR80 (1.586)";

        [ObservableProperty]
        private string _statusMessage = "Crop Studio Ready | Drag handles or adjust corners to frame";

        [ObservableProperty]
        private bool _hasCroppedResult = false;

        private SKBitmap? _originalBitmap;
        private SKBitmap? _activeBitmap;
        private SKBitmap? _croppedBitmap;

        public CropStudioViewModel(IManualCropService cropService, ICardDetectionService cardDetection)
        {
            _cropService = cropService;
            _cardDetection = cardDetection;
        }

        public void LoadImage(SKBitmap bitmap, string title = "Document Image")
        {
            _originalBitmap?.Dispose();
            _activeBitmap?.Dispose();
            _croppedBitmap?.Dispose();

            _originalBitmap = bitmap.Copy();
            _activeBitmap = bitmap.Copy();
            SourceTitle = title;
            ImageWidth = _activeBitmap.Width;
            ImageHeight = _activeBitmap.Height;
            SourceImage = _activeBitmap.ToBitmapSource();
            HasCroppedResult = false;
            PreviewCroppedImage = null;

            ResetAdjustments();
            FitToWindow();
            ResetCropCorners();
            PerformCrop();

            StatusMessage = $"Loaded image for precision cropping ({ImageWidth}x{ImageHeight} px).";
        }

        [RelayCommand]
        public void FitToWindow()
        {
            if (ImageWidth <= 0 || ImageHeight <= 0) return;

            double targetVw = ViewportWidth > 100 ? ViewportWidth : 800;
            double targetVh = ViewportHeight > 100 ? ViewportHeight : 600;

            double scaleX = (targetVw - 32) / ImageWidth;
            double scaleY = (targetVh - 32) / ImageHeight;
            double bestScale = Math.Min(scaleX, scaleY);

            ZoomFactor = Math.Clamp(bestScale, 0.05, 5.0);
            PanOffsetX = Math.Max(0, (targetVw - (ImageWidth * ZoomFactor)) / 2.0);
            PanOffsetY = Math.Max(0, (targetVh - (ImageHeight * ZoomFactor)) / 2.0);
        }

        [RelayCommand]
        public void ActualSize()
        {
            ZoomFactor = 1.0;
            PanOffsetX = 0;
            PanOffsetY = 0;
        }

        [RelayCommand]
        public void ZoomIn()
        {
            ZoomFactor = Math.Min(5.0, ZoomFactor * 1.25);
        }

        [RelayCommand]
        public void ZoomOut()
        {
            ZoomFactor = Math.Max(0.05, ZoomFactor / 1.25);
        }

        [RelayCommand]
        public void ResetCropCorners()
        {
            if (_activeBitmap == null) return;

            var initBox = _cropService.GetInitialCropBox(_activeBitmap.Width, _activeBitmap.Height, SelectedAspectRatio);
            Corner1X = initBox.TopLeft.X;
            Corner1Y = initBox.TopLeft.Y;
            Corner2X = initBox.TopRight.X;
            Corner2Y = initBox.TopRight.Y;
            Corner3X = initBox.BottomRight.X;
            Corner3Y = initBox.BottomRight.Y;
            Corner4X = initBox.BottomLeft.X;
            Corner4Y = initBox.BottomLeft.Y;

            CropX = Corner1X;
            CropY = Corner1Y;
            CropWidth = Math.Max(10, Corner2X - Corner1X);
            CropHeight = Math.Max(10, Corner4Y - Corner1Y);

            UpdateDimensionTexts();
            PerformCrop();
        }

        [RelayCommand]
        public void SetAspectRatio(string ratioName)
        {
            if (Enum.TryParse<CropAspectRatio>(ratioName, out var ratio))
            {
                SelectedAspectRatio = ratio;
                switch (ratio)
                {
                    case CropAspectRatio.Cr80Card:
                        AspectRatioText = "CR80 (85.60 × 53.98 mm)";
                        break;
                    case CropAspectRatio.PassportPhoto:
                        AspectRatioText = "Passport (35 × 45 mm)";
                        break;
                    case CropAspectRatio.StampPhoto:
                        AspectRatioText = "Stamp (25 × 30 mm)";
                        break;
                    case CropAspectRatio.Freeform:
                        AspectRatioText = "Freeform";
                        break;
                }

                ResetCropCorners();
                StatusMessage = $"Aspect ratio preset applied: {ratioName}.";
            }
        }

        [RelayCommand]
        public void TogglePerspectiveMode()
        {
            IsPerspectiveMode = !IsPerspectiveMode;
            StatusMessage = IsPerspectiveMode
                ? "Perspective Quad Mode: Drag 4 corners to straighten angled photo."
                : "Standard Rectangular Mode: Drag 8 handles to frame card.";
            PerformCrop();
        }

        [RelayCommand]
        public void ResetAdjustments()
        {
            Brightness = 0f;
            Contrast = 1.0f;
            Saturation = 1.0f;
            Sharpness = 0f;
            PerformCrop();
        }

        public void UpdateCropRect(float x, float y, float width, float height)
        {
            if (_activeBitmap == null) return;

            CropX = Math.Clamp(x, 0, _activeBitmap.Width - 10);
            CropY = Math.Clamp(y, 0, _activeBitmap.Height - 10);
            CropWidth = Math.Clamp(width, 10, _activeBitmap.Width - CropX);
            CropHeight = Math.Clamp(height, 10, _activeBitmap.Height - CropY);

            Corner1X = CropX;
            Corner1Y = CropY;
            Corner2X = CropX + CropWidth;
            Corner2Y = CropY;
            Corner3X = CropX + CropWidth;
            Corner3Y = CropY + CropHeight;
            Corner4X = CropX;
            Corner4Y = CropY + CropHeight;

            UpdateDimensionTexts();
            PerformCrop();
        }

        public void UpdateCorner(int cornerIndex, float x, float y)
        {
            if (_activeBitmap == null) return;

            x = Math.Clamp(x, 0, _activeBitmap.Width);
            y = Math.Clamp(y, 0, _activeBitmap.Height);

            switch (cornerIndex)
            {
                case 1:
                    Corner1X = x; Corner1Y = y;
                    break;
                case 2:
                    Corner2X = x; Corner2Y = y;
                    break;
                case 3:
                    Corner3X = x; Corner3Y = y;
                    break;
                case 4:
                    Corner4X = x; Corner4Y = y;
                    break;
            }

            if (!IsPerspectiveMode)
            {
                CropX = Math.Min(Corner1X, Corner4X);
                CropY = Math.Min(Corner1Y, Corner2Y);
                CropWidth = Math.Max(10, Math.Max(Corner2X, Corner3X) - CropX);
                CropHeight = Math.Max(10, Math.Max(Corner3Y, Corner4Y) - CropY);
            }

            UpdateDimensionTexts();
            PerformCrop();
        }

        private void UpdateDimensionTexts()
        {
            float w = IsPerspectiveMode ? MathF.Max(Corner2X - Corner1X, Corner3X - Corner4X) : CropWidth;
            float h = IsPerspectiveMode ? MathF.Max(Corner4Y - Corner1Y, Corner3Y - Corner2Y) : CropHeight;

            PixelDimensionsText = $"{Math.Round(w)} × {Math.Round(h)} px";
            double mmW = (w * 25.4) / 600.0;
            double mmH = (h * 25.4) / 600.0;
            PhysicalDimensionsText = $"{mmW:F2} × {mmH:F2} mm (at 600 DPI)";
        }

        [RelayCommand]
        public void PerformCrop()
        {
            if (_activeBitmap == null) return;

            try
            {
                QuadrilateralPoints quad;
                if (IsPerspectiveMode)
                {
                    quad = new QuadrilateralPoints(
                        new SKPoint(Corner1X, Corner1Y),
                        new SKPoint(Corner2X, Corner2Y),
                        new SKPoint(Corner3X, Corner3Y),
                        new SKPoint(Corner4X, Corner4Y)
                    );
                }
                else
                {
                    quad = new QuadrilateralPoints(
                        new SKPoint(CropX, CropY),
                        new SKPoint(CropX + CropWidth, CropY),
                        new SKPoint(CropX + CropWidth, CropY + CropHeight),
                        new SKPoint(CropX, CropY + CropHeight)
                    );
                }

                _croppedBitmap?.Dispose();
                var cropped = _cropService.CropPerspective(_activeBitmap, quad, SelectedAspectRatio, 600);

                // Apply Image Adjustments
                var adjusted = _cropService.ApplyAdjustments(cropped, Brightness, Contrast, Saturation, Sharpness);
                cropped.Dispose();
                _croppedBitmap = adjusted;

                PreviewCroppedImage = _croppedBitmap.ToBitmapSource();
                HasCroppedResult = true;
                StatusMessage = $"Cropped to {_croppedBitmap.Width}x{_croppedBitmap.Height} px at 600 DPI.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Crop error: {ex.Message}";
            }
        }

        [RelayCommand]
        public void Rotate90()
        {
            if (_activeBitmap == null) return;
            var rotated = _cropService.RotateBitmap(_activeBitmap, 90);
            _activeBitmap.Dispose();
            _activeBitmap = rotated;
            ImageWidth = _activeBitmap.Width;
            ImageHeight = _activeBitmap.Height;
            SourceImage = _activeBitmap.ToBitmapSource();
            ResetCropCorners();
            FitToWindow();
            StatusMessage = "Rotated source image 90 degrees.";
        }

        [RelayCommand]
        public void FlipHorizontal()
        {
            if (_activeBitmap == null) return;
            var flipped = _cropService.FlipHorizontal(_activeBitmap);
            _activeBitmap.Dispose();
            _activeBitmap = flipped;
            SourceImage = _activeBitmap.ToBitmapSource();
            ResetCropCorners();
            StatusMessage = "Flipped source image horizontally.";
        }

        [RelayCommand]
        public void AssignToFrontFace()
        {
            if (_croppedBitmap != null)
            {
                ReturnToDeskAction?.Invoke(_croppedBitmap.Copy(), true);
                CloseAction?.Invoke();
            }
        }

        [RelayCommand]
        public void AssignToBackFace()
        {
            if (_croppedBitmap != null)
            {
                ReturnToDeskAction?.Invoke(_croppedBitmap.Copy(), false);
                CloseAction?.Invoke();
            }
        }

        [RelayCommand]
        public void Cancel()
        {
            CloseAction?.Invoke();
        }

        partial void OnBrightnessChanged(float value) => PerformCrop();
        partial void OnContrastChanged(float value) => PerformCrop();
        partial void OnSaturationChanged(float value) => PerformCrop();
        partial void OnSharpnessChanged(float value) => PerformCrop();
    }
}
