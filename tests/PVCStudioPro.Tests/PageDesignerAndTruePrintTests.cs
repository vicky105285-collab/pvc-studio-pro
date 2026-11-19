using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SkiaSharp;
using Xunit;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.Tests
{
    public class PageDesignerAndTruePrintTests
    {
        [Fact]
        public void PhysicalPageRenderService_RenderLayoutToBitmap_ComputesExactMillimeterPixels()
        {
            var renderService = new PhysicalPageRenderService();
            var layout = PageLayoutModel.CreateDefaultA4(); // 210 x 297 mm
            int dpi = 300;

            using var bmp = renderService.RenderLayoutToBitmap(layout, dpi);

            int expectedW = (int)Math.Round(210.0 * dpi / 25.4);
            int expectedH = (int)Math.Round(297.0 * dpi / 25.4);

            bmp.Should().NotBeNull();
            bmp.Width.Should().Be(expectedW);
            bmp.Height.Should().Be(expectedH);
        }

        [Fact]
        public void PhysicalPageRenderService_ExportLayoutToPdf_ProducesValidPdfDocument()
        {
            var renderService = new PhysicalPageRenderService();
            var layout = PageLayoutModel.CreateEpsonTray(); // 120 x 180 mm

            // Add test card item
            using var cardBmp = new SKBitmap(2022, 1275);
            layout.Items.Add(new PageLayoutItem
            {
                Label = "Test Front Card",
                Kind = LayoutItemKind.FrontCard,
                XPositionMm = 17.2,
                YPositionMm = 20.0,
                WidthMm = 85.60,
                HeightMm = 53.98,
                Bitmap = cardBmp
            });

            byte[] pdfBytes = renderService.ExportLayoutToPdf(layout);

            pdfBytes.Should().NotBeNull();
            pdfBytes.Length.Should().BeGreaterThan(500);
            
            // Check PDF header %PDF-
            string header = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
            header.Should().Be("%PDF-");
        }

        [Fact]
        public void PhysicalPageRenderService_SerializeAndDeserializeProject_PreservesCompleteModelAndBitmaps()
        {
            var renderService = new PhysicalPageRenderService();
            var original = PageLayoutModel.CreateDefaultA4();
            original.Title = "Audit Test Project";
            original.OffsetXMm = 0.5;
            original.OffsetYMm = -0.3;

            using var sampleBmp = new SKBitmap(100, 60);
            using (var canvas = new SKCanvas(sampleBmp))
            {
                canvas.Clear(SKColors.Blue);
            }

            original.Items.Add(new PageLayoutItem
            {
                Label = "Test Card",
                Kind = LayoutItemKind.FrontCard,
                XPositionMm = 25.4,
                YPositionMm = 50.8,
                WidthMm = 85.60,
                HeightMm = 53.98,
                Bitmap = sampleBmp
            });

            string json = renderService.SerializeProject(original);
            json.Should().NotBeNullOrWhiteSpace();

            var deserialized = renderService.DeserializeProject(json);
            deserialized.Title.Should().Be("Audit Test Project");
            deserialized.OffsetXMm.Should().Be(0.5);
            deserialized.OffsetYMm.Should().Be(-0.3);
            deserialized.Items.Should().HaveCount(1);

            var item = deserialized.Items[0];
            item.Label.Should().Be("Test Card");
            item.XPositionMm.Should().Be(25.4);
            item.YPositionMm.Should().Be(50.8);
            item.Bitmap.Should().NotBeNull();
            item.Bitmap!.Width.Should().Be(100);
            item.Bitmap!.Height.Should().Be(60);
        }

        [Fact]
        public async Task PhysicalPageRenderService_SaveAndLoadProjectFile_WorksRoundTrip()
        {
            var renderService = new PhysicalPageRenderService();
            var model = PageLayoutModel.CreateDefault4x6();
            model.Title = "Photo 4x6 Project";

            string tempFile = Path.Combine(Path.GetTempPath(), $"pvc_test_{Guid.NewGuid():N}.pvcproj");
            try
            {
                await renderService.SaveProjectAsync(model, tempFile);
                File.Exists(tempFile).Should().BeTrue();

                var loaded = await renderService.LoadProjectAsync(tempFile);
                loaded.Should().NotBeNull();
                loaded.Title.Should().Be("Photo 4x6 Project");
                loaded.PageWidthMm.Should().Be(101.6);
                loaded.PageHeightMm.Should().Be(152.4);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void PageLayoutModel_Presets_MatchPhysicalStandards()
        {
            var a4 = PageLayoutModel.CreateDefaultA4();
            a4.PageWidthMm.Should().Be(210.0);
            a4.PageHeightMm.Should().Be(297.0);

            var cr80 = PageLayoutModel.CreateDirectCr80();
            cr80.PageWidthMm.Should().Be(85.60);
            cr80.PageHeightMm.Should().Be(53.98);

            var photo = PageLayoutModel.CreateDefault4x6();
            photo.PageWidthMm.Should().Be(101.6);
            photo.PageHeightMm.Should().Be(152.4);
        }
    }
}
