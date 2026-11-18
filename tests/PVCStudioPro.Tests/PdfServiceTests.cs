using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using PVCStudioPro.Core.Models;
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
            string baseDir = Directory.GetCurrentDirectory();
            string root = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            _vectorPdfPath = Path.Combine(root, "samples", "test_card_vector.pdf");
            _encryptedPdfPath = Path.Combine(root, "samples", "test_card_encrypted.pdf");
        }

        [Fact]
        public async Task InspectDocumentAsync_ShouldReadPageCountAndDimensions()
        {
            if (!File.Exists(_vectorPdfPath)) return;

            var service = new PdfService();
            using var stream = File.OpenRead(_vectorPdfPath);

            var info = await service.InspectDocumentAsync(stream);

            info.PageCount.Should().BeGreaterThan(0);
            info.PageWidthMm.Should().BeApproximately(210.0, 5.0);
            info.PageHeightMm.Should().BeApproximately(297.0, 5.0);
            info.IsEncrypted.Should().BeFalse();
        }

        [Fact]
        public async Task InspectDocumentAsync_EncryptedPdf_ShouldDetectEncryption()
        {
            if (!File.Exists(_encryptedPdfPath)) return;

            var service = new PdfService();
            using var stream = File.OpenRead(_encryptedPdfPath);

            var info = await service.InspectDocumentAsync(stream);

            info.IsEncrypted.Should().BeTrue();
        }

        [Fact]
        public async Task RenderPageAsync_ShouldRender600DpiCorrectPixelSize()
        {
            if (!File.Exists(_vectorPdfPath)) return;

            var service = new PdfService();
            using var stream = File.OpenRead(_vectorPdfPath);

            using var bitmap = await service.RenderPageAsync(stream, 0, dpi: 600);

            bitmap.Should().NotBeNull();
            bitmap.Width.Should().BeInRange(4900, 5100);
            bitmap.Height.Should().BeInRange(6900, 7150);
        }

        [Fact]
        public async Task ExtractCardFacesAsync_ShouldCropToExactCr80Dimensions()
        {
            if (!File.Exists(_vectorPdfPath)) return;

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
                        CropRectMm = new MillimeterRect { X = 20, Y = 180, Width = 85.6, Height = 54.0 }
                    },
                    Back = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 110, Y = 180, Width = 85.6, Height = 54.0 }
                    }
                }
            };

            var (front, back) = await service.ExtractCardFacesAsync(stream, template);

            front.Should().NotBeNull();
            front.Width.Should().Be(2022);
            front.Height.Should().Be(1275);

            back.Should().NotBeNull();
            back!.Width.Should().Be(2022);
            back.Height.Should().Be(1275);

            front.Dispose();
            back.Dispose();
        }
    }
}
