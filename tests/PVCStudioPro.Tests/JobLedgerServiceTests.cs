using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services.Implementations;
using Xunit;

namespace PVCStudioPro.Tests
{
    public class JobLedgerServiceTests
    {
        [Fact]
        public async Task LedgerService_ShouldRecordJobAndCalculateDailySummary()
        {
            string tempDb = Path.Combine(Path.GetTempPath(), $"pvc_test_{Guid.NewGuid()}.db");
            try
            {
                var ledger = new JobLedgerService(tempDb);
                await ledger.InitializeDatabaseAsync();

                // Record 2 Aadhaar cards and 1 PAN card
                await ledger.RecordJobAsync(new PrintJobRecord
                {
                    DocumentType = "AADHAAR",
                    TotalAmount = 70.00m,
                    PaymentMode = "CASH"
                });

                await ledger.RecordJobAsync(new PrintJobRecord
                {
                    DocumentType = "AADHAAR",
                    TotalAmount = 70.00m,
                    PaymentMode = "UPI"
                });

                await ledger.RecordJobAsync(new PrintJobRecord
                {
                    DocumentType = "PAN",
                    TotalAmount = 80.00m,
                    PaymentMode = "CASH"
                });

                var recentJobs = await ledger.GetRecentJobsAsync(10);
                recentJobs.Should().HaveCount(3);

                var summary = await ledger.GetDailySummaryAsync(DateTime.UtcNow);
                summary.TotalCards.Should().Be(3);
                summary.TotalRevenue.Should().Be(220.00m);
                summary.AadhaarCount.Should().Be(2);
                summary.PanCount.Should().Be(1);
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                if (File.Exists(tempDb))
                {
                    try { File.Delete(tempDb); } catch { }
                }
            }
        }
    }
}
