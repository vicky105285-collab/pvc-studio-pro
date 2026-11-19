using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class DocumentImportService : IDocumentImportService
    {
        public DocumentSniffResult SniffFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return new DocumentSniffResult
                {
                    FilePath = filePath ?? string.Empty,
                    MagicKind = FileMagicKind.Unknown,
                    IsCorrupt = true,
                    Notes = "File does not exist or path is invalid."
                };
            }

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var result = SniffStream(fs, Path.GetFileName(filePath));
                result.FilePath = filePath;
                result.FileSize = fs.Length;
                return result;
            }
            catch (Exception ex)
            {
                return new DocumentSniffResult
                {
                    FilePath = filePath,
                    MagicKind = FileMagicKind.Unknown,
                    IsCorrupt = true,
                    Notes = $"Failed to read file: {ex.Message}"
                };
            }
        }

        public DocumentSniffResult SniffStream(Stream stream, string? originalFileName = null)
        {
            var result = new DocumentSniffResult
            {
                FilePath = originalFileName ?? "stream_document"
            };

            if (stream == null || !stream.CanRead)
            {
                result.IsCorrupt = true;
                result.Notes = "Stream is null or unreadable.";
                return result;
            }

            long initialPos = stream.CanSeek ? stream.Position : 0;
            if (stream.CanSeek)
            {
                stream.Position = 0;
                result.FileSize = stream.Length;
            }

            byte[] header = new byte[Math.Min(1024, stream.Length > 0 ? (int)Math.Min(1024, stream.Length) : 1024)];
            int bytesRead = stream.Read(header, 0, header.Length);

            if (stream.CanSeek)
            {
                stream.Position = initialPos;
            }

            if (bytesRead < 4)
            {
                result.IsCorrupt = true;
                result.Notes = "File is empty or too short to identify.";
                return result;
            }

            result.MagicHex = BitConverter.ToString(header, 0, Math.Min(bytesRead, 16)).Replace("-", " ");

            // 1. PDF: %PDF- (0x25, 0x50, 0x44, 0x46)
            if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
            {
                result.MagicKind = FileMagicKind.Pdf;
                result.MimeType = "application/pdf";
                result.Notes = "Standard Adobe PDF document.";
                return result;
            }

            // 2. JPEG: 0xFF, 0xD8, 0xFF
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            {
                result.MagicKind = FileMagicKind.Jpeg;
                result.MimeType = "image/jpeg";
                result.Notes = "JPEG photo / image.";
                return result;
            }

            // 3. PNG: 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
            if (bytesRead >= 8 &&
                header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            {
                result.MagicKind = FileMagicKind.Png;
                result.MimeType = "image/png";
                result.Notes = "PNG raster graphic.";
                return result;
            }

            // 4. BMP: 'B', 'M' (0x42, 0x4D)
            if (header[0] == 0x42 && header[1] == 0x4D)
            {
                result.MagicKind = FileMagicKind.Bmp;
                result.MimeType = "image/bmp";
                result.Notes = "Windows Bitmap image.";
                return result;
            }

            // 5. TIFF: II*\0 or MM\0*
            if (bytesRead >= 4 &&
                ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) ||
                 (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A)))
            {
                result.MagicKind = FileMagicKind.Tiff;
                result.MimeType = "image/tiff";
                result.Notes = "TIFF image format.";
                return result;
            }

            // 6. WEBP: RIFF....WEBP
            if (bytesRead >= 12 &&
                header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            {
                result.MagicKind = FileMagicKind.Webp;
                result.MimeType = "image/webp";
                result.Notes = "Google WebP image.";
                return result;
            }

            // 7. RTF: {\rtf (0x7B, 0x5C, 0x72, 0x74, 0x66)
            if (bytesRead >= 5 &&
                header[0] == 0x7B && header[1] == 0x5C && header[2] == 0x72 && header[3] == 0x74 && header[4] == 0x66)
            {
                result.MagicKind = FileMagicKind.Rtf;
                result.MimeType = "application/rtf";
                result.Notes = "Rich Text Format document.";
                return result;
            }

            // 8. ZIP-based Office Open XML (DOCX, XLSX, PPTX): PK\x03\x04
            if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
            {
                try
                {
                    if (stream.CanSeek)
                    {
                        stream.Position = 0;
                        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                        bool hasWord = zip.GetEntry("word/document.xml") != null;
                        stream.Position = initialPos;

                        if (hasWord)
                        {
                            result.MagicKind = FileMagicKind.Docx;
                            result.MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                            result.Notes = "Microsoft Word Document (DOCX).";
                            return result;
                        }
                    }
                }
                catch
                {
                    // Fall back if zip parse fails
                }

                // If extension hints at docx
                if (originalFileName?.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) == true)
                {
                    result.MagicKind = FileMagicKind.Docx;
                    result.MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                    result.Notes = "Microsoft Word Document (DOCX).";
                    return result;
                }
            }

            // 9. Legacy Compound Document (DOC): 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1
            if (bytesRead >= 8 &&
                header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0 &&
                header[4] == 0xA1 && header[5] == 0xB1 && header[6] == 0x1A && header[7] == 0xE1)
            {
                result.MagicKind = FileMagicKind.Doc;
                result.MimeType = "application/msword";
                result.Notes = "Legacy Microsoft Word Document (DOC).";
                return result;
            }

            // 10. Plain text check: inspect if header contains mostly printable ASCII / UTF-8
            bool isPrintable = true;
            for (int i = 0; i < bytesRead; i++)
            {
                byte b = header[i];
                if (b == 0x00 || (b < 0x09 && b != 0x0A && b != 0x0D))
                {
                    isPrintable = false;
                    break;
                }
            }

            if (isPrintable)
            {
                result.MagicKind = FileMagicKind.Text;
                result.MimeType = "text/plain";
                result.Notes = "Plain text document.";
                return result;
            }

            result.MagicKind = FileMagicKind.Unknown;
            result.MimeType = "application/octet-stream";
            result.Notes = "Unrecognized binary file format.";
            return result;
        }

        public Task<bool> ValidateIntegrityAsync(string filePath)
        {
            if (!File.Exists(filePath)) return Task.FromResult(false);
            try
            {
                var sniff = SniffFile(filePath);
                if (sniff.IsCorrupt || sniff.MagicKind == FileMagicKind.Unknown)
                    return Task.FromResult(false);

                // Simple size sanity check
                var fi = new FileInfo(filePath);
                bool valid = fi.Length > 0 && fi.Length <= 250 * 1024 * 1024; // 250 MB max
                return Task.FromResult(valid);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }
}
