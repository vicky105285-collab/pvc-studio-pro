using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;
using Xunit;

namespace PVCStudioPro.Tests
{
    public class PdfServiceTests
    {
        private readonly string _vectorPdfPath;
        private readonly string _encryptedPdfPath;

        public PdfServiceTests()
        {
            string current = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(current) && !Directory.Exists(Path.Combine(current, "samples")))
            {
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }
            string root = Directory.Exists(Path.Combine(current, "samples")) 
                ? current 
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

            _vectorPdfPath = Path.Combine(root, "samples", "test_card_vector.pdf");
            _encryptedPdfPath = Path.Combine(root, "samples", "test_card_encrypted.pdf");
        }

        [Fact]
        public async Task InspectDocumentAsync_ShouldReadPageCountAndDimensions()
        {
            Assert.True(File.Exists(_vectorPdfPath), $"Sample fixture missing: {_vectorPdfPath}");

            var service = new PdfService();
            using var stream = File.OpenRead(_vectorPdfPath);

            var info = await service.InspectDocumentAsync(stream);

            info.PageCount.Should().BeGreaterThan(0);
            info.PageWidthMm.Should().BeApproximately(85.4, 2.0);
            info.PageHeightMm.Should().BeApproximately(54.0, 2.0);
            info.IsEncrypted.Should().BeFalse();
        }

        [Fact]
        public async Task InspectDocumentAsync_EncryptedPdf_ShouldDetectEncryption()
        {
            Assert.True(File.Exists(_encryptedPdfPath), $"Sample fixture missing: {_encryptedPdfPath}");

            var service = new PdfService();
            using var stream = File.OpenRead(_encryptedPdfPath);

            var info = await service.InspectDocumentAsync(stream);

            info.IsEncrypted.Should().BeTrue();
        }

        [Fact]
        public async Task RenderPageAsync_EncryptedPdfWithoutPassword_ShouldThrowPdfPasswordRequiredException()
        {
            Assert.True(File.Exists(_encryptedPdfPath), $"Sample fixture missing: {_encryptedPdfPath}");

            var service = new PdfService();
            using var stream = File.OpenRead(_encryptedPdfPath);

            // Attempting to render encrypted PDF without password must throw PdfPasswordRequiredException
            await Assert.ThrowsAsync<PdfPasswordRequiredException>(async () =>
            {
                await service.RenderPageAsync(stream, 0, dpi: 300, password: null);
            });
        }

        [Fact]
        public async Task InspectDocumentAsync_EnforcesMaxStreamSizeLimit()
        {
            var service = new PdfService();
            
            // Create a fake stream reporting Length > 150MB
            using var fakeLargeStream = new LargeDummyStream(160L * 1024 * 1024);

            await Assert.ThrowsAsync<PdfSecurityException>(async () =>
            {
                await service.InspectDocumentAsync(fakeLargeStream);
            });
        }

        [Fact]
        public async Task InspectDocumentAsync_HonorsCancellationToken()
        {
            Assert.True(File.Exists(_vectorPdfPath), $"Sample fixture missing: {_vectorPdfPath}");

            var service = new PdfService();
            using var stream = File.OpenRead(_vectorPdfPath);
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancelled

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await service.InspectDocumentAsync(stream, cancellationToken: cts.Token);
            });
        }

        [Fact]
        public async Task RenderPageAsync_ShouldRender600DpiCorrectPixelSize()
        {
            Assert.True(File.Exists(_vectorPdfPath), $"Sample fixture missing: {_vectorPdfPath}");

            var service = new PdfService();
            using var stream = File.OpenRead(_vectorPdfPath);

            using var bitmap = await service.RenderPageAsync(stream, 0, dpi: 600);

            bitmap.Should().NotBeNull();
            bitmap.Width.Should().BeInRange(2010, 2030);
            bitmap.Height.Should().BeInRange(1265, 1285);
        }

        [Fact]
        public async Task ExtractCardFacesAsync_ShouldCropToExactCr80Dimensions()
        {
            Assert.True(File.Exists(_vectorPdfPath), $"Sample fixture missing: {_vectorPdfPath}");

            var service = new PdfService();
            using var stream = File.OpenRead(_vectorPdfPath);

            var template = new CardTemplate
            {
                TemplateId = "TEST_CR80",
                DisplayName = "Test Template",
                DocumentType = "TEST",
                PhysicalCard = new PhysicalCardSpec
                {
                    WidthMm = 85.60,
                    HeightMm = 53.98
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 0, Y = 0, Width = 85.37, Height = 53.98 }
                    }
                }
            };

            var (front, back) = await service.ExtractCardFacesAsync(stream, template);

            front.Should().NotBeNull();
            front.Width.Should().Be(2022);
            front.Height.Should().Be(1275);

            back.Should().BeNull();

            front.Dispose();
        }

        // Helper dummy stream for testing size limits without allocating 160MB in RAM
        private class LargeDummyStream : Stream
        {
            private readonly long _length;
            private long _position;

            public LargeDummyStream(long length) => _length = length;
            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position { get => _position; set => _position = value; }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => 0;
            public override long Seek(long offset, SeekOrigin origin) => _position = offset;
            public override void SetLength(long value) { }
            public override void Write(byte[] buffer, int offset, int count) { }
        }
    }
}
