using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface ITransactionPaymentService
    {
        Task InitializeDatabaseAsync(CancellationToken ct = default);
        Task<BusinessTransaction> CreateTransactionAsync(CreateTransactionCommand command, CancellationToken ct = default);
        Task<TransactionPayment> RecordPaymentAsync(AddPaymentCommand command, CancellationToken ct = default);
        Task<TransactionPayment> RecordRefundAsync(AddRefundCommand command, CancellationToken ct = default);
        Task<BusinessTransaction> VoidTransactionAsync(VoidTransactionCommand command, CancellationToken ct = default);
        Task<DailyReconciliationReport> GetDailyReconciliationAsync(DateOnly istDate, bool isTestMode, CancellationToken ct = default);
        Task<IReadOnlyList<BusinessTransaction>> GetRecentTransactionsAsync(int limit = 100, bool? isTestMode = null, CancellationToken ct = default);
        Task<BusinessTransaction?> GetTransactionByIdAsync(string transactionId, CancellationToken ct = default);
        Task<IReadOnlyList<TransactionPayment>> GetPaymentsForTransactionAsync(string transactionId, CancellationToken ct = default);
        Task<IReadOnlyList<TransactionAuditTrail>> GetAuditTrailAsync(string transactionId, CancellationToken ct = default);
    }
}
