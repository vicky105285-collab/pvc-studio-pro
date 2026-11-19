using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PVCStudioPro.App.ViewModels;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.Tests
{
    public class ThemeResourceTests
    {
        private static string GetM3TokensFilePath()
        {
            string current = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                string target = Path.Combine(current, "src", "PVCStudioPro.App", "Resources", "Theme", "M3Tokens.xaml");
                if (File.Exists(target))
                {
                    return target;
                }
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            return Path.GetFullPath(@"..\..\..\..\src\PVCStudioPro.App\Resources\Theme\M3Tokens.xaml");
        }

        private static ResourceDictionary LoadM3TokensDictionary()
        {
            string path = GetM3TokensFilePath();
            File.Exists(path).Should().BeTrue($"M3Tokens.xaml should exist at {path}");

            using var stream = File.OpenRead(path);
            var dict = (ResourceDictionary)XamlReader.Load(stream);
            dict.Should().NotBeNull();
            return dict;
        }

        [Fact]
        public void M3Tokens_File_LoadsAsValidResourceDictionary()
        {
            var dict = LoadM3TokensDictionary();
            dict.Count.Should().BeGreaterThan(15);
        }

        [Fact]
        public void M3Tokens_ContainsAllKeyPaletteColorsAndBrushes()
        {
            var dict = LoadM3TokensDictionary();

            // Colors
            dict.Contains("M3PrimaryColor").Should().BeTrue();
            dict.Contains("M3OnPrimaryColor").Should().BeTrue();
            dict.Contains("M3PrimaryContainerColor").Should().BeTrue();
            dict.Contains("M3SecondaryColor").Should().BeTrue();
            dict.Contains("M3TertiaryColor").Should().BeTrue();
            dict.Contains("M3ErrorColor").Should().BeTrue();
            dict.Contains("M3SuccessColor").Should().BeTrue();

            // Brushes
            dict.Contains("M3PrimaryBrush").Should().BeTrue();
            dict["M3PrimaryBrush"].Should().BeOfType<SolidColorBrush>();

            dict.Contains("M3PrimaryContainerBrush").Should().BeTrue();
            dict["M3PrimaryContainerBrush"].Should().BeOfType<SolidColorBrush>();

            dict.Contains("M3SecondaryBrush").Should().BeTrue();
            dict["M3SecondaryBrush"].Should().BeOfType<SolidColorBrush>();

            dict.Contains("M3TertiaryBrush").Should().BeTrue();
            dict["M3TertiaryBrush"].Should().BeOfType<SolidColorBrush>();

            dict.Contains("M3ErrorBrush").Should().BeTrue();
            dict["M3ErrorBrush"].Should().BeOfType<SolidColorBrush>();

            dict.Contains("M3SuccessBrush").Should().BeTrue();
            dict["M3SuccessBrush"].Should().BeOfType<SolidColorBrush>();

            dict.Contains("M3OutlineBrush").Should().BeTrue();
            dict.Contains("M3OutlineVariantBrush").Should().BeTrue();
        }

        [Fact]
        public void M3Tokens_ContainsCornerRadiiAndElevationDropShadows()
        {
            var dict = LoadM3TokensDictionary();

            // Radii
            dict.Contains("M3RadiusSmall").Should().BeTrue();
            ((CornerRadius)dict["M3RadiusSmall"]!).TopLeft.Should().Be(4);

            dict.Contains("M3RadiusMedium").Should().BeTrue();
            ((CornerRadius)dict["M3RadiusMedium"]!).TopLeft.Should().Be(8);

            dict.Contains("M3RadiusLarge").Should().BeTrue();
            ((CornerRadius)dict["M3RadiusLarge"]!).TopLeft.Should().Be(12);

            // Elevation Effects
            dict.Contains("M3Elevation1").Should().BeTrue();
            dict["M3Elevation1"].Should().BeOfType<DropShadowEffect>();

            dict.Contains("M3Elevation2").Should().BeTrue();
            dict["M3Elevation2"].Should().BeOfType<DropShadowEffect>();

            dict.Contains("M3Elevation3").Should().BeTrue();
            dict["M3Elevation3"].Should().BeOfType<DropShadowEffect>();
        }

        [Fact]
        public void M3Tokens_ContainsCardAndStatusPillStyles()
        {
            var dict = LoadM3TokensDictionary();

            dict.Contains("M3ElevatedCardStyle").Should().BeTrue();
            dict["M3ElevatedCardStyle"].Should().BeOfType<Style>();
            ((Style)dict["M3ElevatedCardStyle"]!).TargetType.Should().Be(typeof(System.Windows.Controls.Border));

            dict.Contains("M3StatusPillStyle").Should().BeTrue();
            dict["M3StatusPillStyle"].Should().BeOfType<Style>();
            ((Style)dict["M3StatusPillStyle"]!).TargetType.Should().Be(typeof(System.Windows.Controls.Border));
        }

        [Fact]
        public void MainViewModel_ExposesWorkingKeyboardAccelerators()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "PVCStudioPro_ThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string dbPath = Path.Combine(testDir, "test.db");
            string configDir = Path.Combine(testDir, "config");
            Directory.CreateDirectory(configDir);

            try
            {
                var services = new ServiceCollection();

                services.AddSingleton<IPdfService, PdfService>();
                services.AddSingleton<IPdfDocumentService, PdfService>();
                services.AddSingleton<IDocumentImportService, DocumentImportService>();
                services.AddSingleton<IDocumentSessionManager, DocumentSessionManager>();
                services.AddSingleton<IOfficeDocumentService, OfficeDocumentService>();
                services.AddSingleton<ITemplateService, TemplateService>();
                services.AddSingleton<IDocumentTypeDetectionService, DocumentTypeDetectionService>();
                services.AddSingleton<CardDetectionService>();
                services.AddSingleton<ICardDetectionService>(sp => sp.GetRequiredService<CardDetectionService>());
                services.AddSingleton<ICardDetectionEngine>(sp => sp.GetRequiredService<CardDetectionService>());
                services.AddSingleton<IManualCropService, ManualCropService>();
                services.AddSingleton<IPrintPreviewService, PrintPreviewService>();
                services.AddSingleton<IPhysicalPageRenderService, PhysicalPageRenderService>();
                services.AddSingleton<IPhysicalPageLayoutEngine, PhysicalPageLayoutEngine>();
                services.AddSingleton<IPrinterDiscoveryService, PrinterDiscoveryService>();
                services.AddSingleton<IPrintService, PrintService>();
                services.AddSingleton<ISecurityService, SecurityService>();
                services.AddSingleton<IVisionService, VisionService>();
                services.AddSingleton<IPassportPhotoEngine, PassportPhotoEngine>();
                services.AddSingleton<IDatabasePathProvider, DatabasePathProvider>();
                services.AddSingleton<ITransactionPaymentService, TransactionPaymentService>();
                services.AddSingleton<IJobLedgerService>(sp => new JobLedgerService(dbPath, sp.GetRequiredService<IDatabasePathProvider>()));
                services.AddSingleton<IBusinessProfileService>(sp => new BusinessProfileService(Path.Combine(configDir, "business_profile.json")));
                services.AddSingleton<IPrinterTrayService>(sp => new PrinterTrayService(sp.GetRequiredService<IPrinterDiscoveryService>(), Path.Combine(configDir, "printer_profiles.json")));
                services.AddSingleton<IBackupService>(sp => new BackupService(dbPath, configDir));
                services.AddSingleton<IBatchProcessingService, BatchProcessingService>();
                services.AddSingleton<IFeatureFlagsService, FeatureFlagsService>();

                services.AddSingleton<ProductionWorkspaceViewModel>();
                services.AddSingleton<DocumentDeskViewModel>();
                services.AddSingleton<DocumentPageDesignerViewModel>();
                services.AddSingleton<PrintPreviewApprovalViewModel>();
                services.AddSingleton<PhotoStudioViewModel>();
                services.AddSingleton<DocumentProcessingViewModel>();
                services.AddSingleton<BatchProcessingViewModel>();
                services.AddSingleton<PrinterTrayManagerViewModel>();
                services.AddSingleton<CalibrationViewModel>();
                services.AddSingleton<TemplateManagerViewModel>();
                services.AddSingleton<BusinessProfileViewModel>();
                services.AddSingleton<BusinessLedgerViewModel>();
                services.AddSingleton<ReportsAnalyticsViewModel>();
                services.AddSingleton<BackupRestoreViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<MainViewModel>();

                var sp = services.BuildServiceProvider();
                var mainVm = sp.GetRequiredService<MainViewModel>();

                mainVm.QuickOpenDocumentCommand.Should().NotBeNull();
                mainVm.QuickPrintCommand.Should().NotBeNull();
                mainVm.QuickExportPdfCommand.Should().NotBeNull();
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }

        [Fact]
        public void AllView_XamlFiles_ContainNoPropertyToStyleMismatches()
        {
            string current = AppContext.BaseDirectory;
            string appDir = string.Empty;
            while (!string.IsNullOrEmpty(current))
            {
                string target = Path.Combine(current, "src", "PVCStudioPro.App");
                if (Directory.Exists(target))
                {
                    appDir = target;
                    break;
                }
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            appDir.Should().NotBeEmpty("App directory must be located");

            var xamlFiles = Directory.GetFiles(appDir, "*.xaml", SearchOption.AllDirectories);
            var styles = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var styleRegex = new System.Text.RegularExpressions.Regex(@"<Style\s+[^>]*x:Key=""([^""]+)""", System.Text.RegularExpressions.RegexOptions.Compiled);
            foreach (var file in xamlFiles)
            {
                string text = File.ReadAllText(file);
                foreach (System.Text.RegularExpressions.Match match in styleRegex.Matches(text))
                {
                    styles.Add(match.Groups[1].Value);
                }
            }

            var propRegex = new System.Text.RegularExpressions.Regex(@"(Background|Foreground|BorderBrush|Fill|Stroke)=""\{(DynamicResource|StaticResource)\s+([^}]+)\}""", System.Text.RegularExpressions.RegexOptions.Compiled);
            var violations = new System.Collections.Generic.List<string>();

            foreach (var file in xamlFiles)
            {
                string text = File.ReadAllText(file);
                foreach (System.Text.RegularExpressions.Match match in propRegex.Matches(text))
                {
                    string prop = match.Groups[1].Value;
                    string key = match.Groups[3].Value.Trim();
                    if (styles.Contains(key))
                    {
                        violations.Add($"{Path.GetFileName(file)}: Property '{prop}' is set to Style resource '{key}' instead of a Brush or Style property");
                    }
                }
            }

            violations.Should().BeEmpty();
        }

        [Fact]
        public void StringsTa_LoadsAndContainsAllExpectedTamilKeys()
        {
            string current = AppContext.BaseDirectory;
            string taPath = string.Empty;
            while (!string.IsNullOrEmpty(current))
            {
                string target = Path.Combine(current, "src", "PVCStudioPro.App", "I18n", "Strings.ta.xaml");
                if (File.Exists(target))
                {
                    taPath = target;
                    break;
                }
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            File.Exists(taPath).Should().BeTrue("Strings.ta.xaml must exist");

            using var stream = File.OpenRead(taPath);
            var dict = (ResourceDictionary)XamlReader.Load(stream);

            dict.Should().NotBeNull();
            dict.Contains("AppTitle").Should().BeTrue();
            dict.Contains("NavProductionStudio").Should().BeTrue();
            dict.Contains("ChkMaskAadhaar").Should().BeTrue();
            dict["ChkMaskAadhaar"].ToString().Should().Contain("முதல் 8 எண்களை மறை");
        }
    }
}
