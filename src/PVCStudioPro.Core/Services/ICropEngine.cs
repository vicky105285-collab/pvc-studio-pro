using System;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public enum HandleKind
    {
        None,
        NorthWest,
        North,
        NorthEast,
        East,
        SouthEast,
        South,
        SouthWest,
        West,
        Center,
        CornerTopLeft,
        CornerTopRight,
        CornerBottomRight,
        CornerBottomLeft
    }

    public sealed class CropBounds
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public float RotationDegrees { get; set; }
        public int Dpi { get; set; } = 300;
        public bool IsAspectRatioLocked { get; set; } = true;
        public CropAspectRatio AspectRatio { get; set; } = CropAspectRatio.Cr80Card;

        public SKRect ToSKRect() => new((float)X, (float)Y, (float)(X + Width), (float)(Y + Height));

        public CropBounds Clone() => new()
        {
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            RotationDegrees = RotationDegrees,
            Dpi = Dpi,
            IsAspectRatioLocked = IsAspectRatioLocked,
            AspectRatio = AspectRatio
        };
    }

    public sealed class CropInteractionState
    {
        public double ZoomFactor { get; set; } = 1.0;
        public double PanOffsetX { get; set; } = 0.0;
        public double PanOffsetY { get; set; } = 0.0;
        public double ViewportWidth { get; set; } = 800.0;
        public double ViewportHeight { get; set; } = 600.0;
    }

    /// <summary>
    /// Centralized high-precision crop engine providing sub-pixel coordinate mapping,
    /// aspect-ratio preservation, perspective quadrilateral correction, and rotation math.
    /// </summary>
    public interface ICropEngine
    {
        DerivedImageAsset Crop(SourceImageAsset source, CropBounds bounds, string transformDescription);
        DerivedImageAsset CropPerspective(SourceImageAsset source, QuadrilateralPoints quad, CropAspectRatio ratio, int targetDpi, string transformDescription);

        (double ScreenX, double ScreenY) ImageToScreen(double imgX, double imgY, CropInteractionState state, double imgW, double imgH);
        (double ImageX, double ImageY) ScreenToImage(double screenX, double screenY, CropInteractionState state, double imgW, double imgH);

        CropBounds ConstrainToAspectRatio(CropBounds bounds, CropAspectRatio ratio, double maxW, double maxH);
        (double MmWidth, double MmHeight) PixelsToMillimeters(int pixelW, int pixelH, int dpi);
        (int PixelWidth, int PixelHeight) MillimetersToPixels(double mmW, double mmH, int dpi);
        double GetTargetAspectRatioValue(CropAspectRatio ratio);
    }
}
