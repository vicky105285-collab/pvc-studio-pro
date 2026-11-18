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
        private int _imageWidth;

        [ObservableProperty]
        private int _imageHeight;

        [ObservableProperty]
        private string _statusMessage = "Crop Studio Ready | Adjust corners to straighten and crop";

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

            ResetCropCorners();
            PerformCrop();
            StatusMessage = $"Loaded image for precision cropping ({ImageWidth}x{ImageHeight} px).";
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

            PerformCrop();
        }

        [RelayCommand]
        public void SetAspectRatio(string ratioName)
        {
            if (Enum.TryParse<CropAspectRatio>(ratioName, out var ratio))
            {
                SelectedAspectRatio = ratio;
                ResetCropCorners();
                StatusMessage = $"Set aspect ratio to {ratioName}.";
            }
        }

        [RelayCommand]
        public void PerformCrop()
        {
            if (_activeBitmap == null) return;

            try
            {
                var quad = new QuadrilateralPoints(
                    new SKPoint(Corner1X, Corner1Y),
                    new SKPoint(Corner2X, Corner2Y),
                    new SKPoint(Corner3X, Corner3Y),
                    new SKPoint(Corner4X, Corner4Y)
                );

                _croppedBitmap?.Dispose();
                _croppedBitmap = _cropService.CropPerspective(_activeBitmap, quad, SelectedAspectRatio, 600);
                PreviewCroppedImage = _croppedBitmap.ToBitmapSource();
                HasCroppedResult = true;
                StatusMessage = $"Cropped to {_croppedBitmap.Width}x{_croppedBitmap.Height} px at 600 DPI.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Crop calculation error: {ex.Message}";
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
    }
}
