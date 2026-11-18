using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface IJobLedgerService
    {
        Task InitializeDatabaseAsync();
        Task RecordJobAsync(PrintJobRecord record);
        Task<IReadOnlyList<PrintJobRecord>> GetRecentJobsAsync(int limit = 50);
        Task<DailySummary> GetDailySummaryAsync(DateTime date);
        Task<PrinterProfile> GetDefaultProfileAsync();
        Task SaveProfileAsync(PrinterProfile profile);
    }
}
