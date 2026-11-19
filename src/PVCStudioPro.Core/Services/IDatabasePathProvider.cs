using System;

namespace PVCStudioPro.Core.Services
{
    public interface IDatabasePathProvider
    {
        bool IsTestMode { get; }
        string DatabasePath { get; }
        string ConfigDirectory { get; }
        string ExportDirectory { get; }
        event EventHandler<bool>? TestModeChanged;
        void SetTestMode(bool enabled);
        void ResetTestDatabase();
    }
}
