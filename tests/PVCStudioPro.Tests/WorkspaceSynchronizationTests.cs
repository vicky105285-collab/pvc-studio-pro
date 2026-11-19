using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using SkiaSharp;
using Xunit;
using PVCStudioPro.App.ViewModels;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.Tests
{
    public class WorkspaceSynchronizationTests : IDisposable
    {
        private readonly string _testDirectory;

        public WorkspaceSynchronizationTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "PVCStudioPro_SyncTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private static SKBitmap CreateBitmap(int width, int height, SKColor color)
        {
            var bmp = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(color);
            return bmp;
        }

        private string CreateTestImageFile(string fileName, int width = 300, int height = 200)
        {
            string path = Path.Combine(_testDirectory, fileName);
            using var bmp = CreateBitmap(width, height, SKColors.CornflowerBlue);
            using var image = SKImage.FromBitmap(bmp);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(path);
            data.SaveTo(stream);
            return path;
        }

        [Fact]
        public async Task DocumentLoadedInSession_IsObservedInProduction_WithoutDiskReload()
        {
            var importService = new DocumentImportService();
            var pdfService = new PdfService();
            var templateService = new TemplateService();
            var detectionService = new DocumentTypeDetectionService(templateService);
            var cardDetection = new CardDetectionService();
            var cropService = new ManualCropService();
            var renderService = new PhysicalPageRenderService();
            var printerDiscovery = new PrinterDiscoveryService();
            var printService = new PrintService();
            var jobLedger = new JobLedgerService(Path.Combine(_testDirectory, "ledger.db"));

            using var sessionManager = new DocumentSessionManager(importService, pdfService, detectionService);

            var productionVM = new ProductionWorkspaceViewModel(
                importService,
                pdfService,
                detectionService,
                cardDetection,
                cropService,
                renderService,
                printerDiscovery,
                printService,
                jobLedger,
                templateService,
                sessionManager);

            string filePath = CreateTestImageFile("sync_test.png", 400, 250);

            // Open document in session manager (e.g. from Desk or Production)
            var session = await sessionManager.OpenDocumentAsync(filePath);

            // Production VM must observe the loaded session immediately
            productionVM.HasLoadedDocument.Should().BeTrue();
            productionVM.FileName.Should().Be("sync_test.png");
            productionVM.DocumentPath.Should().Be(filePath);

            // Simulate card extraction committed to session
            using (var lease = session.SourceAsset.AcquireLease())
            {
                var frontBmp = CreateBitmap(1011, 638, SKColors.Crimson);
                var backBmp = CreateBitmap(1011, 638, SKColors.DarkSlateBlue);

                var frontAsset = DerivedImageAsset.Create(session.SourceAsset.AssetId, 1, 300, "Front Card", frontBmp, lease);
                var backAsset = DerivedImageAsset.Create(session.SourceAsset.AssetId, 1, 300, "Back Card", backBmp, lease);

                session.SetCardAssets(frontAsset, backAsset);
            }

            // Sync must reflect extracted cards in Production workspace
            productionVM.SyncFromSession(session);
            productionVM.FrontCardImage.Should().NotBeNull();
            productionVM.BackCardImage.Should().NotBeNull();
            productionVM.FrontCardImage!.PixelWidth.Should().Be(1011);
            productionVM.BackCardImage!.PixelWidth.Should().Be(1011);
        }

        [Fact]
        public async Task ActiveDocumentSwitch_SynchronizesBothWorkspaces()
        {
            var importService = new DocumentImportService();
            var pdfService = new PdfService();
            var templateService = new TemplateService();
            var detectionService = new DocumentTypeDetectionService(templateService);
            var cardDetection = new CardDetectionService();
            var cropService = new ManualCropService();
            var renderService = new PhysicalPageRenderService();
            var printerDiscovery = new PrinterDiscoveryService();
            var printService = new PrintService();
            var jobLedger = new JobLedgerService(Path.Combine(_testDirectory, "ledger.db"));

            using var sessionManager = new DocumentSessionManager(importService, pdfService, detectionService);

            var productionVM = new ProductionWorkspaceViewModel(
                importService,
                pdfService,
                detectionService,
                cardDetection,
                cropService,
                renderService,
                printerDiscovery,
                printService,
                jobLedger,
                templateService,
                sessionManager);

            string fileA = CreateTestImageFile("docA.png", 200, 150);
            string fileB = CreateTestImageFile("docB.png", 350, 250);

            var sessionA = await sessionManager.OpenDocumentAsync(fileA);
            var sessionB = await sessionManager.OpenDocumentAsync(fileB);

            // Currently active is session B
            productionVM.FileName.Should().Be("docB.png");

            // Switch to session A
            sessionManager.SwitchActiveSession(sessionA.SessionId);
            productionVM.FileName.Should().Be("docA.png");
            productionVM.DocumentPath.Should().Be(fileA);

            // Switch back to session B
            sessionManager.SwitchActiveSession(sessionB.SessionId);
            productionVM.FileName.Should().Be("docB.png");
            productionVM.DocumentPath.Should().Be(fileB);
        }

        [Fact]
        public async Task CropStudioEdit_CommitsToActiveSession_AndSyncsToProduction()
        {
            var importService = new DocumentImportService();
            var pdfService = new PdfService();
            var templateService = new TemplateService();
            var detectionService = new DocumentTypeDetectionService(templateService);
            var cardDetection = new CardDetectionService();
            var cropService = new ManualCropService();
            var renderService = new PhysicalPageRenderService();
            var printerDiscovery = new PrinterDiscoveryService();
            var printService = new PrintService();
            var jobLedger = new JobLedgerService(Path.Combine(_testDirectory, "ledger.db"));

            using var sessionManager = new DocumentSessionManager(importService, pdfService, detectionService);

            var productionVM = new ProductionWorkspaceViewModel(
                importService,
                pdfService,
                detectionService,
                cardDetection,
                cropService,
                renderService,
                printerDiscovery,
                printService,
                jobLedger,
                templateService,
                sessionManager);

            var cropStudioVM = new CropStudioViewModel(cropService, cardDetection, sessionManager);

            string filePath = CreateTestImageFile("crop_source.png", 400, 300);
            var session = await sessionManager.OpenDocumentAsync(filePath);

            // Load session image into Crop Studio
            cropStudioVM.LoadFromSession(session, isFront: true);

            // Perform a crop
            cropStudioVM.CropX = 20;
            cropStudioVM.CropY = 20;
            cropStudioVM.CropWidth = 200;
            cropStudioVM.CropHeight = 120;
            cropStudioVM.PerformCrop();

            // Assign to front face
            cropStudioVM.AssignToFrontFace();

            // Session must have front card asset committed
            session.FrontCardAsset.Should().NotBeNull();
            session.FrontCardAsset!.TransformDescription.Should().Be("Crop Studio Front");
            session.ActiveRevision.Should().Be(1);

            // Production workspace reflects updated front card
            productionVM.SyncFromSession(session);
            productionVM.FrontCardImage.Should().NotBeNull();
        }
    }
}
