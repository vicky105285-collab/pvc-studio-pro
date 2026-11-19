using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PVCStudioPro.App.ViewModels;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.App
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;
        private static string _logPath = string.Empty;
        private static readonly object _logLock = new object();

        public static void Log(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_logPath))
                {
                    string appData = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "PVCStudioPro");
                    Directory.CreateDirectory(appData);
                    _logPath = Path.Combine(appData, "app_trace.log");
                }

                lock (_logLock)
                {
                    File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\r\n");
                }
            }
            catch { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Log("=== PVC Studio Pro Starting ===");

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Log($"CRITICAL AppDomain UnhandledException: {args.ExceptionObject}");
            };

            bool isHandlingException = false;
            DispatcherUnhandledException += (s, args) =>
            {
                Log($"CRITICAL DispatcherUnhandledException: {args.Exception}");
                args.Handled = true;
                if (!isHandlingException)
                {
                    isHandlingException = true;
                    try
                    {
                        MessageBox.Show($"Startup Error:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}", "PVC Studio Pro");
                    }
                    catch { }
                    finally
                    {
                        isHandlingException = false;
                    }
                }
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                Log($"TaskScheduler UnobservedTaskException: {args.Exception}");
            };

            this.Exit += (s, args) =>
            {
                Log($"Application.Exit event fired with ExitCode: {args.ApplicationExitCode}");
            };

            base.OnStartup(e);
            Log("base.OnStartup executed.");

            this.ShutdownMode = ShutdownMode.OnMainWindowClose;

            try
            {
                string appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PVCStudioPro");
                string dbPath = Path.Combine(appDataDir, "pvc_ledger.db");
                string configDir = Path.Combine(appDataDir, "config");
                Directory.CreateDirectory(configDir);

                Log("Configuring DI container...");
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
                services.AddSingleton<IJobLedgerService>(sp => new JobLedgerService(sp.GetRequiredService<IDatabasePathProvider>().DatabasePath, sp.GetRequiredService<IDatabasePathProvider>()));

                string profileConfigPath = Path.Combine(configDir, "business_profile.json");
                services.AddSingleton<IBusinessProfileService>(sp => new BusinessProfileService(profileConfigPath));

                string traysConfigPath = Path.Combine(configDir, "printer_profiles.json");
                services.AddSingleton<IPrinterTrayService>(sp => new PrinterTrayService(sp.GetRequiredService<IPrinterDiscoveryService>(), traysConfigPath));

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

                services.AddSingleton<MainWindow>();

                ServiceProvider = services.BuildServiceProvider();
                Log("DI container built successfully.");

                try
                {
                    Log("Initializing JobLedger database...");
                    var ledger = ServiceProvider.GetRequiredService<IJobLedgerService>();
                    _ = ledger.InitializeDatabaseAsync();
                    Log("JobLedger database initialized.");
                }
                catch (Exception ex)
                {
                    Log($"Warning: JobLedger init error: {ex.Message}");
                }

                try
                {
                    Log("Initializing PrinterTray service...");
                    var trayService = ServiceProvider.GetRequiredService<IPrinterTrayService>();
                    _ = trayService.InitializeAsync();
                    Log("PrinterTray service initialized.");
                }
                catch (Exception ex)
                {
                    Log($"Warning: PrinterTray init error: {ex.Message}");
                }

                Log("Resolving MainWindow...");
                MainWindow mainWindow;
                try
                {
                    mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                    Log("MainWindow resolved successfully.");
                }
                catch (Exception ex)
                {
                    Log($"FATAL: Failed to resolve MainWindow: {ex}");
                    MessageBox.Show($"Failed to resolve MainWindow: {ex.Message}\n\n{ex.InnerException?.Message}", "PVC Studio Pro");
                    return;
                }

                this.MainWindow = mainWindow;
                mainWindow.SourceInitialized += (s, args) => Log("mainWindow.SourceInitialized fired.");
                mainWindow.Loaded += (s, args) => Log("mainWindow.Loaded fired.");
                mainWindow.Closed += (s, args) =>
                {
                    Log("MainWindow.Closed event fired.");
                };

                try
                {
                    Log("Calling mainWindow.Show()...");
                    mainWindow.Show();
                    Log("mainWindow.Show() completed successfully.");
                }
                catch (Exception ex)
                {
                    Log($"EXCEPTION in mainWindow.Show(): {ex}");
                    MessageBox.Show($"Error in Show: {ex.Message}\n\n{ex.StackTrace}", "PVC Studio Pro");
                }
            }
            catch (Exception ex)
            {
                Log($"FATAL in OnStartup try block: {ex}");
                MessageBox.Show($"Startup Exception:\n{ex.Message}\n\n{ex.StackTrace}", "PVC Studio Pro");
            }
        }
    }
}
