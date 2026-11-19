using System.Collections.Generic;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IBackupService
    {
        Task<BackupMetadata> CreateBackupAsync(string targetDirectoryPath);
        Task<IReadOnlyList<BackupMetadata>> ListBackupsAsync(string backupDirectoryPath);
        Task<RestorePreview> PreviewRestoreAsync(string backupFilePath);
        Task<bool> RestoreBackupAsync(string backupFilePath);
    }
}
