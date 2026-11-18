using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
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

        // Resource & Safety Limits
        private const int MaxAllowedPages = 50;
        private const double MaxAllowedPageDimensionMm = 1200.0; // 1.2 meters
        private const long MaxAllowedStreamBytes = 150 * 1024 * 1024; // 150 MB

        public Task<PdfDocumentInfo> InspectDocumentAsync(Stream pdfStream, string? password = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] bytes = ToByteArrayWithLimit(pdfStream, MaxAllowedStreamBytes);
                var info = new PdfDocumentInfo();

                try
                {
                    using var docReader = string.IsNullOrEmpty(password)
                        ? DocLib.Instance.GetDocReader(bytes, new PageDimensions(1.0))
                        : DocLib.Instance.GetDocReader(bytes, password, new PageDimensions(1.0));

                    info.PageCount = docReader.GetPageCount();
                    if (info.PageCount > MaxAllowedPages)
                    {
                        throw new PdfSecurityException($"PDF exceeds maximum allowable page count ({info.PageCount} > {MaxAllowedPages}).");
                    }

                    if (info.PageCount > 0)
                    {
                        using var pageReader = docReader.GetPageReader(0);
                        int wPt = pageReader.GetPageWidth();
                        int hPt = pageReader.GetPageHeight();
                        info.PageWidthMm = (wPt * MmPerInch) / PointsPerInch;
                        info.PageHeightMm = (hPt * MmPerInch) / PointsPerInch;

                        if (info.PageWidthMm > MaxAllowedPageDimensionMm || info.PageHeightMm > MaxAllowedPageDimensionMm)
                        {
                            throw new PdfSecurityException($"PDF dimensions exceed maximum physical safety threshold ({info.PageWidthMm:F0}x{info.PageHeightMm:F0} mm).");
                        }

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
                    else if (ex is PdfSecurityException)
                    {
                        throw;
                    }
                    else
                    {
                        throw new InvalidOperationException($"PDF inspection failure: {ex.Message}", ex);
                    }
                }
                finally
                {
                    // Best-effort memory scrub of buffer
                    Array.Clear(bytes, 0, bytes.Length);
                }

                return info;
            }, cancellationToken);
        }

        public Task<SKBitmap> RenderPageAsync(Stream pdfStream, int pageIndex, int dpi = 600, string? password = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (dpi < 72 || dpi > 1200)
                    throw new ArgumentOutOfRangeException(nameof(dpi), "Render DPI must be between 72 and 1200 DPI.");

                byte[] bytes = ToByteArrayWithLimit(pdfStream, MaxAllowedStreamBytes);

                try
                {
                    double scalingFactor = (double)dpi / PointsPerInch;
                    using var renderDoc = string.IsNullOrEmpty(password)
                        ? DocLib.Instance.GetDocReader(bytes, new PageDimensions(scalingFactor))
                        : DocLib.Instance.GetDocReader(bytes, password, new PageDimensions(scalingFactor));

                    if (pageIndex < 0 || pageIndex >= renderDoc.GetPageCount())
                        throw new ArgumentOutOfRangeException(nameof(pageIndex), $"Page {pageIndex} is out of bounds (document has {renderDoc.GetPageCount()} pages).");

                    cancellationToken.ThrowIfCancellationRequested();

                    using var renderPage = renderDoc.GetPageReader(pageIndex);
                    byte[] rawBgra = renderPage.GetImage();
                    int renderedWidth = renderPage.GetPageWidth();
                    int renderedHeight = renderPage.GetPageHeight();

                    cancellationToken.ThrowIfCancellationRequested();

                    var bitmap = new SKBitmap(renderedWidth, renderedHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
                    IntPtr pixelsPtr = bitmap.GetPixels();
                    Marshal.Copy(rawBgra, 0, pixelsPtr, rawBgra.Length);

                    return bitmap;
                }
                catch (DocnetException ex) when (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
                {
                    throw new PdfPasswordRequiredException("The document is password-protected. Please provide a valid decryption password.");
                }
                finally
                {
                    Array.Clear(bytes, 0, bytes.Length);
                }
            }, cancellationToken);
        }

        public async Task<(SKBitmap Front, SKBitmap? Back)> ExtractCardFacesAsync(
            Stream pdfStream,
            CardTemplate template,
            string? password = null,
            bool applyMask = false,
            CancellationToken cancellationToken = default)
        {
            const int dpi = 600;
            double cr80WidthMm = template.PhysicalCard?.WidthMm ?? 85.60;
            double cr80HeightMm = template.PhysicalCard?.HeightMm ?? 53.98;

            int cr80WidthPx = (int)Math.Round((cr80WidthMm * dpi) / MmPerInch);
            int cr80HeightPx = (int)Math.Round((cr80HeightMm * dpi) / MmPerInch);

            // Render front page
            SKBitmap frontSource = await RenderPageAsync(pdfStream, template.Faces.Front.SourcePage, dpi, password, cancellationToken);
            SKBitmap frontCard = CropAndOrientCard(frontSource, template.Faces.Front, cr80WidthPx, cr80HeightPx, dpi, applyMask);
            frontSource.Dispose();

            cancellationToken.ThrowIfCancellationRequested();

            SKBitmap? backCard = null;
            if (template.Faces.Back != null)
            {
                SKBitmap backSource = await RenderPageAsync(pdfStream, template.Faces.Back.SourcePage, dpi, password, cancellationToken);
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

            // Apply Masking Zones if requested (UIDAI Masked Aadhaar compliance)
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

        private static byte[] ToByteArrayWithLimit(Stream stream, long maxBytes)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
                if (stream.Length > maxBytes)
                {
                    throw new PdfSecurityException($"PDF file size ({stream.Length / (1024 * 1024)} MB) exceeds maximum security limit ({maxBytes / (1024 * 1024)} MB).");
                }
            }

            if (stream is MemoryStream ms)
            {
                return ms.ToArray();
            }

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            if (memoryStream.Length > maxBytes)
            {
                throw new PdfSecurityException($"PDF content exceeds maximum allowable size ({maxBytes / (1024 * 1024)} MB).");
            }
            return memoryStream.ToArray();
        }
    }
}
