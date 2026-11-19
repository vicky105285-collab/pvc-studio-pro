using System;
using System.IO;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class DatabasePathProvider : IDatabasePathProvider
    {
        private readonly string _productionBaseDir;
        private readonly string _testBaseDir;
        private bool _isTestMode;

        public event EventHandler<bool>? TestModeChanged;

        public DatabasePathProvider(string? customBaseDir = null)
        {
            _productionBaseDir = customBaseDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PVCStudioPro");

            _testBaseDir = Path.Combine(_productionBaseDir, "TestMode");

            Directory.CreateDirectory(_productionBaseDir);
            Directory.CreateDirectory(_testBaseDir);
        }

        public bool IsTestMode => _isTestMode;

        public string DatabasePath => _isTestMode
            ? Path.Combine(_testBaseDir, "pvc_studio_test.db")
            : Path.Combine(_productionBaseDir, "pvc_studio.db");

        public string ConfigDirectory => _isTestMode
            ? _testBaseDir
            : _productionBaseDir;

        public string ExportDirectory
        {
            get
            {
                if (_isTestMode)
                {
                    string testExports = Path.Combine(_testBaseDir, "Exports");
                    Directory.CreateDirectory(testExports);
                    return testExports;
                }

                string docsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string prodExports = Path.Combine(docsDir, "PVC Studio Pro", "Exports");
                Directory.CreateDirectory(prodExports);
                return prodExports;
            }
        }

        public void SetTestMode(bool enabled)
        {
            if (_isTestMode != enabled)
            {
                _isTestMode = enabled;
                TestModeChanged?.Invoke(this, enabled);
            }
        }

        public void ResetTestDatabase()
        {
            string activeDb = DatabasePath;
            if (!activeDb.Contains("TestMode", StringComparison.OrdinalIgnoreCase) || 
                !Path.GetFileName(activeDb).Equals("pvc_studio_test.db", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "FATAL SAFETY VIOLATION: Attempted to reset database when not running in TestMode sandbox!");
            }

            if (File.Exists(activeDb))
            {
                // Clear any SQLite pooling / locks if possible and delete
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                File.Delete(activeDb);
            }
        }
    }
}
