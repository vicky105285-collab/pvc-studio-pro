using System;
using System.Collections.Generic;
using SkiaSharp;

namespace PVCStudioPro.Core.Models
{
    public enum FileMagicKind
    {
        Unknown,
        Pdf,
        Docx,
        Doc,
        Rtf,
        Text,
        Jpeg,
        Png,
        Bmp,
        Tiff,
        Webp
    }

    public enum DocumentKind
    {
        Unknown,
        Aadhaar,
        Pan,
        VoterId,
        DrivingLicence,
        VehicleRc,
        AyushmanBharat,
        Abha,
        EShram,
        TamilNaduRation,
        GenericId,
        OfficeDocument
    }

    public enum AadhaarGeneration
    {
        Unknown,
        Pre2018,
        Masked2018To2022,
        Modern2022Plus,
        PvcOrderLetter
    }

    public enum PdfPasswordStatus
    {
        Unencrypted,
        PasswordRequired,
        DecryptionSuccessful,
        InvalidPassword,
        UnsupportedEncryption,
        Corrupted
    }

    public class DocumentSniffResult
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => System.IO.Path.GetFileName(FilePath);
        public FileMagicKind MagicKind { get; set; } = FileMagicKind.Unknown;
        public string MimeType { get; set; } = "application/octet-stream";
        public string MagicHex { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public bool IsCorrupt { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class PasswordUnlockResult
    {
        public PdfPasswordStatus Status { get; set; }
        public string? EffectivePassword { get; set; }
        public string? ErrorMessage { get; set; }

        public static PasswordUnlockResult Success(string? password) => new()
        {
            Status = PdfPasswordStatus.DecryptionSuccessful,
            EffectivePassword = password
        };

        public static PasswordUnlockResult Unencrypted() => new()
        {
            Status = PdfPasswordStatus.Unencrypted
        };

        public static PasswordUnlockResult Required(string message = "Document is password encrypted.") => new()
        {
            Status = PdfPasswordStatus.PasswordRequired,
            ErrorMessage = message
        };

        public static PasswordUnlockResult Invalid(string message = "Incorrect password.") => new()
        {
            Status = PdfPasswordStatus.InvalidPassword,
            ErrorMessage = message
        };

        public static PasswordUnlockResult Corrupt(string message = "Document header or encryption structure is corrupted.") => new()
        {
            Status = PdfPasswordStatus.Corrupted,
            ErrorMessage = message
        };
    }

    public class QuadrilateralPoints
    {
        public SKPoint TopLeft { get; set; }
        public SKPoint TopRight { get; set; }
        public SKPoint BottomRight { get; set; }
        public SKPoint BottomLeft { get; set; }

        public QuadrilateralPoints() { }

        public QuadrilateralPoints(SKPoint tl, SKPoint tr, SKPoint br, SKPoint bl)
        {
            TopLeft = tl;
            TopRight = tr;
            BottomRight = br;
            BottomLeft = bl;
        }

        public static QuadrilateralPoints FromRect(SKRect rect) => new(
            new SKPoint(rect.Left, rect.Top),
            new SKPoint(rect.Right, rect.Top),
            new SKPoint(rect.Right, rect.Bottom),
            new SKPoint(rect.Left, rect.Bottom)
        );
    }

    public enum CropAspectRatio
    {
        Cr80Card,       // 85.60 x 53.98 mm = 1.5857
        PassportPhoto,  // 35 x 45 mm = 0.7778
        StampPhoto,     // 25 x 30 mm = 0.8333
        Freeform
    }

    public enum TargetMediaKind
    {
        Epson2CardTray,
        CanonMultiTray,
        SingleCardCR80,
        SheetDragonA4,
        StandardPaperA4,
        StandardPaperA5,
        StandardPaperLetter
    }

    public class PrintPreviewConfig
    {
        public TargetMediaKind MediaKind { get; set; } = TargetMediaKind.Epson2CardTray;
        public double OffsetXMm { get; set; } = 0.0;
        public double OffsetYMm { get; set; } = 0.0;
        public bool Duplex { get; set; } = false;
        public int Copies { get; set; } = 1;
        public bool MirrorMode { get; set; } = false;
        public bool MaskAadhaar { get; set; } = false;
    }

    public class CardPlacementVisual
    {
        public string Label { get; set; } = string.Empty;
        public double XPositionMm { get; set; }
        public double YPositionMm { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public bool IsFrontFace { get; set; }
    }

    public class PrintPreviewRender
    {
        public SKBitmap PreviewBitmap { get; set; }
        public double CanvasWidthMm { get; set; }
        public double CanvasHeightMm { get; set; }
        public List<CardPlacementVisual> Placements { get; set; } = new();

        public PrintPreviewRender(SKBitmap bmp, double wMm, double hMm)
        {
            PreviewBitmap = bmp;
            CanvasWidthMm = wMm;
            CanvasHeightMm = hMm;
        }
    }
}
