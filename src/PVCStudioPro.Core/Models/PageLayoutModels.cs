using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SkiaSharp;

namespace PVCStudioPro.Core.Models
{
    public enum LayoutItemKind
    {
        FrontCard,
        BackCard,
        PassportPhoto,
        Image,
        Text
    }

    public enum PaperPreset
    {
        A4,
        A5,
        Photo4x6,
        EpsonTray,
        CanonTray,
        DirectCr80,
        Custom
    }

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

    public class PageLayoutItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public LayoutItemKind Kind { get; set; } = LayoutItemKind.FrontCard;
        public string Label { get; set; } = "Item";
        
        public double XPositionMm { get; set; }
        public double YPositionMm { get; set; }
        public double WidthMm { get; set; } = 85.60;
        public double HeightMm { get; set; } = 53.98;
        public double RotationDegrees { get; set; } = 0.0;
        
        public bool LockAspectRatio { get; set; } = true;
        public bool IsLocked { get; set; } = false;
        public int ZIndex { get; set; } = 0;

        // Image representation
        [JsonIgnore]
        public SKBitmap? Bitmap { get; set; }

        public string? Base64ImageData { get; set; }
        public string? TextContent { get; set; }
        public string? TextFontFamily { get; set; } = "Segoe UI";
        public float TextFontSizePt { get; set; } = 12f;
        public string TextColorHex { get; set; } = "#000000";

        public PageLayoutItem Clone()
        {
            return new PageLayoutItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = Kind,
                Label = Label,
                XPositionMm = XPositionMm,
                YPositionMm = YPositionMm,
                WidthMm = WidthMm,
                HeightMm = HeightMm,
                RotationDegrees = RotationDegrees,
                LockAspectRatio = LockAspectRatio,
                IsLocked = IsLocked,
                ZIndex = ZIndex,
                Bitmap = Bitmap != null ? SKBitmap.FromImage(SKImage.FromBitmap(Bitmap)) : null,
                Base64ImageData = Base64ImageData,
                TextContent = TextContent,
                TextFontFamily = TextFontFamily,
                TextFontSizePt = TextFontSizePt,
                TextColorHex = TextColorHex
            };
        }
    }

    public class PageLayoutModel
    {
        public string Title { get; set; } = "Untitled Layout";
        public PaperPreset Preset { get; set; } = PaperPreset.A4;
        
        // Physical page dimensions in millimeters
        public double PageWidthMm { get; set; } = 210.0;
        public double PageHeightMm { get; set; } = 297.0;

        // Margins in millimeters
        public double MarginLeftMm { get; set; } = 5.0;
        public double MarginTopMm { get; set; } = 5.0;
        public double MarginRightMm { get; set; } = 5.0;
        public double MarginBottomMm { get; set; } = 5.0;

        // Calibration offsets
        public double OffsetXMm { get; set; } = 0.0;
        public double OffsetYMm { get; set; } = 0.0;

        // Rendering options
        public int TargetDpi { get; set; } = 600;
        public bool MirrorMode { get; set; } = false;
        public bool ShowCutMarks { get; set; } = true;
        public bool ShowFoldLine { get; set; } = false;

        public List<PageLayoutItem> Items { get; set; } = new();

        public static PageLayoutModel CreateDefaultA4()
        {
            return new PageLayoutModel
            {
                Title = "A4 Standard PVC Layout",
                Preset = PaperPreset.A4,
                PageWidthMm = 210.0,
                PageHeightMm = 297.0,
                MarginLeftMm = 5.0,
                MarginTopMm = 5.0,
                MarginRightMm = 5.0,
                MarginBottomMm = 5.0,
                TargetDpi = 600
            };
        }

        public static PageLayoutModel CreateDefault4x6()
        {
            return new PageLayoutModel
            {
                Title = "4x6 Photo PVC Layout",
                Preset = PaperPreset.Photo4x6,
                PageWidthMm = 101.6,
                PageHeightMm = 152.4,
                MarginLeftMm = 3.0,
                MarginTopMm = 3.0,
                MarginRightMm = 3.0,
                MarginBottomMm = 3.0,
                TargetDpi = 600
            };
        }

        public static PageLayoutModel CreateEpsonTray()
        {
            return new PageLayoutModel
            {
                Title = "Epson Direct Tray (2-Up)",
                Preset = PaperPreset.EpsonTray,
                PageWidthMm = 120.0,
                PageHeightMm = 180.0,
                MarginLeftMm = 0.0,
                MarginTopMm = 0.0,
                MarginRightMm = 0.0,
                MarginBottomMm = 0.0,
                TargetDpi = 600
            };
        }

        public static PageLayoutModel CreateDirectCr80()
        {
            return new PageLayoutModel
            {
                Title = "Single CR80 PVC Card",
                Preset = PaperPreset.DirectCr80,
                PageWidthMm = 85.60,
                PageHeightMm = 53.98,
                MarginLeftMm = 0.0,
                MarginTopMm = 0.0,
                MarginRightMm = 0.0,
                MarginBottomMm = 0.0,
                TargetDpi = 600
            };
        }
    }
}
