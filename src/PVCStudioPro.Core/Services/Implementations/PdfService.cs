using System;
using System.Collections.Generic;
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
    public class PdfService : IPdfDocumentService
    {
        private const double MmPerInch = 25.4;
        private const double PointsPerInch = 72.0;

        // Resource & Safety Limits
        private const int MaxAllowedPages = 50;
        private const double MaxAllowedPageDimensionMm = 1200.0; // 1.2 meters
        private const long MaxAllowedStreamBytes = 150 * 1024 * 1024; // 150 MB

        public Task<PasswordUnlockResult> TryUnlockPasswordAsync(
            Stream pdfStream,
            string? password,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] bytes = ToByteArrayWithLimit(pdfStream, MaxAllowedStreamBytes);

                try
                {
                    // 1. First test if document opens without password
                    try
                    {
                        using var noPassDoc = DocLib.Instance.GetDocReader(bytes, new PageDimensions(1.0));
                        int count = noPassDoc.GetPageCount();
                        if (count > 0)
                        {
                            return PasswordUnlockResult.Unencrypted();
                        }
                    }
                    catch (DocnetException ex) when (IsPasswordException(ex))
                    {
                        // Needs password
                    }

                    // 2. If password is null or empty, report password required
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        return PasswordUnlockResult.Required("Document is password protected. Please enter the 8-character decryption password.");
                    }

                    // 3. Candidate password attempts:
                    // Candidate A: trimmed original
                    // Candidate B: uppercase (UIDAI standard e.g. SURE1985)
                    // Candidate C: lowercase
                    string trimmed = password.Trim();
                    var candidates = new List<string> { trimmed };
                    string upper = trimmed.ToUpperInvariant();
                    if (!candidates.Contains(upper)) candidates.Add(upper);
                    string lower = trimmed.ToLowerInvariant();
                    if (!candidates.Contains(lower)) candidates.Add(lower);

                    foreach (var candidate in candidates)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            using var passDoc = DocLib.Instance.GetDocReader(bytes, candidate, new PageDimensions(1.0));
                            if (passDoc.GetPageCount() > 0)
                            {
                                return PasswordUnlockResult.Success(candidate);
                            }
                        }
                        catch (DocnetException ex) when (IsPasswordException(ex))
                        {
                            // Try next candidate
                        }
                    }

                    return PasswordUnlockResult.Invalid("Incorrect password. For UIDAI e-Aadhaar, format is FIRST4_OF_NAME_CAPS + YYYY (e.g. SURE1985).");
                }
                catch (DocnetException ex)
                {
                    return PasswordUnlockResult.Corrupt($"PDF structure error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    return PasswordUnlockResult.Corrupt($"Error inspecting document: {ex.Message}");
                }
                finally
                {
                    Array.Clear(bytes, 0, bytes.Length);
                }
            }, cancellationToken);
        }

        public async Task<PdfDocumentInfo> InspectDocumentAsync(
            Stream pdfStream,
            string? password = null,
            CancellationToken cancellationToken = default)
        {
            var unlockResult = await TryUnlockPasswordAsync(pdfStream, password, cancellationToken);
            var info = new PdfDocumentInfo
            {
                IsEncrypted = unlockResult.Status == PdfPasswordStatus.PasswordRequired ||
                              unlockResult.Status == PdfPasswordStatus.DecryptionSuccessful
            };

            if (unlockResult.Status == PdfPasswordStatus.PasswordRequired ||
                unlockResult.Status == PdfPasswordStatus.InvalidPassword)
            {
                return info;
            }

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] bytes = ToByteArrayWithLimit(pdfStream, MaxAllowedStreamBytes);

                try
                {
                    string? effectivePw = unlockResult.EffectivePassword ?? password;
                    using var docReader = string.IsNullOrEmpty(effectivePw)
                        ? DocLib.Instance.GetDocReader(bytes, new PageDimensions(1.0))
                        : DocLib.Instance.GetDocReader(bytes, effectivePw, new PageDimensions(1.0));

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
                finally
                {
                    Array.Clear(bytes, 0, bytes.Length);
                }

                return info;
            }, cancellationToken);
        }

        public async Task<SKBitmap> RenderPageAsync(
            Stream pdfStream,
            int pageIndex,
            int dpi = 600,
            string? password = null,
            CancellationToken cancellationToken = default)
        {
            if (dpi < 72 || dpi > 1200)
                throw new ArgumentOutOfRangeException(nameof(dpi), "Render DPI must be between 72 and 1200 DPI.");

            var unlock = await TryUnlockPasswordAsync(pdfStream, password, cancellationToken);
            string? effectivePw = unlock.EffectivePassword ?? password;

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] bytes = ToByteArrayWithLimit(pdfStream, MaxAllowedStreamBytes);

                try
                {
                    double scalingFactor = (double)dpi / PointsPerInch;
                    using var renderDoc = string.IsNullOrEmpty(effectivePw)
                        ? DocLib.Instance.GetDocReader(bytes, new PageDimensions(scalingFactor))
                        : DocLib.Instance.GetDocReader(bytes, effectivePw, new PageDimensions(scalingFactor));

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
                catch (DocnetException ex) when (IsPasswordException(ex))
                {
                    throw new PdfPasswordRequiredException("The document is password-protected. Please provide a valid decryption password.");
                }
                finally
                {
                    Array.Clear(bytes, 0, bytes.Length);
                }
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<SKBitmap>> RenderAllPagesAsync(
            Stream pdfStream,
            int dpi = 300,
            string? password = null,
            CancellationToken cancellationToken = default)
        {
            var unlock = await TryUnlockPasswordAsync(pdfStream, password, cancellationToken);
            string? effectivePw = unlock.EffectivePassword ?? password;

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] bytes = ToByteArrayWithLimit(pdfStream, MaxAllowedStreamBytes);
                var pages = new List<SKBitmap>();

                try
                {
                    double scalingFactor = (double)dpi / PointsPerInch;
                    using var renderDoc = string.IsNullOrEmpty(effectivePw)
                        ? DocLib.Instance.GetDocReader(bytes, new PageDimensions(scalingFactor))
                        : DocLib.Instance.GetDocReader(bytes, effectivePw, new PageDimensions(scalingFactor));

                    int count = Math.Min(renderDoc.GetPageCount(), 20); // render up to 20 pages
                    for (int i = 0; i < count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        using var pageReader = renderDoc.GetPageReader(i);
                        byte[] rawBgra = pageReader.GetImage();
                        int w = pageReader.GetPageWidth();
                        int h = pageReader.GetPageHeight();

                        var bmp = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
                        Marshal.Copy(rawBgra, 0, bmp.GetPixels(), rawBgra.Length);
                        pages.Add(bmp);
                    }

                    return (IReadOnlyList<SKBitmap>)pages;
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

        private static bool IsPasswordException(Exception ex)
        {
            return ex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   ex.Message.IndexOf("handler", StringComparison.OrdinalIgnoreCase) >= 0;
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
