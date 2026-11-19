using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IBusinessProfileService
    {
        Task<BusinessProfile> GetProfileAsync();
        Task SaveProfileAsync(BusinessProfile profile);
        Task<BusinessProfile> ResetToDefaultsAsync();
    }
}
