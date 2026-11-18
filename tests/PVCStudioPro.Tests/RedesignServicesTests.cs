using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;
using Xunit;

namespace PVCStudioPro.Tests
{
    public class FakePrinterDiscoveryService : IPrinterDiscoveryService
    {
        public Task<IReadOnlyList<PrinterCapabilities>> GetInstalledPrintersAsync()
        {
            var list = new List<PrinterCapabilities>
            {
                new PrinterCapabilities { PrinterName = "EPSON L805 Series", DetectedCategory = "EPSON_PHOTO" },
                new PrinterCapabilities { PrinterName = "Canon G5070 Series", DetectedCategory = "CANON_PIXMA" }
            };
            return Task.FromResult<IReadOnlyList<PrinterCapabilities>>(list);
        }

        public PrinterCapabilities QueryPrinterCapabilities(string printerName, string portName = "")
        {
            return new PrinterCapabilities { PrinterName = printerName };
        }

        public bool ValidateMediaTrayMatch(PrinterCapabilities caps, string selectedTray)
        {
            return true;
        }
    }

    public class RedesignServicesTests : IDisposable
    {
        private readonly string _testDir;

        public RedesignServicesTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "PVCStudioPro_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public async Task BusinessProfileService_SavesAndLoadsCorrectly()
        {
            string configPath = Path.Combine(_testDir, "business_profile.json");
            var service = new BusinessProfileService(configPath);

            var profile = await service.GetProfileAsync();
            Assert.NotNull(profile);
            Assert.Contains("e-Sevai", profile.ShopName);

            profile.ShopName = "Karur Digital e-Sevai";
            profile.GstNumber = "33AAAAA0000A1Z5";
            profile.DefaultCardPrice = 75.00m;
            profile.ReceiptPaperSize = "80mm";

            await service.SaveProfileAsync(profile);

            var service2 = new BusinessProfileService(configPath);
            var loaded = await service2.GetProfileAsync();

            Assert.Equal("Karur Digital e-Sevai", loaded.ShopName);
            Assert.Equal("33AAAAA0000A1Z5", loaded.GstNumber);
            Assert.Equal(75.00m, loaded.DefaultCardPrice);
            Assert.Equal("80mm", loaded.ReceiptPaperSize);
        }

        [Fact]
        public async Task PrinterTrayService_InitializesDefaultProfilesAndDuplicates()
        {
            string profilesPath = Path.Combine(_testDir, "printer_profiles.json");
            var fakeDiscovery = new FakePrinterDiscoveryService();

            var service = new PrinterTrayService(fakeDiscovery, profilesPath);
            await service.InitializeAsync();

            var profiles = await service.GetAllProfilesAsync();
            Assert.NotEmpty(profiles);

            // Verify both aftermarket PVC tray and manufacturer CD attachment profiles exist
            Assert.Contains(profiles, p => p.AttachmentType == "AftermarketTray");
            Assert.Contains(profiles, p => p.AttachmentType == "ManufacturerAttachment");

            // Test duplicate
            var first = profiles.First();
            var copy = await service.DuplicateProfileAsync(first.ProfileId, first.ProfileName + " (Custom)");
            Assert.NotNull(copy);
            Assert.Equal(first.ProfileName + " (Custom)", copy.ProfileName);
            Assert.Equal(first.OffsetXMm, copy.OffsetXMm);
        }

        [Fact]
        public async Task BackupService_CreatesArchiveAndInspectsMetadata()
        {
            string dbFile = Path.Combine(_testDir, "pvc_ledger.db");
            var ledger = new JobLedgerService(dbFile);
            await ledger.InitializeDatabaseAsync();
            await ledger.RecordJobAsync(new PrintJobRecord
            {
                DocumentType = "AADHAAR",
                Quantity = 1,
                TotalAmount = 70.00m,
                PaymentMode = "CASH"
            });

            string configDir = Path.Combine(_testDir, "config");
            Directory.CreateDirectory(configDir);
            await File.WriteAllTextAsync(Path.Combine(configDir, "settings.json"), "{\"app\":\"pvc_studio\"}");

            var backupService = new BackupService(dbFile, configDir);
            string backupTargetDir = Path.Combine(_testDir, "Backups");
            Directory.CreateDirectory(backupTargetDir);

            var metadata = await backupService.CreateBackupAsync(backupTargetDir);
            Assert.True(File.Exists(metadata.FilePath));
            Assert.True(metadata.FileSizeBytes > 0);

            var preview = await backupService.PreviewRestoreAsync(metadata.FilePath);
            Assert.True(preview.CanSafelyRestore);
            Assert.Equal(1, preview.TotalJobsCount);
            Assert.Equal(70.00m, preview.TotalRecordedRevenue);
        }

        [Fact]
        public void BatchProcessingService_CalculatesSummaryAndEnqueueWorks()
        {
            var pdfService = new PdfService();
            var templateService = new TemplateService();
            var service = new BatchProcessingService(pdfService, templateService);

            var dummyFile = Path.Combine(_testDir, "card.pdf");
            File.WriteAllText(dummyFile, "%PDF-1.4 dummy content");

            service.EnqueueFile(dummyFile);
            var queue = service.GetQueue();

            Assert.Single(queue);
            Assert.Equal(Path.GetFileName(dummyFile), queue[0].FileName);

            var summary = service.GetSummary();
            Assert.Equal(1, summary.TotalItems);
        }
    }
}
