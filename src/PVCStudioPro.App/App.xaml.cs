using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PVCStudioPro.App.ViewModels;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.App
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();

            // Directory paths
            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PVCStudioPro");
            Directory.CreateDirectory(appDataDir);

            string dbPath = Path.Combine(appDataDir, "pvc_ledger.db");
            string configDir = Path.Combine(appDataDir, "config");
            Directory.CreateDirectory(configDir);

            // Core Hardware & Processing Services
            services.AddSingleton<IPdfService, PdfService>();
            services.AddSingleton<ITemplateService, TemplateService>();
            services.AddSingleton<IPrinterDiscoveryService, PrinterDiscoveryService>();
            services.AddSingleton<IPrintService, PrintService>();
            services.AddSingleton<ISecurityService, SecurityService>();
            services.AddSingleton<IVisionService, VisionService>();
            services.AddSingleton<IJobLedgerService>(sp => new JobLedgerService(dbPath));

            // Business & Management Services
            string profileConfigPath = Path.Combine(configDir, "business_profile.json");
            services.AddSingleton<IBusinessProfileService>(sp => new BusinessProfileService(profileConfigPath));

            string traysConfigPath = Path.Combine(configDir, "printer_profiles.json");
            services.AddSingleton<IPrinterTrayService>(sp => new PrinterTrayService(sp.GetRequiredService<IPrinterDiscoveryService>(), traysConfigPath));

            services.AddSingleton<IBackupService>(sp => new BackupService(dbPath, configDir));
            services.AddSingleton<IBatchProcessingService, BatchProcessingService>();

            // ViewModels (14 Core Workspaces)
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<CardPrintViewModel>();
            services.AddSingleton<PhotoStudioViewModel>();
            services.AddSingleton<DocumentProcessingViewModel>();
            services.AddSingleton<BatchProcessingViewModel>();
            services.AddSingleton<PrintCenterViewModel>();
            services.AddSingleton<PrinterTrayManagerViewModel>();
            services.AddSingleton<CalibrationViewModel>();
            services.AddSingleton<TemplateManagerViewModel>();
            services.AddSingleton<BusinessProfileViewModel>();
            services.AddSingleton<BusinessLedgerViewModel>();
            services.AddSingleton<ReportsAnalyticsViewModel>();
            services.AddSingleton<BackupRestoreViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<MainViewModel>();

            // Main Window
            services.AddSingleton<MainWindow>();

            ServiceProvider = services.BuildServiceProvider();

            // Asynchronously initialize database and configuration services
            _ = ServiceProvider.GetRequiredService<IJobLedgerService>().InitializeDatabaseAsync();
            _ = ServiceProvider.GetRequiredService<IPrinterTrayService>().InitializeAsync();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}
