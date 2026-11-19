using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PVCStudioPro.App;
using PVCStudioPro.App.Converters;
using PVCStudioPro.App.ViewModels;
using PVCStudioPro.App.Views;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;
using SkiaSharp;
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
                services.AddSingleton<IFeatureFlagsService, FeatureFlagsService>();

                services.AddTransient<MainViewModel>();
                services.AddTransient<ProductionWorkspaceViewModel>();
                services.AddTransient<DocumentDeskViewModel>();
                services.AddTransient<DocumentPageDesignerViewModel>();
                services.AddTransient<PrintPreviewApprovalViewModel>();
                services.AddTransient<PhotoStudioViewModel>();
                services.AddTransient<DocumentProcessingViewModel>();
                services.AddTransient<BatchProcessingViewModel>();
                services.AddTransient<PrinterTrayManagerViewModel>();
                services.AddTransient<CalibrationViewModel>();
                services.AddTransient<TemplateManagerViewModel>();
                services.AddTransient<BusinessProfileViewModel>();
                services.AddTransient<BusinessLedgerViewModel>();
                services.AddTransient<ReportsAnalyticsViewModel>();
                services.AddTransient<BackupRestoreViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<MainWindow>();

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
                        app.Resources.Add("FeatureVisibilityConverter", new PVCStudioPro.App.Converters.FeatureVisibilityConverter());
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
                string target = Path.Combine(current, "docs", "screens", "runs");
                if (Directory.Exists(Path.Combine(current, "docs")))
                {
                    Directory.CreateDirectory(target);
                    return target;
                }
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            string fallback = Path.GetFullPath(@"..\..\..\..\docs\screens\runs");
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

        private static SKBitmap CreateSampleCardBitmap(string title, string subtitle, SKColor headerColor)
        {
            var bmp = new SKBitmap(2022, 1275, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(SKColors.White);

            // Header banner
            using var headerPaint = new SKPaint { Color = headerColor, Style = SKPaintStyle.Fill };
            canvas.DrawRect(new SKRect(0, 0, 2022, 220), headerPaint);

            // Header text
            using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            using var titleFont = new SKFont(SKTypeface.Default, 64);
            canvas.DrawText(title, 80, 140, SKTextAlign.Left, titleFont, textPaint);

            // Photo placeholder
            using var photoPaint = new SKPaint { Color = new SKColor(220, 230, 245), Style = SKPaintStyle.Fill };
            using var borderPaint = new SKPaint { Color = new SKColor(180, 200, 225), Style = SKPaintStyle.Stroke, StrokeWidth = 4 };
            var photoRect = new SKRect(120, 320, 580, 880);
            canvas.DrawRect(photoRect, photoPaint);
            canvas.DrawRect(photoRect, borderPaint);

            // Citizen text lines
            using var linePaint = new SKPaint { Color = new SKColor(100, 116, 139), Style = SKPaintStyle.Fill };
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(650, 360, 1700, 410), 8), linePaint);
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(650, 460, 1500, 510), 8), linePaint);
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(650, 560, 1600, 610), 8), linePaint);

            // Aadhaar UID block
            using var numPaint = new SKPaint { Color = new SKColor(15, 23, 42), IsAntialias = true };
            using var numFont = new SKFont(SKTypeface.Default, 72);
            canvas.DrawText("XXXX XXXX 1234", 650, 780, SKTextAlign.Left, numFont, numPaint);

            // QR Code placeholder
            using var qrPaint = new SKPaint { Color = new SKColor(240, 240, 240), Style = SKPaintStyle.Fill };
            var qrRect = new SKRect(1500, 750, 1900, 1150);
            canvas.DrawRect(qrRect, qrPaint);
            canvas.DrawRect(qrRect, borderPaint);

            return bmp;
        }

        private static void PopulateProductionWorkspace(ProductionWorkspaceViewModel vm)
        {
            var frontBmp = CreateSampleCardBitmap("GOVERNMENT OF INDIA", "Unique Identification Authority", new SKColor(0, 103, 184));
            var backBmp = CreateSampleCardBitmap("UNIQUE IDENTIFICATION AUTHORITY OF INDIA", "Address & Verification", new SKColor(245, 158, 11));

            vm.FileName = "e-Aadhaar_Sample.pdf";
            vm.DocumentPath = @"C:\SampleDocuments\e-Aadhaar_Sample.pdf";
            vm.SelectedPrinter = "Epson EcoTank L8050";
            vm.DetectedDocTitle = "Aadhaar Card (CR80 Standard)";
            vm.DetectedBadge = "AADHAAR • AUTO-DETECTED";

            vm.FrontCardImage = frontBmp.ToBitmapSource();
            vm.BackCardImage = backBmp.ToBitmapSource();
            vm.HasFrontCard = true;
            vm.HasBackCard = true;
            vm.HasLoadedDocument = true;

            var layout = PageLayoutModel.CreateDefaultA4();
            layout.Items.Add(new PageLayoutItem
            {
                Kind = LayoutItemKind.FrontCard,
                Label = "Front",
                XPositionMm = 15.0,
                YPositionMm = 15.0,
                WidthMm = 85.60,
                HeightMm = 53.98
            });
            layout.Items.Add(new PageLayoutItem
            {
                Kind = LayoutItemKind.BackCard,
                Label = "Back",
                XPositionMm = 106.0,
                YPositionMm = 15.0,
                WidthMm = 85.60,
                HeightMm = 53.98
            });
            vm.ActivePageLayout = layout;
            vm.SheetPreviewImage = new PhysicalPageRenderService().RenderLayoutToBitmap(layout, 150).ToBitmapSource();
        }

        private static void PopulateBusinessLedger(BusinessLedgerViewModel vm)
        {
            var now = DateTime.UtcNow;
            var txs = new ObservableCollection<BusinessTransaction>
            {
                new BusinessTransaction { InvoiceNumber = "INV-202611-001", CustomerName = "Ramesh Kumar", DocumentType = "AADHAAR_CR80", NetTotalPaise = 5000, PaidPaise = 5000, BalancePaise = 0, Status = "PAID", CreatedAt = now.AddHours(-6) },
                new BusinessTransaction { InvoiceNumber = "INV-202611-002", CustomerName = "Priya Sharma", DocumentType = "PAN_CR80", NetTotalPaise = 5000, PaidPaise = 5000, BalancePaise = 0, Status = "PAID", CreatedAt = now.AddHours(-5) },
                new BusinessTransaction { InvoiceNumber = "INV-202611-003", CustomerName = "Karthik Subramanian", DocumentType = "DL_CR80", NetTotalPaise = 7000, PaidPaise = 7000, BalancePaise = 0, Status = "PAID", CreatedAt = now.AddHours(-4) },
                new BusinessTransaction { InvoiceNumber = "INV-202611-004", CustomerName = "Ananya Patel", DocumentType = "VOTER_CR80", NetTotalPaise = 5000, PaidPaise = 5000, BalancePaise = 0, Status = "PAID", CreatedAt = now.AddHours(-3) },
                new BusinessTransaction { InvoiceNumber = "INV-202611-005", CustomerName = "Suresh Raina", DocumentType = "PASSPORT_PHOTO_8UP", NetTotalPaise = 8000, PaidPaise = 8000, BalancePaise = 0, Status = "PAID", CreatedAt = now.AddHours(-2.5) },
                new BusinessTransaction { InvoiceNumber = "INV-202611-006", CustomerName = "Meenakshi Sundaram", DocumentType = "AYUSHMAN_CR80", NetTotalPaise = 5000, PaidPaise = 3000, BalancePaise = 2000, Status = "PARTIAL", CreatedAt = now.AddHours(-2) },
                new BusinessTransaction { InvoiceNumber = "INV-202611-007", CustomerName = "Deepak Verma", DocumentType = "STUDENT_ID", NetTotalPaise = 6000, PaidPaise = 0, BalancePaise = 6000, Status = "PENDING", CreatedAt = now.AddHours(-1) },
                new BusinessTransaction { InvoiceNumber = "INV-202611-008", CustomerName = "Lakshmi Narayanan", DocumentType = "REPRINT_PVC", NetTotalPaise = 5000, PaidPaise = 0, BalancePaise = 0, Status = "VOID", CreatedAt = now.AddMinutes(-30) },
            };

            vm.FilteredTransactions = txs;
            vm.SelectedTransaction = txs[0];
            vm.TodayRevenue = 330.00m;
            vm.TotalCollected = 330.00m;
            vm.CashRevenue = 150.00m;
            vm.UpiRevenue = 110.00m;
            vm.CardRevenue = 70.00m;
            vm.TotalRefunded = 50.00m;
            vm.TodayCards = 8;
        }

        [Fact]
        public void ScreenshotHarness_RendersAllPrimaryViewsAt100And150Dpi()
        {
            EnsureInitialized();

            RunInSTA(() =>
            {
                // 1. Production Workspace View (Populated State)
                var prodVm = _serviceProvider!.GetRequiredService<ProductionWorkspaceViewModel>();
                PopulateProductionWorkspace(prodVm);
                var prodView = new ProductionWorkspaceView { DataContext = prodVm };
                CaptureView(prodView, "ProductionWorkspaceView");

                // 2. Photo Studio View
                var photoVm = _serviceProvider!.GetRequiredService<PhotoStudioViewModel>();
                var photoView = new PhotoStudioView { DataContext = photoVm };
                CaptureView(photoView, "PhotoStudioView");

                // 3. Printer Tray Manager View
                var trayVm = _serviceProvider!.GetRequiredService<PrinterTrayManagerViewModel>();
                var trayView = new PrinterTrayManagerView { DataContext = trayVm };
                CaptureView(trayView, "PrinterTrayManagerView");

                // 4. Calibration View
                var calibVm = _serviceProvider!.GetRequiredService<CalibrationViewModel>();
                var calibView = new CalibrationView { DataContext = calibVm };
                CaptureView(calibView, "CalibrationView");

                // 5a. Business Ledger View (Populated State - 8 Transactions)
                var ledgerVm = _serviceProvider!.GetRequiredService<BusinessLedgerViewModel>();
                PopulateBusinessLedger(ledgerVm);
                var ledgerView = new BusinessLedgerView { DataContext = ledgerVm };
                CaptureView(ledgerView, "BusinessLedgerView");

                // 5b. Business Ledger View (Empty State)
                var emptyLedgerVm = _serviceProvider!.GetRequiredService<BusinessLedgerViewModel>();
                emptyLedgerVm.FilteredTransactions = new ObservableCollection<BusinessTransaction>();
                emptyLedgerVm.TodayRevenue = 0.00m;
                emptyLedgerVm.TotalCollected = 0.00m;
                var emptyLedgerView = new BusinessLedgerView { DataContext = emptyLedgerVm };
                CaptureView(emptyLedgerView, "BusinessLedgerView_Empty");

                // 6. Business Profile View
                var profileVm = _serviceProvider!.GetRequiredService<BusinessProfileViewModel>();
                var profileView = new BusinessProfileView { DataContext = profileVm };
                CaptureView(profileView, "BusinessProfileView");

                // 7. Document Desk View
                var docDeskVm = _serviceProvider!.GetRequiredService<DocumentDeskViewModel>();
                var docDeskView = new DocumentDeskView { DataContext = docDeskVm };
                CaptureView(docDeskView, "DocumentDeskView");

                // 8. Document Page Designer View
                var pageDesignerVm = _serviceProvider!.GetRequiredService<DocumentPageDesignerViewModel>();
                var pageDesignerView = new DocumentPageDesignerView { DataContext = pageDesignerVm };
                CaptureView(pageDesignerView, "DocumentPageDesignerView");

                // 9. Settings View
                var settingsVm = _serviceProvider!.GetRequiredService<SettingsViewModel>();
                var settingsView = new SettingsView { DataContext = settingsVm };
                CaptureView(settingsView, "SettingsView");

                // 10. Dialog / Drawer States
                // 10a. Production Workspace with Advanced Crop Drawer Open (Loaded image)
                var cropDrawerVm = _serviceProvider!.GetRequiredService<ProductionWorkspaceViewModel>();
                PopulateProductionWorkspace(cropDrawerVm);
                cropDrawerVm.IsCropDrawerOpen = true;
                cropDrawerVm.CropTargetFace = "Front Card";
                cropDrawerVm.CropSourceImage = CreateSampleCardBitmap("CROP SOURCE", "Sample Page", SKColors.DarkSlateBlue).ToBitmapSource();
                cropDrawerVm.CropX = 210;
                cropDrawerVm.CropY = 2595;
                cropDrawerVm.CropWidth = 1045;
                cropDrawerVm.CropHeight = 665;
                var cropDrawerView = new ProductionWorkspaceView { DataContext = cropDrawerVm };
                CaptureView(cropDrawerView, "ProductionWorkspaceView_CropDrawerOpen");

                // 10b. Production Workspace with Password Prompt Modal
                var pwdPromptVm = _serviceProvider!.GetRequiredService<ProductionWorkspaceViewModel>();
                pwdPromptVm.IsPasswordPromptVisible = true;
                pwdPromptVm.FileName = "e-Aadhaar_Protected.pdf";
                var pwdPromptView = new ProductionWorkspaceView { DataContext = pwdPromptVm };
                CaptureView(pwdPromptView, "ProductionWorkspaceView_PasswordPrompt");

                // 10c. Business Ledger with Payment Dialog Open
                var paymentLedgerVm = _serviceProvider!.GetRequiredService<BusinessLedgerViewModel>();
                PopulateBusinessLedger(paymentLedgerVm);
                paymentLedgerVm.IsPaymentDialogVisible = true;
                paymentLedgerVm.PaymentInputAmount = 50.00m;
                var paymentLedgerView = new BusinessLedgerView { DataContext = paymentLedgerVm };
                CaptureView(paymentLedgerView, "BusinessLedgerView_PaymentDialog");

                // 11. MainWindow with Full Navigation & Populated Workspace
                var mainVm = _serviceProvider!.GetRequiredService<MainViewModel>();
                PopulateProductionWorkspace(mainVm.ProductionVM);
                mainVm.CurrentView = mainVm.ProductionVM;
                var mainWindow = new MainWindow(mainVm);
                if (mainWindow.Content is FrameworkElement winRoot)
                {
                    CaptureView(winRoot, "MainWindow");
                }
                else
                {
                    CaptureView(mainWindow, "MainWindow");
                }
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
