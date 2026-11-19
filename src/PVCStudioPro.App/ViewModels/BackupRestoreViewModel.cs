using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;

namespace PVCStudioPro.App.ViewModels
{
    public partial class BackupRestoreViewModel : ObservableObject
    {
        private readonly IBackupService _backupService;
        private readonly string _defaultBackupDirectory;

        [ObservableProperty]
        private string _backupLocation;

        [ObservableProperty]
        private BackupMetadata? _selectedBackup;

        [ObservableProperty]
        private RestorePreview? _activeRestorePreview;

        [ObservableProperty]
        private string _statusMessage = "Ready. Safeguard your business ledger and profile configurations.";

        public ObservableCollection<BackupMetadata> Backups { get; } = new ObservableCollection<BackupMetadata>();

        public ICommand CreateBackupCommand { get; }
        public ICommand BrowseLocationCommand { get; }
        public ICommand RefreshBackupsCommand { get; }
        public ICommand PreviewRestoreCommand { get; }
        public ICommand ExecuteRestoreCommand { get; }

        public BackupRestoreViewModel(IBackupService backupService)
        {
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _defaultBackupDirectory = Path.Combine(appData, "PVCStudioPro", "backups");
            Directory.CreateDirectory(_defaultBackupDirectory);
            _backupLocation = _defaultBackupDirectory;

            CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync);
            BrowseLocationCommand = new RelayCommand(BrowseLocation);
            RefreshBackupsCommand = new AsyncRelayCommand(RefreshBackupsAsync);
            PreviewRestoreCommand = new AsyncRelayCommand(PreviewRestoreSelectedAsync);
            ExecuteRestoreCommand = new AsyncRelayCommand(ExecuteRestoreAsync);

            _ = RefreshBackupsAsync();
        }

        private async Task CreateBackupAsync()
        {
            try
            {
                StatusMessage = "Creating archive...";
                var metadata = await _backupService.CreateBackupAsync(BackupLocation);
                Backups.Insert(0, metadata);
                StatusMessage = $"Backup created successfully: {metadata.FileName} ({metadata.FileSizeBytes / 1024} KB)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Backup failed: {ex.Message}";
            }
        }

        private async Task RefreshBackupsAsync()
        {
            try
            {
                var list = await _backupService.ListBackupsAsync(BackupLocation);
                Backups.Clear();
                foreach (var b in list)
                {
                    Backups.Add(b);
                }
                StatusMessage = $"Found {Backups.Count} backups in archive directory.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        private void BrowseLocation()
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Select Backup Storage Folder",
                InitialDirectory = BackupLocation
            };

            if (dlg.ShowDialog() == true)
            {
                BackupLocation = dlg.FolderName;
                _ = RefreshBackupsAsync();
            }
        }

        private async Task PreviewRestoreSelectedAsync()
        {
            if (SelectedBackup == null) return;

            try
            {
                ActiveRestorePreview = await _backupService.PreviewRestoreAsync(SelectedBackup.FilePath);
                StatusMessage = $"Restore preview loaded: {ActiveRestorePreview.TotalJobsCount} jobs, ₹ {ActiveRestorePreview.TotalRecordedRevenue:N2}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Preview error: {ex.Message}";
            }
        }

        private async Task ExecuteRestoreAsync()
        {
            if (SelectedBackup == null) return;

            try
            {
                StatusMessage = "Restoring database from safety archive...";
                bool success = await _backupService.RestoreBackupAsync(SelectedBackup.FilePath);
                if (success)
                {
                    StatusMessage = "Database restored successfully. Pre-restore safety snapshot created.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Restore failed: {ex.Message}";
            }
        }
    }
}
