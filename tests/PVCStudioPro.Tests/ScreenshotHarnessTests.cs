using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PVCStudioPro.App.ViewModels;
using PVCStudioPro.App.Views;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;
using Xunit;

namespace PVCStudioPro.Tests
{
    public class ScreenshotHarnessTests
    {
        private static readonly object _initLock = new object();
        private static IServiceProvider? _serviceProvider;
        private static bool _appInitialized = false;

        private static void EnsureInitialized()
        {
            lock (_initLock)
            {
                if (_serviceProvider != null) return;

                string testDir = Path.Combine(Path.GetTempPath(), "PVCStudioPro_Harness_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(testDir);
                string dbPath = Path.Combine(testDir, "test.db");
                string configDir = Path.Combine(testDir, "config");
                Directory.CreateDirectory(configDir);

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
                services.AddSingleton<IDatabasePathProvider>(new DatabasePathProvider(dbPath));
                services.AddSingleton<IJobLedgerService, JobLedgerService>();
                services.AddSingleton<ITransactionPaymentService, TransactionPaymentService>();
                services.AddSingleton<IPrinterTrayService>(sp => new PrinterTrayService(sp.GetRequiredService<IPrinterDiscoveryService>(), configDir));
                services.AddSingleton<IBusinessProfileService>(new BusinessProfileService(configDir));
                services.AddSingleton<IBackupService>(new BackupService(dbPath, configDir));
                services.AddSingleton<IBatchProcessingService, BatchProcessingService>();
                services.AddSingleton<IPassportPhotoEngine, PassportPhotoEngine>();

                services.AddTransient<MainViewModel>();
                services.AddTransient<ProductionWorkspaceViewModel>();
                services.AddTransient<PhotoStudioViewModel>();
                services.AddTransient<PrinterTrayManagerViewModel>();
                services.AddTransient<CalibrationViewModel>();
                services.AddTransient<BusinessLedgerViewModel>();
                services.AddTransient<BusinessProfileViewModel>();
                services.AddTransient<DocumentDeskViewModel>();
                services.AddTransient<DocumentPageDesignerViewModel>();
                services.AddTransient<SettingsViewModel>();

                _serviceProvider = services.BuildServiceProvider();
            }
        }

        private static void RunInSTA(Action action)
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null && !_appInitialized)
                    {
                        var app = new Application();
                        // Load resource dictionaries
                        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Wpf.Ui;component/resources/wpf.ui.xaml") });
                        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Wpf.Ui;component/resources/theme/light.xaml") });
                        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PVCStudioPro.App;component/Themes/ThemeTokens.xaml", UriKind.RelativeOrAbsolute) });
                        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PVCStudioPro.App;component/Themes/LightTheme.xaml", UriKind.RelativeOrAbsolute) });
                        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PVCStudioPro.App;component/Resources/Theme/M3Tokens.xaml", UriKind.RelativeOrAbsolute) });
                        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PVCStudioPro.App;component/I18n/Strings.en.xaml", UriKind.RelativeOrAbsolute) });
                        app.Resources.Add("BooleanToVisibilityConverter", new System.Windows.Controls.BooleanToVisibilityConverter());
                        _appInitialized = true;
                    }

                    action();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (caught != null)
            {
                throw new AggregateException("STA thread execution failed", caught);
            }
        }

        private static string GetScreenshotsDirectory()
        {
            string current = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                string target = Path.Combine(current, "docs", "screens");
                if (Directory.Exists(Path.Combine(current, "docs")))
                {
                    Directory.CreateDirectory(target);
                    return target;
                }
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            string fallback = Path.GetFullPath(@"..\..\..\..\docs\screens");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        private static void CaptureView(FrameworkElement element, string viewName, double width = 1280, double height = 800)
        {
            string outputDir = GetScreenshotsDirectory();

            element.Width = width;
            element.Height = height;
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
            element.UpdateLayout();

            // 1. Render 100% DPI (96 DPI)
            var rtb100 = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            rtb100.Render(element);
            string file100 = Path.Combine(outputDir, $"{viewName}_100dpi.png");
            SavePng(rtb100, file100);

            // 2. Render 150% DPI (144 DPI)
            int pixelWidth150 = (int)Math.Round(width * 1.5);
            int pixelHeight150 = (int)Math.Round(height * 1.5);
            var rtb150 = new RenderTargetBitmap(pixelWidth150, pixelHeight150, 144, 144, PixelFormats.Pbgra32);
            rtb150.Render(element);
            string file150 = Path.Combine(outputDir, $"{viewName}_150dpi.png");
            SavePng(rtb150, file150);
        }

        private static void SavePng(BitmapSource source, string filePath)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = File.Create(filePath);
            encoder.Save(stream);
        }

