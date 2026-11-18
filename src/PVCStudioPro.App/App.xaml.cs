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

            // Core Services
            services.AddSingleton<IPdfService, PdfService>();
            services.AddSingleton<ITemplateService, TemplateService>();
            services.AddSingleton<IPrinterDiscoveryService, PrinterDiscoveryService>();
            services.AddSingleton<IPrintService, PrintService>();
            services.AddSingleton<ISecurityService, SecurityService>();
            services.AddSingleton<IVisionService, VisionService>();

            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PVCStudioPro",
                "pvc_ledger.db");
            services.AddSingleton<IJobLedgerService>(sp => new JobLedgerService(dbPath));

            // ViewModels
            services.AddSingleton<CardPrintViewModel>();
            services.AddSingleton<CalibrationViewModel>();
            services.AddSingleton<BusinessLedgerViewModel>();
            services.AddSingleton<MainViewModel>();

            // Main Window
            services.AddSingleton<MainWindow>();

            ServiceProvider = services.BuildServiceProvider();

            _ = ServiceProvider.GetRequiredService<IJobLedgerService>().InitializeDatabaseAsync();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}
