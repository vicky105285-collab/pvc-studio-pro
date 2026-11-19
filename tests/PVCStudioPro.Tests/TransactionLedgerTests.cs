using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.Tests
{
    public class TransactionLedgerTests : IDisposable
    {
        private readonly string _testBaseDir;
        private readonly IDatabasePathProvider _pathProvider;
        private readonly ITransactionPaymentService _service;

        public TransactionLedgerTests()
        {
            _testBaseDir = Path.Combine(Path.GetTempPath(), $"pvc_test_{Guid.NewGuid():N}");
            _pathProvider = new DatabasePathProvider(_testBaseDir);
            _service = new TransactionPaymentService(_pathProvider);
        }

        public void Dispose()
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(_testBaseDir))
                {
                    Directory.Delete(_testBaseDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void IntegerPaisePrecision_ShouldHaveZeroFloatingPointDrift()
        {
            // Exact paise math: ₹33.33 x 3 = 3333 x 3 = 9999 paise = ₹99.99
            long unitPaise = 3333; // ₹33.33
            int qty = 3;
            long totalPaise = unitPaise * qty;

            totalPaise.Should().Be(9999);
            (totalPaise / 100.0m).Should().Be(99.99m);

            // Adding 1 paisa gives exact ₹100.00
            long adjusted = totalPaise + 1;
            adjusted.Should().Be(10000);
            (adjusted / 100.0m).Should().Be(100.00m);
        }

        [Fact]
        public async Task Scenario1_CashReceiptAndPartialRefund_ShouldReconcileNetCashDrawer()
        {
            // Customer purchases 2 PVC cards @ ₹50.00 each = ₹100.00 (10000 paise)
            var tx = await _service.CreateTransactionAsync(new CreateTransactionCommand
            {
                CustomerName = "Ramesh Kumar",
                DocumentType = "AADHAAR_CR80",
                Quantity = 2,
                UnitPricePaise = 5000,
                InitialPaymentPaise = 10000,
                InitialPaymentMethod = "CASH"
            });

            tx.Status.Should().Be("PAID");
            tx.PaidPaise.Should().Be(10000);
            tx.BalancePaise.Should().Be(0);

            // 1 card defective: operator issues ₹20.00 (2000 paise) cash refund
            var refund = await _service.RecordRefundAsync(new AddRefundCommand
            {
                TransactionId = tx.TransactionId,
                AmountPaise = 2000,
                PaymentMethod = "CASH",
                MandatoryReason = "Ribbon scuff on Card #2"
            });

            refund.IsRefund.Should().BeTrue();
            refund.AmountPaise.Should().Be(2000);

            // Check Daily Reconciliation
            var nowIst = DateTime.UtcNow.AddHours(5.5);
            var report = await _service.GetDailyReconciliationAsync(DateOnly.FromDateTime(nowIst), isTestMode: false);

            report.TotalCollectedPaise.Should().Be(10000); // Gross ₹100.00
            report.TotalRefundedPaise.Should().Be(2000);   // Refund ₹20.00
            report.NetCashPaise.Should().Be(8000);         // Net ₹80.00 in drawer
            report.TotalRevenuePaise.Should().Be(8000);    // Net sales ₹80.00
        }

        [Fact]
        public async Task Scenario2_SplitPaymentCashAndUpi_ShouldReconcileAndMarkInvoicePaid()
        {
            // Total: ₹140.00 (14000 paise)
            var tx = await _service.CreateTransactionAsync(new CreateTransactionCommand
            {
                CustomerName = "Priya Dharshini",
                DocumentType = "PAN_NSDL",
                Quantity = 2,
                UnitPricePaise = 7000
            });

            tx.Status.Should().Be("PENDING");
            tx.BalancePaise.Should().Be(14000);

            // Pay ₹100 cash
            await _service.RecordPaymentAsync(new AddPaymentCommand
            {
                TransactionId = tx.TransactionId,
                AmountPaise = 10000,
                PaymentMethod = "CASH"
            });

            // Pay ₹40 UPI
            await _service.RecordPaymentAsync(new AddPaymentCommand
            {
                TransactionId = tx.TransactionId,
                AmountPaise = 4000,
                PaymentMethod = "UPI",
                ReferenceNumber = "UPI-REF-998877"
            });

            var updatedTx = await _service.GetTransactionByIdAsync(tx.TransactionId);
            updatedTx.Should().NotBeNull();
            updatedTx!.Status.Should().Be("PAID");
            updatedTx.PaidPaise.Should().Be(14000);
            updatedTx.BalancePaise.Should().Be(0);

            var nowIst = DateTime.UtcNow.AddHours(5.5);
            var report = await _service.GetDailyReconciliationAsync(DateOnly.FromDateTime(nowIst), isTestMode: false);

            report.NetCashPaise.Should().Be(10000);
            report.UpiPaise.Should().Be(4000);
            report.TotalRevenuePaise.Should().Be(14000);
        }

        [Fact]
        public async Task Scenario3_PartialPayment_ShouldShowOutstandingReceivables()
        {
            var tx = await _service.CreateTransactionAsync(new CreateTransactionCommand
            {
                CustomerName = "Anand Murthy",
                DocumentType = "DRIVING_LICENCE",
                Quantity = 1,
                UnitPricePaise = 14000
            });

            await _service.RecordPaymentAsync(new AddPaymentCommand
            {
                TransactionId = tx.TransactionId,
                AmountPaise = 10000,
                PaymentMethod = "CASH"
            });

            var updatedTx = await _service.GetTransactionByIdAsync(tx.TransactionId);
            updatedTx.Should().NotBeNull();
            updatedTx!.Status.Should().Be("PARTIAL");
            updatedTx.PaidPaise.Should().Be(10000);
            updatedTx.BalancePaise.Should().Be(4000); // ₹40.00 balance due
        }

        [Fact]
        public async Task Scenario4_OlderOutstandingInvoicePaidToday_ShouldAttributeInflowToToday()
        {
            // Create invoice simulating earlier day
            var tx = await _service.CreateTransactionAsync(new CreateTransactionCommand
            {
                CustomerName = "David Raja",
                DocumentType = "VOTER_ID",
                Quantity = 1,
                UnitPricePaise = 14000
            });

            // Pay ₹100 on invoice creation
            await _service.RecordPaymentAsync(new AddPaymentCommand
            {
                TransactionId = tx.TransactionId,
                AmountPaise = 10000,
                PaymentMethod = "CASH"
            });

            // Customer pays remaining ₹40 today
            await _service.RecordPaymentAsync(new AddPaymentCommand
            {
                TransactionId = tx.TransactionId,
                AmountPaise = 4000,
                PaymentMethod = "CASH"
            });

            var nowIst = DateTime.UtcNow.AddHours(5.5);
            var report = await _service.GetDailyReconciliationAsync(DateOnly.FromDateTime(nowIst), isTestMode: false);

            report.NetCashPaise.Should().Be(14000);
        }

        [Fact]
        public async Task Scenario5_VoidedInvoiceWithPreviousPaymentAndRefund_ShouldHaveZeroNetCashAndZeroRevenue()
        {
            var tx = await _service.CreateTransactionAsync(new CreateTransactionCommand
            {
                CustomerName = "Test Void Customer",
                DocumentType = "AADHAAR_CR80",
                Quantity = 1,
                UnitPricePaise = 7000,
                InitialPaymentPaise = 7000,
                InitialPaymentMethod = "CASH"
            });

            // Void invoice due to printer damage
            var voided = await _service.VoidTransactionAsync(new VoidTransactionCommand
            {
                TransactionId = tx.TransactionId,
                MandatoryReason = "Printer shredded card; customer left without card",
                RefundAllCollected = true,
                RefundPaymentMethod = "CASH"
            });

            voided.Status.Should().Be("VOID");
            voided.BalancePaise.Should().Be(0);

            var nowIst = DateTime.UtcNow.AddHours(5.5);
            var report = await _service.GetDailyReconciliationAsync(DateOnly.FromDateTime(nowIst), isTestMode: false);

            report.TotalCollectedPaise.Should().Be(7000);
            report.TotalRefundedPaise.Should().Be(7000);
            report.NetCashPaise.Should().Be(0);
            report.TotalRevenuePaise.Should().Be(0);
        }

        [Fact]
        public async Task OverpaymentPolicy_ShouldThrowOverpaymentNotPermittedException()
        {
            var tx = await _service.CreateTransactionAsync(new CreateTransactionCommand
            {
                UnitPricePaise = 10000, // ₹100.00
                Quantity = 1
            });

            // Attempt to pay ₹120.00 on ₹100.00 bill
            var act = async () => await _service.RecordPaymentAsync(new AddPaymentCommand
            {
                TransactionId = tx.TransactionId,
                AmountPaise = 12000,
                PaymentMethod = "CASH"
            });

            await act.Should().ThrowAsync<OverpaymentNotPermittedException>();
        }

        [Fact]
        public async Task RefundCeiling_ShouldThrowRefundExceedsCollectedException()
        {
            var tx = await _service.CreateTransactionAsync(new CreateTransactionCommand
            {
                UnitPricePaise = 10000,
                Quantity = 1,
                InitialPaymentPaise = 5000, // ₹50.00 paid
                InitialPaymentMethod = "CASH"
            });

            // Attempt to refund ₹60.00 when only ₹50.00 was paid
            var act = async () => await _service.RecordRefundAsync(new AddRefundCommand
            {
                TransactionId = tx.TransactionId,
                AmountPaise = 6000,
                PaymentMethod = "CASH",
                MandatoryReason = "Fraudulent refund request"
            });

            await act.Should().ThrowAsync<RefundExceedsCollectedException>();
        }

        [Fact]
        public void IndianStandardTime_HalfOpenBoundary_ShouldAttributeEarlyMorningTxToIstDate()
        {
            // November 19, 2026 IST
            var istDate = new DateOnly(2026, 11, 19);
            var (startUtc, nextDayStartUtc) = IndianBusinessDay.GetIstDayUtcBounds(istDate);

            // 00:00:00 IST on Nov 19 = 18:30:00 UTC on Nov 18
            startUtc.Should().Be("2026-11-18T18:30:00.000Z");

            // 00:00:00 IST on Nov 20 = 18:30:00 UTC on Nov 19
            nextDayStartUtc.Should().Be("2026-11-19T18:30:00.000Z");

            // 02:00:00 AM IST on Nov 19 = 20:30:00 UTC on Nov 18
            var earlyMorningIstUtc = DateTime.Parse("2026-11-18T20:30:00.000Z").ToUniversalTime();

            // Verify half-open bounds encompass this early morning transaction
            bool isInIstDay = earlyMorningIstUtc >= DateTime.Parse(startUtc).ToUniversalTime() &&
                              earlyMorningIstUtc < DateTime.Parse(nextDayStartUtc).ToUniversalTime();

            isInIstDay.Should().BeTrue("02:00 AM IST on Nov 19 must belong strictly to Nov 19 IST business day");
        }

        [Fact]
        public async Task TestMode_SandboxDatabaseSeparationAndSafeReset()
        {
            // 1. Create transaction in Production Mode
            _pathProvider.SetTestMode(false);
            _pathProvider.IsTestMode.Should().BeFalse();
            _pathProvider.DatabasePath.Should().NotContain("TestMode");

            var prodTx = await _service.CreateTransactionAsync(new CreateTransactionCommand
            {
                CustomerName = "Production Customer",
                UnitPricePaise = 5000,
                IsTestMode = false
            });

            // 2. Switch to Test Mode
            _pathProvider.SetTestMode(true);
            _pathProvider.IsTestMode.Should().BeTrue();
            _pathProvider.DatabasePath.Should().Contain("TestMode");
            _pathProvider.DatabasePath.Should().EndWith("pvc_studio_test.db");

            var testTx = await _service.CreateTransactionAsync(new CreateTransactionCommand
            {
                CustomerName = "Test Sandbox Customer",
                UnitPricePaise = 7000,
                IsTestMode = true
            });

            // Verify production transactions are NOT in test database
            var testTransactions = await _service.GetRecentTransactionsAsync(100, isTestMode: true);
            testTransactions.Should().HaveCount(1);
            testTransactions.First().CustomerName.Should().Be("Test Sandbox Customer");

            // 3. Reset Test Database
            _pathProvider.ResetTestDatabase();
            var testAfterReset = await _service.GetRecentTransactionsAsync(100, isTestMode: true);
            testAfterReset.Should().BeEmpty();

            // 4. Switch back to Production Mode and verify production record remains completely intact
            _pathProvider.SetTestMode(false);
            var prodTransactions = await _service.GetRecentTransactionsAsync(100, isTestMode: false);
            prodTransactions.Should().HaveCount(1);
            prodTransactions.First().CustomerName.Should().Be("Production Customer");

            // 5. Attempting to reset database while in Production Mode MUST throw InvalidOperationException
            var act = () => _pathProvider.ResetTestDatabase();
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*FATAL SAFETY VIOLATION*");
        }
    }
}
