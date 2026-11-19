using System;
using SkiaSharp;

namespace PVCStudioPro.Core.Models
{
    public enum PhotoSheetLayout
    {
        Sheet4x6_8Up,  // 4x6" (152.4 x 101.6 mm) 8 photos (4x2 grid)
        A4_16Up,       // A4 (210 x 297 mm) 16 photos (4x4 grid)
        A4_32Up        // A4 (210 x 297 mm) 32 photos (4x8 grid / stamps)
    }

    /// <summary>
    /// Models ICAO 9303 and ISO/IEC 19794-5 biometric face placement geometry and tolerance constraints.
    /// Standard Passport Photo: 35 mm x 45 mm (Width x Height).
    /// </summary>
    public class BiometricGuidelineModel
    {
        public double TargetWidthMm { get; set; } = 35.0;
        public double TargetHeightMm { get; set; } = 45.0;

        // ICAO 9303 constraints
        public double CrownMarginMinMm { get; set; } = 3.0;
        public double CrownMarginMaxMm { get; set; } = 5.0;
        public double ChinToBottomMinMm { get; set; } = 10.0;
        public double ChinToBottomMaxMm { get; set; } = 14.0;
        public double EyeLevelFromBottomMinMm { get; set; } = 28.0;
        public double EyeLevelFromBottomMaxMm { get; set; } = 31.0;
        public double FaceHeightMinRatio { get; set; } = 0.70; // 70% min face height (~31.5 mm)
        public double FaceHeightMaxRatio { get; set; } = 0.80; // 80% max face height (~36.0 mm)

        // Pixel-space coordinates calculated for current canvas DPI
        public double CrownLimitYPx { get; set; }
        public double EyeLevelYPx { get; set; }
        public double ChinLineYPx { get; set; }
        public double VerticalCenterXPx { get; set; }
        public double HeadOvalLeftPx { get; set; }
        public double HeadOvalTopPx { get; set; }
        public double HeadOvalWidthPx { get; set; }
        public double HeadOvalHeightPx { get; set; }
        public int Dpi { get; set; } = 600;
    }

    public class PassportPhotoOptions
    {
        public SKColor BackgroundColor { get; set; } = SKColors.White;
        public bool RemoveBackground { get; set; } = true;
        public double PanOffsetXRatio { get; set; } = 0.0;
        public double PanOffsetYRatio { get; set; } = 0.0;
        public double ZoomScale { get; set; } = 1.0;
        public int Dpi { get; set; } = 600;
    }
}
