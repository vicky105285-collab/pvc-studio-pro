using System.Collections.Generic;

namespace PVCStudioPro.Core.Services
{
    public interface IFeatureFlagsService
    {
        bool IsFeatureEnabled(string featureId);
        string GetFeatureStatus(string featureId);
        IReadOnlyDictionary<string, string> GetAllFeatureStatuses();
    }
}
