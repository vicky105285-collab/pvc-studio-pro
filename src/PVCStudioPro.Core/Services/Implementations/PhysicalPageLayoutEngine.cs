using System;
using System.Collections.Generic;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class PhysicalPageLayoutEngine : IPhysicalPageLayoutEngine
    {
        private const double MmPerInch = 25.4;
        public const double Cr80WidthMm = 85.60;
        public const double Cr80HeightMm = 53.98;

        public PageLayoutModel CreateLayout(
            SheetLayoutMode mode,
            SKBitmap? frontCard,
            SKBitmap? backCard,
            bool showCutMarks = true,
            bool showFoldLine = false,
            bool mirrorMode = false,
            double offsetXMm = 0.0,
            double offsetYMm = 0.0,
            int targetDpi = 600)
        {
            var model = new PageLayoutModel
            {
                ShowCutMarks = showCutMarks,
                ShowFoldLine = showFoldLine,
                MirrorMode = mirrorMode,
                OffsetXMm = offsetXMm,
                OffsetYMm = offsetYMm,
                TargetDpi = targetDpi > 0 ? targetDpi : 600
            };

            switch (mode)
            {
                case SheetLayoutMode.SingleSideA4:
                    SetupSingleSideA4(model, frontCard, backCard);
                    break;

                case SheetLayoutMode.CenterFoldA4:
                    SetupCenterFoldA4(model, frontCard, backCard);
                    break;

                case SheetLayoutMode.DoubleSideDuplexA4:
                    SetupDoubleSideDuplexA4(model, frontCard, backCard);
                    break;

                case SheetLayoutMode.Epson2CardTray:
                    SetupEpson2CardTray(model, frontCard, backCard);
                    break;

                case SheetLayoutMode.CanonMultiTray:
                    SetupCanonMultiTray(model, frontCard, backCard);
                    break;

                case SheetLayoutMode.Photo4x6Sheet:
                    SetupPhoto4x6Sheet(model, frontCard, backCard);
                    break;

                case SheetLayoutMode.DirectCr80:
                    SetupDirectCr80(model, frontCard);
                    break;

                default:
                    SetupSingleSideA4(model, frontCard, backCard);
                    break;
            }

            return model;
        }

        private static void SetupSingleSideA4(PageLayoutModel model, SKBitmap? frontCard, SKBitmap? backCard)
        {
            model.Title = "A4 Upper Dual Cards";
            model.Preset = PaperPreset.A4;
            model.PageWidthMm = 210.0;
            model.PageHeightMm = 297.0;

            // Strict single-source millimeter geometry:
            // Front at X=15.0 mm, Back at X=109.4 mm. Symmetrical 15.0 mm outer margins.
            if (frontCard != null)
            {
                model.Items.Add(new PageLayoutItem
                {
                    Label = "Front Card Face",
                    Kind = LayoutItemKind.FrontCard,
                    XPositionMm = 15.0,
                    YPositionMm = 15.0,
                    WidthMm = Cr80WidthMm,
                    HeightMm = Cr80HeightMm,
                    Bitmap = frontCard
                });
            }

            if (backCard != null)
            {
                model.Items.Add(new PageLayoutItem
                {
                    Label = "Back Card Face",
                    Kind = LayoutItemKind.BackCard,
                    XPositionMm = 109.4,
                    YPositionMm = 15.0,
                    WidthMm = Cr80WidthMm,
                    HeightMm = Cr80HeightMm,
                    Bitmap = backCard
                });
            }
        }

        private static void SetupCenterFoldA4(PageLayoutModel model, SKBitmap? frontCard, SKBitmap? backCard)
        {
            model.Title = "A4 Center Fold (Crease Line)";
            model.Preset = PaperPreset.A4;
            model.PageWidthMm = 210.0;
            model.PageHeightMm = 297.0;
            model.ShowFoldLine = true;

            double centeredX = (210.0 - Cr80WidthMm) / 2.0; // 62.20 mm
            double topY = 15.0;
            double foldY = topY + Cr80HeightMm; // 68.98 mm

            if (frontCard != null)
            {
                model.Items.Add(new PageLayoutItem
                {
                    Label = "Front Card Face",
                    Kind = LayoutItemKind.FrontCard,
                    XPositionMm = centeredX,
                    YPositionMm = topY,
                    WidthMm = Cr80WidthMm,
                    HeightMm = Cr80HeightMm,
                    Bitmap = frontCard
                });
            }

            if (backCard != null)
            {
                model.Items.Add(new PageLayoutItem
                {
                    Label = "Back Card Face (Fold Mirror)",
                    Kind = LayoutItemKind.BackCard,
                    XPositionMm = centeredX,
                    YPositionMm = foldY,
                    WidthMm = Cr80WidthMm,
                    HeightMm = Cr80HeightMm,
                    Bitmap = backCard,
                    RotationDegrees = 180.0 // Rotated 180° so both faces read right-side-up when folded
                });
            }
        }

        private static void SetupDoubleSideDuplexA4(PageLayoutModel model, SKBitmap? frontCard, SKBitmap? backCard)
        {
            model.Title = "A4 Duplex (Single Sheet Centered)";
            model.Preset = PaperPreset.A4;
            model.PageWidthMm = 210.0;
            model.PageHeightMm = 297.0;

            double centeredX = (210.0 - Cr80WidthMm) / 2.0;
            double centeredY = (297.0 - Cr80HeightMm) / 2.0;

            var activeFace = frontCard ?? backCard;
            if (activeFace != null)
            {
                model.Items.Add(new PageLayoutItem
                {
                    Label = "Duplex Card Face",
                    Kind = LayoutItemKind.FrontCard,
                    XPositionMm = centeredX,
                    YPositionMm = centeredY,
                    WidthMm = Cr80WidthMm,
                    HeightMm = Cr80HeightMm,
                    Bitmap = activeFace
                });
            }
        }

        private static void SetupEpson2CardTray(PageLayoutModel model, SKBitmap? frontCard, SKBitmap? backCard)
        {
            model.Title = "Epson Direct Card Tray (2-Up)";
            model.Preset = PaperPreset.EpsonTray;
            model.PageWidthMm = 120.0;
            model.PageHeightMm = 180.0;

            if (frontCard != null)
            {
                model.Items.Add(new PageLayoutItem
                {
                    Label = "Epson Tray Slot 1 (Front)",
                    Kind = LayoutItemKind.FrontCard,
                    XPositionMm = 17.20,
                    YPositionMm = 20.00,
                    WidthMm = Cr80WidthMm,
                    HeightMm = Cr80HeightMm,
                    Bitmap = frontCard
                });
            }

            if (backCard != null)
            {
                model.Items.Add(new PageLayoutItem
                {
                    Label = "Epson Tray Slot 2 (Back)",
                    Kind = LayoutItemKind.BackCard,
                    XPositionMm = 17.20,
                    YPositionMm = 81.00,
                    WidthMm = Cr80WidthMm,
                    HeightMm = Cr80HeightMm,
                    Bitmap = backCard
                });
            }
        }

        private static void SetupCanonMultiTray(PageLayoutModel model, SKBitmap? frontCard, SKBitmap? backCard)
        {
            model.Title = "Canon Multi Card Tray (2-Up)";
            model.Preset = PaperPreset.CanonTray;
            model.PageWidthMm = 130.0;
            model.PageHeightMm = 254.0;

            if (frontCard != null)
            {
                model.Items.Add(new PageLayoutItem
                {
                    Label = "Canon Tray Slot 1 (Front)",
                    Kind = LayoutItemKind.FrontCard,
                    XPositionMm = 22.20,
                    YPositionMm = 25.00,
                    WidthMm = Cr80WidthMm,
                    HeightMm = Cr80HeightMm,
                    Bitmap = frontCard
                });
            }

            if (backCard != null)
            {
                model.Items.Add(new PageLayoutItem
                {
                    Label = "Canon Tray Slot 2 (Back)",
                    Kind = LayoutItemKind.BackCard,
                    XPositionMm = 22.20,
                    YPositionMm = 95.00,
                    WidthMm = Cr80WidthMm,
                    HeightMm = Cr80HeightMm,
                    Bitmap = backCard
                });
            }
        }

        private static void SetupPhoto4x6Sheet(PageLayoutModel model, SKBitmap? frontCard, SKBitmap? backCard)
        {
            model.Title = "4x6 Photo Paper Sheet (2-Up)";
            model.Preset = PaperPreset.Photo4x6;
            model.PageWidthMm = 101.6;
            model.PageHeightMm = 152.4;

            if (frontCard != null)
            {
                model.Items.Add(new PageLayoutItem
                {
                    Label = "Front Card Face",
                    Kind = LayoutItemKind.FrontCard,
                    XPositionMm = 8.00,
                    YPositionMm = 10.00,
                    WidthMm = Cr80WidthMm,
                    HeightMm = Cr80HeightMm,
                    Bitmap = frontCard
                });
            }

            if (backCard != null)
            {
                model.Items.Add(new PageLayoutItem
                {
                    Label = "Back Card Face",
                    Kind = LayoutItemKind.BackCard,
                    XPositionMm = 8.00,
                    YPositionMm = 68.00,
                    WidthMm = Cr80WidthMm,
                    HeightMm = Cr80HeightMm,
                    Bitmap = backCard
                });
            }
        }

        private static void SetupDirectCr80(PageLayoutModel model, SKBitmap? frontCard)
        {
            model.Title = "Single CR80 Direct Card";
            model.Preset = PaperPreset.DirectCr80;
            model.PageWidthMm = Cr80WidthMm;
            model.PageHeightMm = Cr80HeightMm;
            model.ShowCutMarks = false;

            if (frontCard != null)
            {
                model.Items.Add(new PageLayoutItem
                {
                    Label = "Direct CR80 Card",
                    Kind = LayoutItemKind.FrontCard,
                    XPositionMm = 0.0,
                    YPositionMm = 0.0,
                    WidthMm = Cr80WidthMm,
                    HeightMm = Cr80HeightMm,
                    Bitmap = frontCard
                });
            }
        }

        public void UpdateLayoutOffsets(PageLayoutModel model, double offsetXMm, double offsetYMm)
        {
            if (model == null) return;
            model.OffsetXMm = offsetXMm;
            model.OffsetYMm = offsetYMm;
        }

        public (float X, float Y) MillimetersToPixels(double xMm, double yMm, int dpi)
        {
            float x = (float)(xMm * dpi / MmPerInch);
            float y = (float)(yMm * dpi / MmPerInch);
            return (x, y);
        }

        public (double X, double Y) PixelsToMillimeters(float xPx, float yPx, int dpi)
        {
            if (dpi <= 0) dpi = 300;
            double x = (double)xPx * MmPerInch / dpi;
            double y = (double)yPx * MmPerInch / dpi;
            return (x, y);
        }
    }
}
