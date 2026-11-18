using System;
using System.IO;
using System.Windows;

namespace PVCStudioPro.App
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PVCStudioPro");
            Directory.CreateDirectory(appData);
            string logPath = Path.Combine(appData, "app_trace.log");

            void LogDirect(string msg)
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] [MAIN] {msg}\r\n"); } catch { }
            }

            LogDirect("Program.Main started.");

            try
            {
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    LogDirect($"CRITICAL AppDomain Exception in Program: {e.ExceptionObject}");
                };

                var app = new App();
                LogDirect("Calling app.InitializeComponent()...");
                app.InitializeComponent();
                LogDirect("app.InitializeComponent() completed. Calling app.Run()...");
                app.Run();
                LogDirect("app.Run() completed normally.");
            }
            catch (Exception ex)
            {
                LogDirect($"FATAL UNHANDLED IN MAIN: {ex}");
                MessageBox.Show($"Fatal Application Error:\n{ex.Message}\n\n{ex.StackTrace}", "PVC Studio Pro Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
