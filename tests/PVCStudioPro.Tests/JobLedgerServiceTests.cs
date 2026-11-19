using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
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
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                if (File.Exists(tempDb))
                {
                    try { File.Delete(tempDb); } catch { }
                }
            }
        }

        [Fact]
        public async Task LedgerService_ConcurrentWrites_ShouldNotThrowDatabaseLockedException()
        {
            string tempDb = Path.Combine(Path.GetTempPath(), $"pvc_concurrent_test_{Guid.NewGuid()}.db");
            try
            {
                var ledger = new JobLedgerService(tempDb);
                await ledger.InitializeDatabaseAsync();

                // Spin up 10 concurrent tasks writing jobs
                var writeTasks = Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
                {
                    await ledger.RecordJobAsync(new PrintJobRecord
                    {
                        DocumentType = (i % 2 == 0) ? "AADHAAR" : "VOTER",
                        TotalAmount = 50.00m,
                        PaymentMode = "UPI"
                    });
                }));

                await Task.WhenAll(writeTasks);

                var summary = await ledger.GetDailySummaryAsync(DateTime.UtcNow);
                summary.TotalCards.Should().Be(10);
                summary.TotalRevenue.Should().Be(500.00m);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                if (File.Exists(tempDb))
                {
                    try { File.Delete(tempDb); } catch { }
                }
            }
        }

        [Fact]
        public async Task LedgerService_ZeroCustomerIdentityData_SchemaMustNotContainPersonalIdentityColumns()
        {
            string tempDb = Path.Combine(Path.GetTempPath(), $"pvc_schema_test_{Guid.NewGuid()}.db");
            try
            {
                var ledger = new JobLedgerService(tempDb);
                await ledger.InitializeDatabaseAsync();

                using var connection = new SqliteConnection($"Data Source={tempDb}");
                await connection.OpenAsync();

                var columns = (await connection.QueryAsync<string>(
                    "SELECT name FROM pragma_table_info('print_jobs')")).Select(c => c.ToLowerInvariant()).ToList();

                // Verify strictly operational fields exist
                columns.Should().Contain(new[] { "job_id", "document_type", "total_amount", "payment_mode", "created_at" });

                // Verify ABSOLUTELY ZERO customer identity or PII columns exist
                columns.Should().NotContain("name");
                columns.Should().NotContain("customername");
                columns.Should().NotContain("aadhaar");
                columns.Should().NotContain("aadhaarnumber");
                columns.Should().NotContain("address");
                columns.Should().NotContain("phone");
                columns.Should().NotContain("photo");
                columns.Should().NotContain("documentdata");
            }
            finally
            {
                SqliteConnection.ClearAllPools();
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
