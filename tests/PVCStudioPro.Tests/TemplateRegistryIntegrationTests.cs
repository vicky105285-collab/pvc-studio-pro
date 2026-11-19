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
using PVCStudioPro.App.ViewModels;

namespace PVCStudioPro.Tests
{
    public class TemplateRegistryIntegrationTests
    {
        private readonly ITemplateService _templateService;

        public TemplateRegistryIntegrationTests()
        {
            _templateService = new TemplateService();
        }

        [Fact]
        public void TemplateRegistry_ShouldContainExactly29CalibratedIndianIdTemplates()
        {
            var allTemplates = _templateService.GetAllTemplates();
            allTemplates.Should().NotBeNull();
            allTemplates.Count.Should().Be(29, "Template catalog requires all 29 Indian ID templates to be registered");

            // Ensure unique template IDs
            var uniqueIds = allTemplates.Select(t => t.TemplateId).Distinct().ToList();
            uniqueIds.Count.Should().Be(29);

            // Ensure all 29 templates have valid physical CR80 card specifications
            foreach (var template in allTemplates)
            {
                template.Faces.Should().NotBeNull();
                template.Faces.Front.Should().NotBeNull();
                template.Faces.Front.CropRectMm.Width.Should().BeGreaterThan(40.0);
                template.Faces.Front.CropRectMm.Height.Should().BeGreaterThan(25.0);

                // Coordinates must be within A4 page boundaries (210 x 297 mm)
                template.Faces.Front.CropRectMm.X.Should().BeInRange(0.0, 210.0);
                template.Faces.Front.CropRectMm.Y.Should().BeInRange(0.0, 297.0);

                if (template.Faces.Back != null)
                {
                    template.Faces.Back.CropRectMm.Width.Should().BeGreaterThan(40.0);
                    template.Faces.Back.CropRectMm.Height.Should().BeGreaterThan(25.0);
                    template.Faces.Back.CropRectMm.X.Should().BeInRange(0.0, 210.0);
                    template.Faces.Back.CropRectMm.Y.Should().BeInRange(0.0, 297.0);
                }
            }
        }

        [Fact]
        public void GetCategorizedTemplates_ShouldCoverAllFiveMajorCategories()
        {
            var categories = _templateService.GetCategorizedTemplates();
            categories.Should().NotBeNull();
            categories.Count.Should().Be(5);

            var categoryKeys = categories.Select(c => c.CategoryKey).ToList();
            categoryKeys.Should().Contain(new[] { "IDENTITY", "HEALTHCARE", "LABOUR", "STATE_RATIONS", "TRANSPORT" });

            int totalGrouped = categories.Sum(c => c.Templates.Count);
            totalGrouped.Should().Be(29, "Every registered template must belong to a category group");

            // Ensure specific Indian ID templates are in their expected groups
            var healthGroup = categories.First(c => c.CategoryKey == "HEALTHCARE");
            healthGroup.Templates.Should().Contain(t => t.TemplateId == "ayushman_pmjay");
            healthGroup.Templates.Should().Contain(t => t.TemplateId == "abha_health_id");

            var stateGroup = categories.First(c => c.CategoryKey == "STATE_RATIONS");
            stateGroup.Templates.Should().Contain(t => t.TemplateId == "kerala_ration");
            stateGroup.Templates.Should().Contain(t => t.TemplateId == "tn_smart_ration");
            stateGroup.Templates.Should().Contain(t => t.TemplateId == "wb_ration");

            var transportGroup = categories.First(c => c.CategoryKey == "TRANSPORT");
            transportGroup.Templates.Should().Contain(t => t.TemplateId == "parivahan_dl");
            transportGroup.Templates.Should().Contain(t => t.TemplateId == "parivahan_rc");
        }

        [Theory]
        [InlineData("Unique Identification Authority of India Enrollment No आधार", "aadhaar_v2024")]
        [InlineData("Income Tax Department Permanent Account Number NSDL", "pan_nsdl")]
        [InlineData("ELECTION COMMISSION OF INDIA ELECTOR PHOTO IDENTITY CARD", "voter_epic_v2")]
        [InlineData("Ayushman Bharat Pradhan Mantri Jan Arogya Yojana National Health Authority", "ayushman_pmjay")]
        [InlineData("e-Shram Ministry of Labour and Employment Universal Account Number", "eshram_card")]
        [InlineData("Civil Supplies Kerala Ration Card Priority Household", "kerala_ration")]
        [InlineData("Union of India Driving Licence Form 7 Rule 16", "parivahan_dl")]
        public void MatchTemplateByText_ShouldIdentifyIndianIdTemplatesAccurately(string text, string expectedTemplateId)
        {
            var match = _templateService.MatchTemplateByText(text);
            match.Should().NotBeNull();
            match.IsSuccess.Should().BeTrue();
            match.Template.Should().NotBeNull();
            match.Template!.TemplateId.Should().Be(expectedTemplateId);
        }

        [Fact]
        public void ManualOverride_ApplyingTemplate_ShouldSnapToCalibratedCoordinates()
        {
            // Create simulated A4 raster bitmap (2480 x 3508 px @ 300 DPI)
            using var a4Bitmap = new SKBitmap(2480, 3508);
            using (var canvas = new SKCanvas(a4Bitmap))
            {
                canvas.Clear(SKColors.White);
                using var p = new SKPaint { Color = SKColors.SteelBlue };
                // Draw some shapes
                canvas.DrawRect(100, 100, 500, 300, p);
            }

            var importService = new DocumentImportService();
            var pdfService = new PdfService();
            var docTypeService = new DocumentTypeDetectionService(_templateService);
            var cardDetection = new CardDetectionService(_templateService, docTypeService);
            var manualCropService = new ManualCropService();
            var renderService = new PhysicalPageRenderService();
            var printerDiscovery = new PrinterDiscoveryService();
            var printService = new PrintService(renderService);
            var tempDb = Path.Combine(Path.GetTempPath(), $"test_ledger_{Guid.NewGuid():N}.db");
            var jobLedger = new JobLedgerService(tempDb);
            var layoutEngine = new PhysicalPageLayoutEngine();

            var vm = new ProductionWorkspaceViewModel(
                importService,
                pdfService,
                docTypeService,
                cardDetection,
                manualCropService,
                renderService,
                printerDiscovery,
                printService,
                jobLedger,
                _templateService,
                layoutEngine: layoutEngine);

            vm.TemplateOptions.Should().HaveCount(29);

            // Test applying Kerala Ration Card template
            var keralaTemplate = _templateService.GetTemplateById("kerala_ration");
            keralaTemplate.Should().NotBeNull();

            // Set cached bitmap manually for testing override
            var prop = typeof(ProductionWorkspaceViewModel).GetField("_cachedPageBitmap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            prop?.SetValue(vm, a4Bitmap);
            vm.HasLoadedDocument = true;

            vm.ApplyTemplateCoordinates(keralaTemplate!);

            vm.HasFrontCard.Should().BeTrue();
            vm.HasBackCard.Should().BeTrue();
            vm.DetectedDocTitle.Should().Be(keralaTemplate!.DisplayName);
            vm.DetectedBadge.Should().Contain("OVERRIDE");
            vm.StatusMessage.Should().Contain("Kerala Civil Supplies");
        }
    }
}