        [Fact]
        public void ScreenshotHarness_RendersAllPrimaryViewsAt100And150Dpi()
        {
            EnsureInitialized();

            RunInSTA(() =>
            {
                // 1. Production Workspace View
                var prodVm = _serviceProvider!.GetRequiredService<ProductionWorkspaceViewModel>();
                var prodView = new ProductionWorkspaceView { DataContext = prodVm };
                CaptureView(prodView, "ProductionWorkspaceView");

                // 2. Photo Studio View
                var photoVm = _serviceProvider.GetRequiredService<PhotoStudioViewModel>();
                var photoView = new PhotoStudioView { DataContext = photoVm };
                CaptureView(photoView, "PhotoStudioView");

                // 3. Printer Tray Manager View
                var trayVm = _serviceProvider.GetRequiredService<PrinterTrayManagerViewModel>();
                var trayView = new PrinterTrayManagerView { DataContext = trayVm };
                CaptureView(trayView, "PrinterTrayManagerView");

                // 4. Calibration View
                var calibVm = _serviceProvider.GetRequiredService<CalibrationViewModel>();
                var calibView = new CalibrationView { DataContext = calibVm };
                CaptureView(calibView, "CalibrationView");

                // 5. Business Ledger View
                var ledgerVm = _serviceProvider.GetRequiredService<BusinessLedgerViewModel>();
                var ledgerView = new BusinessLedgerView { DataContext = ledgerVm };
                CaptureView(ledgerView, "BusinessLedgerView");

                // 6. Business Profile View
                var profileVm = _serviceProvider.GetRequiredService<BusinessProfileViewModel>();
                var profileView = new BusinessProfileView { DataContext = profileVm };
                CaptureView(profileView, "BusinessProfileView");

                // 7. Document Desk View
                var docDeskVm = _serviceProvider.GetRequiredService<DocumentDeskViewModel>();
                var docDeskView = new DocumentDeskView { DataContext = docDeskVm };
                CaptureView(docDeskView, "DocumentDeskView");

                // 8. Document Page Designer View
                var pageDesignerVm = _serviceProvider.GetRequiredService<DocumentPageDesignerViewModel>();
                var pageDesignerView = new DocumentPageDesignerView { DataContext = pageDesignerVm };
                CaptureView(pageDesignerView, "DocumentPageDesignerView");

                // 9. Settings View
                var settingsVm = _serviceProvider.GetRequiredService<SettingsViewModel>();
                var settingsView = new SettingsView { DataContext = settingsVm };
                CaptureView(settingsView, "SettingsView");

                // 10. Dialog / Drawer States
                // 10a. Production Workspace with Advanced Crop Drawer Open
                var cropDrawerVm = _serviceProvider.GetRequiredService<ProductionWorkspaceViewModel>();
                cropDrawerVm.IsCropDrawerOpen = true;
                cropDrawerVm.CropTargetFace = "Front Card";
                var cropDrawerView = new ProductionWorkspaceView { DataContext = cropDrawerVm };
                CaptureView(cropDrawerView, "ProductionWorkspaceView_CropDrawerOpen");

                // 10b. Production Workspace with Password Prompt Modal
                var pwdPromptVm = _serviceProvider.GetRequiredService<ProductionWorkspaceViewModel>();
                pwdPromptVm.IsPasswordPromptVisible = true;
                pwdPromptVm.FileName = "e-Aadhaar_Protected.pdf";
                var pwdPromptView = new ProductionWorkspaceView { DataContext = pwdPromptVm };
                CaptureView(pwdPromptView, "ProductionWorkspaceView_PasswordPrompt");

                // 10c. Business Ledger with Payment Dialog Open
                var paymentLedgerVm = _serviceProvider.GetRequiredService<BusinessLedgerViewModel>();
                paymentLedgerVm.IsPaymentDialogVisible = true;
                paymentLedgerVm.PaymentInputAmount = 50.00m;
                var paymentLedgerView = new BusinessLedgerView { DataContext = paymentLedgerVm };
                CaptureView(paymentLedgerView, "BusinessLedgerView_PaymentDialog");
            });

            // Assert that files were generated
            string outDir = GetScreenshotsDirectory();
            File.Exists(Path.Combine(outDir, "ProductionWorkspaceView_100dpi.png")).Should().BeTrue();
            File.Exists(Path.Combine(outDir, "ProductionWorkspaceView_150dpi.png")).Should().BeTrue();
            File.Exists(Path.Combine(outDir, "PrinterTrayManagerView_100dpi.png")).Should().BeTrue();
            File.Exists(Path.Combine(outDir, "BusinessLedgerView_100dpi.png")).Should().BeTrue();
        }
    }
}
