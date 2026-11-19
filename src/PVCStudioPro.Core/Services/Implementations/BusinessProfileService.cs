using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class BusinessProfileService : IBusinessProfileService
    {
        private readonly string _configFilePath;
        private readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);
        private BusinessProfile? _cachedProfile;

        public BusinessProfileService(string? customConfigPath = null)
        {
            if (!string.IsNullOrWhiteSpace(customConfigPath))
            {
                _configFilePath = customConfigPath;
            }
            else
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string baseDir = Path.Combine(appData, "PVCStudioPro", "config");
                Directory.CreateDirectory(baseDir);
                _configFilePath = Path.Combine(baseDir, "business_profile.json");
            }
        }

        public async Task<BusinessProfile> GetProfileAsync()
        {
            if (_cachedProfile != null)
                return _cachedProfile;

            await _fileLock.WaitAsync();
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = await File.ReadAllTextAsync(_configFilePath);
                    var profile = JsonSerializer.Deserialize<BusinessProfile>(json);
                    if (profile != null)
                    {
                        _cachedProfile = profile;
                        return _cachedProfile;
                    }
                }

                // Default initial profile
                _cachedProfile = new BusinessProfile();
                await SaveInternalAsync(_cachedProfile);
                return _cachedProfile;
            }
            catch
            {
                _cachedProfile = new BusinessProfile();
                return _cachedProfile;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task SaveProfileAsync(BusinessProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            profile.UpdatedAtUtc = DateTime.UtcNow;

            await _fileLock.WaitAsync();
            try
            {
                await SaveInternalAsync(profile);
                _cachedProfile = profile;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<BusinessProfile> ResetToDefaultsAsync()
        {
            var defaultProfile = new BusinessProfile();
            await SaveProfileAsync(defaultProfile);
            return defaultProfile;
        }

        private async Task SaveInternalAsync(BusinessProfile profile)
        {
            string? dir = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(profile, options);
            await File.WriteAllTextAsync(_configFilePath, json);
        }
    }
}
