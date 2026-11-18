using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class PhysicalPageRenderService : IPhysicalPageRenderService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public SKBitmap RenderLayoutToBitmap(PageLayoutModel model, int dpi = 300)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (dpi <= 0) dpi = 300;

            int widthPx = (int)Math.Round(model.PageWidthMm * dpi / 25.4);
            int heightPx = (int)Math.Round(model.PageHeightMm * dpi / 25.4);

            if (widthPx < 1) widthPx = 1;
            if (heightPx < 1) heightPx = 1;

            var bitmap = new SKBitmap(widthPx, heightPx, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);

            RenderToPrintCanvas(model, canvas, dpi);
            return bitmap;
        }

        public void RenderToPrintCanvas(PageLayoutModel model, SKCanvas canvas, int targetDpi)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            if (targetDpi <= 0) targetDpi = 300;

            float mmToPx = (float)(targetDpi / 25.4);
            float pageWidthPx = (float)(model.PageWidthMm * mmToPx);
            float pageHeightPx = (float)(model.PageHeightMm * mmToPx);

            // 1. Draw base white paper
            canvas.Clear(SKColors.White);

            canvas.Save();

            // 2. Handle mirror mode if requested
            if (model.MirrorMode)
            {
                canvas.Scale(-1f, 1f, pageWidthPx / 2f, 0f);
            }

            // 3. Render items sorted by ZIndex
            var sortedItems = model.Items.OrderBy(i => i.ZIndex).ToList();
            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

            using var cutMarkPaint = new SKPaint
            {
                Color = new SKColor(160, 160, 160, 200),
                StrokeWidth = Math.Max(1f, 0.2f * mmToPx),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            foreach (var item in sortedItems)
            {
                float x = (float)((model.OffsetXMm + item.XPositionMm) * mmToPx);
                float y = (float)((model.OffsetYMm + item.YPositionMm) * mmToPx);
                float w = (float)(item.WidthMm * mmToPx);
                float h = (float)(item.HeightMm * mmToPx);

                canvas.Save();

                if (Math.Abs(item.RotationDegrees) > 0.001)
                {
                    float cx = x + w / 2f;
                    float cy = y + h / 2f;
                    canvas.RotateDegrees((float)item.RotationDegrees, cx, cy);
                }

                var destRect = new SKRect(x, y, x + w, y + h);

                if (item.Bitmap != null)
                {
                    using var paint = new SKPaint { IsAntialias = true };
                    canvas.DrawBitmap(item.Bitmap, destRect, sampling, paint);
                }
                else if (!string.IsNullOrEmpty(item.TextContent))
                {
                    using var font = new SKFont(SKTypeface.FromFamilyName(item.TextFontFamily ?? "Segoe UI"), item.TextFontSizePt * (targetDpi / 72f));
                    using var textPaint = new SKPaint
                    {
                        Color = SKColor.TryParse(item.TextColorHex, out var col) ? col : SKColors.Black,
                        IsAntialias = true
                    };
                    canvas.DrawText(item.TextContent, x, y + font.Size, SKTextAlign.Left, font, textPaint);
                }
                else
                {
                    // Placeholder card outline
                    using var placeholderPaint = new SKPaint
                    {
                        Color = new SKColor(220, 220, 220),
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 1f
                    };
                    canvas.DrawRect(destRect, placeholderPaint);
                }

                // 4. Draw cut marks if enabled
                if (model.ShowCutMarks && w > 0 && h > 0)
                {
                    float markLen = 3.0f * mmToPx; // 3mm mark
                    // Top-left
                    canvas.DrawLine(x - markLen, y, x, y, cutMarkPaint);
                    canvas.DrawLine(x, y - markLen, x, y, cutMarkPaint);
                    // Top-right
                    canvas.DrawLine(x + w, y, x + w + markLen, y, cutMarkPaint);
                    canvas.DrawLine(x + w, y - markLen, x + w, y, cutMarkPaint);
                    // Bottom-left
                    canvas.DrawLine(x - markLen, y + h, x, y + h, cutMarkPaint);
                    canvas.DrawLine(x, y + h, x, y + h + markLen, cutMarkPaint);
                    // Bottom-right
                    canvas.DrawLine(x + w, y + h, x + w + markLen, y + h, cutMarkPaint);
                    canvas.DrawLine(x + w, y + h, x + w, y + h + markLen, cutMarkPaint);
                }

                canvas.Restore();
            }

            // 5. Draw fold line if enabled
            if (model.ShowFoldLine && sortedItems.Count >= 2)
            {
                using var foldPaint = new SKPaint
                {
                    Color = new SKColor(180, 180, 180, 180),
                    StrokeWidth = 1f,
                    PathEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, 0),
                    Style = SKPaintStyle.Stroke
                };
                // Draw horizontal center fold between top & bottom items or vertical center fold
                float foldY = (float)((model.Items[0].YPositionMm + model.Items[0].HeightMm + 
                    (model.Items.Count > 1 ? model.Items[1].YPositionMm : model.Items[0].YPositionMm + model.Items[0].HeightMm)) / 2.0 * mmToPx);
                canvas.DrawLine(0, foldY, pageWidthPx, foldY, foldPaint);
            }

            canvas.Restore();
        }

        public byte[] ExportLayoutToPdf(PageLayoutModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            using var stream = new MemoryStream();
            
            // 72 DPI coordinate space in PDF points
            float widthPts = (float)(model.PageWidthMm * 72.0 / 25.4);
            float heightPts = (float)(model.PageHeightMm * 72.0 / 25.4);

            using (var document = SKDocument.CreatePdf(stream))
            {
                using var canvas = document.BeginPage(widthPts, heightPts);
                RenderToPrintCanvas(model, canvas, 72);
                document.EndPage();
                document.Close();
            }

            return stream.ToArray();
        }

        public string SerializeProject(PageLayoutModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            // Hydrate Base64ImageData from Bitmaps
            foreach (var item in model.Items)
            {
                if (item.Bitmap != null)
                {
                    using var image = SKImage.FromBitmap(item.Bitmap);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    item.Base64ImageData = Convert.ToBase64String(data.ToArray());
                }
            }

            return JsonSerializer.Serialize(model, JsonOptions);
        }

        public PageLayoutModel DeserializeProject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON string cannot be empty", nameof(json));

            var model = JsonSerializer.Deserialize<PageLayoutModel>(json, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize page layout model.");

            // Reconstruct SKBitmaps from Base64ImageData
            foreach (var item in model.Items)
            {
                if (!string.IsNullOrEmpty(item.Base64ImageData))
                {
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(item.Base64ImageData);
                        item.Bitmap = SKBitmap.Decode(bytes);
                    }
                    catch
                    {
                        item.Bitmap = null;
                    }
                }
            }

            return model;
        }

        public async Task SaveProjectAsync(PageLayoutModel model, string filePath)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path cannot be empty", nameof(filePath));

            string json = SerializeProject(model);
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task<PageLayoutModel> LoadProjectAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path cannot be empty", nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("Project file not found", filePath);

            string json = await File.ReadAllTextAsync(filePath);
            return DeserializeProject(json);
        }
    }
}
