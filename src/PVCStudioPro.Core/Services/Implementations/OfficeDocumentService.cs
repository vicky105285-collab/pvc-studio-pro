using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class OfficeDocumentService : IOfficeDocumentService
    {
        private const double MmPerInch = 25.4;

        public Task<string> ExtractTextAsync(Stream stream, FileMagicKind kind, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (stream.CanSeek) stream.Position = 0;

                switch (kind)
                {
                    case FileMagicKind.Docx:
                        return ExtractTextFromDocx(stream);
                    case FileMagicKind.Rtf:
                        return ExtractTextFromRtf(stream);
                    case FileMagicKind.Text:
                    default:
                        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
                        {
                            return reader.ReadToEnd();
                        }
                }
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<SKBitmap>> RenderOfficeDocumentAsync(
            Stream stream,
            FileMagicKind kind,
            int dpi = 300,
            CancellationToken cancellationToken = default)
        {
            string rawText = await ExtractTextAsync(stream, kind, cancellationToken);
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Standard A4 dimensions in pixels at given DPI (210 x 297 mm)
                int pageWidthPx = (int)Math.Round((210.0 * dpi) / MmPerInch);
                int pageHeightPx = (int)Math.Round((297.0 * dpi) / MmPerInch);

                float marginPx = (float)((20.0 * dpi) / MmPerInch); // 20mm margins
                float printableWidth = pageWidthPx - (2 * marginPx);
                float printableHeight = pageHeightPx - (2 * marginPx);

                float fontSize = 12.0f * (dpi / 72.0f); // 12pt font
                float lineHeight = fontSize * 1.4f;

                using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal);
                using var font = new SKFont(typeface, fontSize);
                using var textPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    IsAntialias = true
                };

                using var headerFont = new SKFont(typeface, 9.0f * (dpi / 72.0f));
                using var headerPaint = new SKPaint
                {
                    Color = new SKColor(128, 128, 128),
                    IsAntialias = true
                };

                // Split raw text into paragraphs
                var paragraphs = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var wrappedLines = new List<string>();

                foreach (var para in paragraphs)
                {
                    if (string.IsNullOrWhiteSpace(para))
                    {
                        wrappedLines.Add(string.Empty);
                        continue;
                    }

                    // Word-wrap paragraph
                    var words = para.Split(' ');
                    var currentLine = new StringBuilder();

                    foreach (var word in words)
                    {
                        string testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
                        float textWidth = font.MeasureText(testLine);

                        if (textWidth > printableWidth && currentLine.Length > 0)
                        {
                            wrappedLines.Add(currentLine.ToString());
                            currentLine.Clear();
                            currentLine.Append(word);
                        }
                        else
                        {
                            currentLine.Clear();
                            currentLine.Append(testLine);
                        }
                    }

                    if (currentLine.Length > 0)
                    {
                        wrappedLines.Add(currentLine.ToString());
                    }
                }

                // Paginate lines
                int linesPerPage = Math.Max(1, (int)(printableHeight / lineHeight));
                var pages = new List<SKBitmap>();
                int totalPages = Math.Max(1, (int)Math.Ceiling((double)wrappedLines.Count / linesPerPage));

                for (int pageIdx = 0; pageIdx < totalPages; pageIdx++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var pageBmp = new SKBitmap(pageWidthPx, pageHeightPx, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using var canvas = new SKCanvas(pageBmp);
                    canvas.Clear(SKColors.White);

                    // Draw Header / Page number
                    string pageHeader = $"PVC Studio Pro Document Desk — Page {pageIdx + 1} of {totalPages}";
                    canvas.DrawText(pageHeader, marginPx, marginPx * 0.7f, SKTextAlign.Left, headerFont, headerPaint);

                    canvas.DrawLine(marginPx, marginPx * 0.85f, pageWidthPx - marginPx, marginPx * 0.85f, headerPaint);

                    // Draw text lines
                    float y = marginPx + lineHeight;
                    int startLine = pageIdx * linesPerPage;
                    int endLine = Math.Min(startLine + linesPerPage, wrappedLines.Count);

                    for (int l = startLine; l < endLine; l++)
                    {
                        string line = wrappedLines[l];
                        if (!string.IsNullOrEmpty(line))
                        {
                            canvas.DrawText(line, marginPx, y, SKTextAlign.Left, font, textPaint);
                        }
                        y += lineHeight;
                    }

                    canvas.Flush();
                    pages.Add(pageBmp);
                }

                return (IReadOnlyList<SKBitmap>)pages;
            }, cancellationToken);
        }

        private static string ExtractTextFromDocx(Stream stream)
        {
            try
            {
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                var entry = zip.GetEntry("word/document.xml");
                if (entry == null) return string.Empty;

                using var entryStream = entry.Open();
                var doc = XDocument.Load(entryStream);
                XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

                var sb = new StringBuilder();
                foreach (var p in doc.Descendants(w + "p"))
                {
                    var pText = new StringBuilder();
                    foreach (var t in p.Descendants(w + "t"))
                    {
                        pText.Append(t.Value);
                    }
                    if (pText.Length > 0)
                    {
                        sb.AppendLine(pText.ToString());
                    }
                    else
                    {
                        sb.AppendLine();
                    }
                }

                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ExtractTextFromRtf(Stream stream)
        {
            try
            {
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                string rtf = reader.ReadToEnd();

                // Simple RTF text stripper
                string plain = Regex.Replace(rtf, @"\\([a-z]{1,32})(-?\d+)? ?|\\\'[0-9a-f]{2}|[{}]", "", RegexOptions.IgnoreCase);
                return plain.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
