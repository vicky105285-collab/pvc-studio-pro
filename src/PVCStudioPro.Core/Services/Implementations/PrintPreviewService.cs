using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class PrintPreviewService : IPrintPreviewService
    {
        private const double MmPerInch = 25.4;
        private const int PreviewDpi = 150; // crisp interactive preview

        public Task<PrintPreviewRender> GeneratePreviewAsync(
            SKBitmap? frontCard,
            SKBitmap? backCard,
            PrintPreviewConfig config,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                double canvasWMm;
                double canvasHMm;
                var placements = new List<CardPlacementVisual>();

                switch (config.MediaKind)
                {
                    case TargetMediaKind.SingleCardCR80:
                        canvasWMm = 85.60;
                        canvasHMm = 53.98;
                        placements.Add(new CardPlacementVisual
                        {
                            Label = "Card 1 (Single Direct CR80)",
                            XPositionMm = 0.0 + config.OffsetXMm,
                            YPositionMm = 0.0 + config.OffsetYMm,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            IsFrontFace = true
                        });
                        break;

                    case TargetMediaKind.CanonMultiTray:
                        canvasWMm = 130.0;
                        canvasHMm = 220.0;
                        placements.Add(new CardPlacementVisual
                        {
                            Label = "Slot 1 (Front)",
                            XPositionMm = 22.2 + config.OffsetXMm,
                            YPositionMm = 25.0 + config.OffsetYMm,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            IsFrontFace = true
                        });
                        placements.Add(new CardPlacementVisual
                        {
                            Label = "Slot 2 (Back)",
                            XPositionMm = 22.2 + config.OffsetXMm,
                            YPositionMm = 115.0 + config.OffsetYMm,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            IsFrontFace = false
                        });
                        break;

                    case TargetMediaKind.SheetDragonA4:
                        canvasWMm = 210.0;
                        canvasHMm = 297.0;
                        // Dragon Sheet / A4 2-card layout centered
                        placements.Add(new CardPlacementVisual
                        {
                            Label = "Front Card (A4 Dragon Sheet)",
                            XPositionMm = 16.5 + config.OffsetXMm,
                            YPositionMm = 20.0 + config.OffsetYMm,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            IsFrontFace = true
                        });
                        placements.Add(new CardPlacementVisual
                        {
                            Label = "Back Card (A4 Dragon Sheet)",
                            XPositionMm = 108.0 + config.OffsetXMm,
                            YPositionMm = 20.0 + config.OffsetYMm,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            IsFrontFace = false
                        });
                        break;

                    case TargetMediaKind.Epson2CardTray:
                    default:
                        // Standard Epson L805 / L8050 / L18050 2-Card Tray
                        canvasWMm = 130.0;
                        canvasHMm = 220.0;
                        placements.Add(new CardPlacementVisual
                        {
                            Label = "Slot 1 (Front Card)",
                            XPositionMm = 22.2 + config.OffsetXMm,
                            YPositionMm = 18.0 + config.OffsetYMm,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            IsFrontFace = true
                        });
                        placements.Add(new CardPlacementVisual
                        {
                            Label = "Slot 2 (Back Card)",
                            XPositionMm = 22.2 + config.OffsetXMm,
                            YPositionMm = 110.0 + config.OffsetYMm,
                            WidthMm = 85.60,
                            HeightMm = 53.98,
                            IsFrontFace = false
                        });
                        break;
                }

                int canvasWPx = (int)Math.Round((canvasWMm * PreviewDpi) / MmPerInch);
                int canvasHPx = (int)Math.Round((canvasHMm * PreviewDpi) / MmPerInch);

                var previewBmp = new SKBitmap(canvasWPx, canvasHPx, SKColorType.Bgra8888, SKAlphaType.Premul);
                using (var canvas = new SKCanvas(previewBmp))
                {
                    // Draw Media Background (Tray or Paper)
                    if (config.MediaKind == TargetMediaKind.Epson2CardTray || config.MediaKind == TargetMediaKind.CanonMultiTray)
                    {
                        // Industrial Tray body styling
                        canvas.Clear(new SKColor(45, 52, 60)); // Tray Dark Slate
                        using var traySlotPaint = new SKPaint
                        {
                            Color = new SKColor(30, 35, 40),
                            Style = SKPaintStyle.Fill
                        };
                        using var trayBorderPaint = new SKPaint
                        {
                            Color = new SKColor(70, 80, 95),
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 2
                        };

                        // Feed Direction Arrow at Top
                        using var arrowPaint = new SKPaint
                        {
                            Color = new SKColor(200, 200, 200),
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 3,
                            IsAntialias = true
                        };
                        float midX = canvasWPx / 2f;
                        canvas.DrawLine(midX, 15, midX, 35, arrowPaint);
                        canvas.DrawLine(midX - 8, 23, midX, 15, arrowPaint);
                        canvas.DrawLine(midX + 8, 23, midX, 15, arrowPaint);

                        using var font = new SKFont(SKTypeface.Default, 14);
                        using var textPaint = new SKPaint { Color = SKColors.LightGray, IsAntialias = true };
                        canvas.DrawText("FEED DIRECTION ▲", midX + 16, 28, SKTextAlign.Left, font, textPaint);

                        // Draw recessed tray cutouts
                        foreach (var slot in placements)
                        {
                            float sx = (float)((slot.XPositionMm * PreviewDpi) / MmPerInch);
                            float sy = (float)((slot.YPositionMm * PreviewDpi) / MmPerInch);
                            float sw = (float)((slot.WidthMm * PreviewDpi) / MmPerInch);
                            float sh = (float)((slot.HeightMm * PreviewDpi) / MmPerInch);

                            var slotRect = new SKRoundRect(new SKRect(sx - 2, sy - 2, sx + sw + 2, sy + sh + 2), 12, 12);
                            canvas.DrawRoundRect(slotRect, traySlotPaint);
                            canvas.DrawRoundRect(slotRect, trayBorderPaint);
                        }
                    }
                    else
                    {
                        // White Paper Sheet Background
                        canvas.Clear(SKColors.White);
                        using var borderPaint = new SKPaint
                        {
                            Color = new SKColor(220, 220, 220),
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 2
                        };
                        canvas.DrawRect(new SKRect(0, 0, canvasWPx - 1, canvasHPx - 1), borderPaint);
                    }

                    // Draw Cards into Placements
                    using var cardBorderPaint = new SKPaint
                    {
                        Color = new SKColor(20, 100, 200),
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 2,
                        IsAntialias = true
                    };

                    using var labelFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 13);
                    using var labelPaint = new SKPaint
                    {
                        Color = SKColors.White,
                        IsAntialias = true
                    };
                    using var labelBgPaint = new SKPaint
                    {
                        Color = new SKColor(0, 0, 0, 180),
                        Style = SKPaintStyle.Fill
                    };

                    foreach (var p in placements)
                    {
                        float cx = (float)((p.XPositionMm * PreviewDpi) / MmPerInch);
                        float cy = (float)((p.YPositionMm * PreviewDpi) / MmPerInch);
                        float cw = (float)((p.WidthMm * PreviewDpi) / MmPerInch);
                        float ch = (float)((p.HeightMm * PreviewDpi) / MmPerInch);

                        var cardRect = new SKRect(cx, cy, cx + cw, cy + ch);
                        SKBitmap? targetFace = p.IsFrontFace ? frontCard : (backCard ?? frontCard);

                        if (targetFace != null)
                        {
                            if (config.MirrorMode)
                            {
                                canvas.Save();
                                canvas.Scale(-1, 1, cx + (cw / 2f), 0);
                                canvas.DrawBitmap(targetFace, cardRect, new SKSamplingOptions(SKCubicResampler.Mitchell));
                                canvas.Restore();
                            }
                            else
                            {
                                canvas.DrawBitmap(targetFace, cardRect, new SKSamplingOptions(SKCubicResampler.Mitchell));
                            }
                        }
                        else
                        {
                            // Empty card face indicator
                            using var emptyCardPaint = new SKPaint
                            {
                                Color = new SKColor(240, 240, 245),
                                Style = SKPaintStyle.Fill
                            };
                            canvas.DrawRoundRect(new SKRoundRect(cardRect, 8, 8), emptyCardPaint);
                        }

                        // Outline with rounded CR80 corners (CR80 standard radius 3.18mm)
                        float cornerRadiusPx = (float)((3.18 * PreviewDpi) / MmPerInch);
                        canvas.DrawRoundRect(new SKRoundRect(cardRect, cornerRadiusPx, cornerRadiusPx), cardBorderPaint);

                        // Face label badge
                        var badgeRect = new SKRect(cx + 8, cy + 8, cx + 180, cy + 32);
                        canvas.DrawRoundRect(new SKRoundRect(badgeRect, 4, 4), labelBgPaint);
                        canvas.DrawText(p.Label, cx + 14, cy + 25, SKTextAlign.Left, labelFont, labelPaint);
                    }

                    // Millimeter Calibration Offset Grid Indicator at bottom
                    if (Math.Abs(config.OffsetXMm) > 0.01 || Math.Abs(config.OffsetYMm) > 0.01)
                    {
                        string offsetLabel = $"CALIBRATION OFFSET: X = {config.OffsetXMm:+0.0;-0.0;0.0} mm | Y = {config.OffsetYMm:+0.0;-0.0;0.0} mm";
                        using var offsetFont = new SKFont(SKTypeface.Default, 13);
                        using var offsetPaint = new SKPaint { Color = new SKColor(255, 200, 50), IsAntialias = true };
                        canvas.DrawText(offsetLabel, 15, canvasHPx - 15, SKTextAlign.Left, offsetFont, offsetPaint);
                    }

                    canvas.Flush();
                }

                return new PrintPreviewRender(previewBmp, canvasWMm, canvasHMm)
                {
                    Placements = placements
                };
            });
        }

        public Task<PrintPreviewRender> GenerateDocumentPreviewAsync(
            IReadOnlyList<SKBitmap> pages,
            PrintPreviewConfig config,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                double canvasWMm = 210.0; // A4
                double canvasHMm = 297.0;

                int canvasWPx = (int)Math.Round((canvasWMm * PreviewDpi) / MmPerInch);
                int canvasHPx = (int)Math.Round((canvasHMm * PreviewDpi) / MmPerInch);

                var previewBmp = new SKBitmap(canvasWPx, canvasHPx, SKColorType.Bgra8888, SKAlphaType.Premul);
                using (var canvas = new SKCanvas(previewBmp))
                {
                    canvas.Clear(SKColors.White);

                    if (pages != null && pages.Count > 0)
                    {
                        var firstPage = pages[0];
                        canvas.DrawBitmap(firstPage, new SKRect(0, 0, canvasWPx, canvasHPx), new SKSamplingOptions(SKCubicResampler.Mitchell));
                    }

                    using var borderPaint = new SKPaint
                    {
                        Color = new SKColor(200, 200, 200),
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 2
                    };
                    canvas.DrawRect(new SKRect(0, 0, canvasWPx - 1, canvasHPx - 1), borderPaint);
                    canvas.Flush();
                }

                return new PrintPreviewRender(previewBmp, canvasWMm, canvasHMm);
            });
        }
    }
}
