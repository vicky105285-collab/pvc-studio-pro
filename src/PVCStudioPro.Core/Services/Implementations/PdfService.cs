using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Docnet.Core;
using Docnet.Core.Exceptions;
using Docnet.Core.Models;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class PdfService : IPdfService
    {
        private const double MmPerInch = 25.4;
        private const double PointsPerInch = 72.0;

        public Task<PdfDocumentInfo> InspectDocumentAsync(Stream pdfStream, string? password = null)
        {
            return Task.Run(() =>
            {
                byte[] bytes = ToByteArray(pdfStream);
                var info = new PdfDocumentInfo();

                try
                {
                    using var docReader = string.IsNullOrEmpty(password)
                        ? DocLib.Instance.GetDocReader(bytes, new PageDimensions(1.0))
                        : DocLib.Instance.GetDocReader(bytes, password, new PageDimensions(1.0));

                    info.PageCount = docReader.GetPageCount();
                    if (info.PageCount > 0)
                    {
                        using var pageReader = docReader.GetPageReader(0);
                        int wPt = pageReader.GetPageWidth();
                        int hPt = pageReader.GetPageHeight();
                        info.PageWidthMm = (wPt * MmPerInch) / PointsPerInch;
                        info.PageHeightMm = (hPt * MmPerInch) / PointsPerInch;
                        info.ExtractedTextSample = pageReader.GetText() ?? string.Empty;
                    }
                }
                catch (DocnetException ex) when (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
                {
                    info.IsEncrypted = true;
                }
                catch (Exception ex)
                {
                    if (ex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        info.IsEncrypted = true;
                    }
                    else
                    {
                        throw;
                    }
                }

                return info;
            });
        }

        public Task<SKBitmap> RenderPageAsync(Stream pdfStream, int pageIndex, int dpi = 600, string? password = null)
        {
            return Task.Run(() =>
            {
                byte[] bytes = ToByteArray(pdfStream);

                using var probeDoc = string.IsNullOrEmpty(password)
                    ? DocLib.Instance.GetDocReader(bytes, new PageDimensions(1.0))
                    : DocLib.Instance.GetDocReader(bytes, password, new PageDimensions(1.0));

                if (pageIndex < 0 || pageIndex >= probeDoc.GetPageCount())
                    throw new ArgumentOutOfRangeException(nameof(pageIndex), $"Page {pageIndex} is out of range.");

                using var probePage = probeDoc.GetPageReader(pageIndex);
                int baseWidthPt = probePage.GetPageWidth();
                int baseHeightPt = probePage.GetPageHeight();

                int targetWidthPx = (int)Math.Round((baseWidthPt * dpi) / PointsPerInch);
                int targetHeightPx = (int)Math.Round((baseHeightPt * dpi) / PointsPerInch);

                using var renderDoc = string.IsNullOrEmpty(password)
                    ? DocLib.Instance.GetDocReader(bytes, new PageDimensions(targetWidthPx, targetHeightPx))
                    : DocLib.Instance.GetDocReader(bytes, password, new PageDimensions(targetWidthPx, targetHeightPx));

                using var renderPage = renderDoc.GetPageReader(pageIndex);
                byte[] rawBgra = renderPage.GetImage();
                int renderedWidth = renderPage.GetPageWidth();
                int renderedHeight = renderPage.GetPageHeight();

                var bitmap = new SKBitmap(renderedWidth, renderedHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
                IntPtr pixelsPtr = bitmap.GetPixels();
                Marshal.Copy(rawBgra, 0, pixelsPtr, rawBgra.Length);

                return bitmap;
            });
        }

        public async Task<(SKBitmap Front, SKBitmap? Back)> ExtractCardFacesAsync(
            Stream pdfStream,
            CardTemplate template,
            string? password = null,
            bool applyMask = false)
        {
            const int dpi = 600;
            double cr80WidthMm = template.PhysicalCard?.WidthMm ?? 85.60;
            double cr80HeightMm = template.PhysicalCard?.HeightMm ?? 53.98;

            int cr80WidthPx = (int)Math.Round((cr80WidthMm * dpi) / MmPerInch);
            int cr80HeightPx = (int)Math.Round((cr80HeightMm * dpi) / MmPerInch);

            // Render front page
            SKBitmap frontSource = await RenderPageAsync(pdfStream, template.Faces.Front.SourcePage, dpi, password);
            SKBitmap frontCard = CropAndOrientCard(frontSource, template.Faces.Front, cr80WidthPx, cr80HeightPx, dpi, applyMask);
            frontSource.Dispose();

            SKBitmap? backCard = null;
            if (template.Faces.Back != null)
            {
                SKBitmap backSource = await RenderPageAsync(pdfStream, template.Faces.Back.SourcePage, dpi, password);
                backCard = CropAndOrientCard(backSource, template.Faces.Back, cr80WidthPx, cr80HeightPx, dpi, applyMask);
                backSource.Dispose();
            }

            return (frontCard, backCard);
        }

        private SKBitmap CropAndOrientCard(
            SKBitmap source,
            FaceDefinition face,
            int targetWidthPx,
            int targetHeightPx,
            int dpi,
            bool applyMask)
        {
            int cropX = (int)Math.Round((face.CropRectMm.X * dpi) / MmPerInch);
            int cropY = (int)Math.Round((face.CropRectMm.Y * dpi) / MmPerInch);
            int cropW = (int)Math.Round((face.CropRectMm.Width * dpi) / MmPerInch);
            int cropH = (int)Math.Round((face.CropRectMm.Height * dpi) / MmPerInch);

            // Ensure coordinates stay inside source bounds
            cropX = Math.Max(0, Math.Min(cropX, source.Width - 1));
            cropY = Math.Max(0, Math.Min(cropY, source.Height - 1));
            cropW = Math.Min(cropW, source.Width - cropX);
            cropH = Math.Min(cropH, source.Height - cropY);

            var cropRect = new SKRectI(cropX, cropY, cropX + cropW, cropY + cropH);
            using var cropped = new SKBitmap();
            source.ExtractSubset(cropped, cropRect);

            var finalCard = new SKBitmap(targetWidthPx, targetHeightPx, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(finalCard);
            canvas.Clear(SKColors.White);

            if (face.RotationDegrees != 0)
            {
                canvas.Save();
                canvas.Translate(targetWidthPx / 2f, targetHeightPx / 2f);
                canvas.RotateDegrees(face.RotationDegrees);
                canvas.Translate(-targetWidthPx / 2f, -targetHeightPx / 2f);
            }

            var destRect = new SKRect(0, 0, targetWidthPx, targetHeightPx);
            canvas.DrawBitmap(cropped, destRect, new SKSamplingOptions(SKCubicResampler.Mitchell));

            if (face.RotationDegrees != 0)
            {
                canvas.Restore();
            }

            if (applyMask && face.MaskZones != null && face.MaskZones.Count > 0)
            {
                using var maskPaint = new SKPaint
                {
                    Color = SKColors.White,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };

                using var textPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    IsAntialias = true
                };

                using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
                using var font = new SKFont(typeface, 24.0f * (dpi / 300.0f));

                foreach (var mz in face.MaskZones)
                {
                    float maskX = (float)((mz.RelativeXMm * dpi) / MmPerInch);
                    float maskY = (float)((mz.RelativeYMm * dpi) / MmPerInch);
                    float maskW = (float)((mz.WidthMm * dpi) / MmPerInch);
                    float maskH = (float)((mz.HeightMm * dpi) / MmPerInch);

                    var maskRect = new SKRect(maskX, maskY, maskX + maskW, maskY + maskH);
                    canvas.DrawRect(maskRect, maskPaint);

                    if (!string.IsNullOrEmpty(mz.OverlayText))
                    {
                        canvas.DrawText(mz.OverlayText, maskX + 10, maskY + (maskH * 0.75f), SKTextAlign.Left, font, textPaint);
                    }
                }
            }

            canvas.Flush();
            return finalCard;
        }

        private static byte[] ToByteArray(Stream stream)
        {
            if (stream is MemoryStream ms)
            {
                return ms.ToArray();
            }

            using var memoryStream = new MemoryStream();
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
