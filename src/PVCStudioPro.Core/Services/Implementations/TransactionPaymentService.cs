using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class TransactionPaymentService : ITransactionPaymentService
    {
        private readonly IDatabasePathProvider _pathProvider;

        public TransactionPaymentService(IDatabasePathProvider pathProvider)
        {
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        }

        private SqliteConnection CreateConnection()
        {
            string dbPath = _pathProvider.DatabasePath;
            string? dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            return conn;
        }

        public async Task InitializeDatabaseAsync(CancellationToken ct = default)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS business_transactions (
                    transaction_id TEXT PRIMARY KEY,
                    invoice_number TEXT NOT NULL UNIQUE,
                    session_id TEXT,
                    customer_name TEXT,
                    customer_phone TEXT,
                    document_type TEXT NOT NULL,
                    template_id TEXT,
                    printer_name TEXT NOT NULL,
                    quantity INTEGER NOT NULL DEFAULT 1 CHECK (quantity > 0),
                    unit_price_paise INTEGER NOT NULL DEFAULT 0 CHECK (unit_price_paise >= 0),
                    discount_paise INTEGER NOT NULL DEFAULT 0 CHECK (discount_paise >= 0),
                    net_total_paise INTEGER NOT NULL CHECK (net_total_paise >= 0),
                    paid_paise INTEGER NOT NULL DEFAULT 0 CHECK (paid_paise >= 0),
                    balance_paise INTEGER NOT NULL CHECK (balance_paise >= 0),
                    status TEXT NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'PARTIAL', 'PAID', 'VOID')),
                    is_test_mode INTEGER NOT NULL DEFAULT 0 CHECK (is_test_mode IN (0, 1)),
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_trans_created ON business_transactions(created_at, is_test_mode);
                CREATE INDEX IF NOT EXISTS idx_trans_status ON business_transactions(status, is_test_mode);
                CREATE INDEX IF NOT EXISTS idx_trans_invoice ON business_transactions(invoice_number);

                CREATE TABLE IF NOT EXISTS transaction_payments (
                    payment_id TEXT PRIMARY KEY,
                    transaction_id TEXT NOT NULL REFERENCES business_transactions(transaction_id) ON DELETE RESTRICT,
                    amount_paise INTEGER NOT NULL CHECK (amount_paise > 0),
                    is_refund INTEGER NOT NULL DEFAULT 0 CHECK (is_refund IN (0, 1)),
                    payment_method TEXT NOT NULL CHECK (payment_method IN ('CASH', 'UPI', 'CARD', 'OTHER')),
                    reference_number TEXT,
                    is_test_mode INTEGER NOT NULL DEFAULT 0 CHECK (is_test_mode IN (0, 1)),
                    operator_id TEXT NOT NULL DEFAULT 'Admin',
                    collected_at TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_pay_trans ON transaction_payments(transaction_id);
                CREATE INDEX IF NOT EXISTS idx_pay_collected ON transaction_payments(collected_at, is_test_mode);
                CREATE INDEX IF NOT EXISTS idx_pay_method ON transaction_payments(payment_method, is_test_mode);

                CREATE TABLE IF NOT EXISTS print_job_attempts (
                    attempt_id TEXT PRIMARY KEY,
                    transaction_id TEXT NOT NULL REFERENCES business_transactions(transaction_id) ON DELETE CASCADE,
                    attempt_number INTEGER NOT NULL CHECK (attempt_number > 0),
                    printer_name TEXT NOT NULL,
                    media_tray TEXT NOT NULL,
                    spooler_job_id INTEGER,
                    spooler_status TEXT NOT NULL,
                    physical_verification_status TEXT NOT NULL DEFAULT 'PendingVerification',
                    defect_reason TEXT,
                    is_billable INTEGER NOT NULL DEFAULT 1 CHECK (is_billable IN (0, 1)),
                    operator_notes TEXT,
                    spooled_at TEXT NOT NULL,
                    verified_at TEXT
                );

                CREATE INDEX IF NOT EXISTS idx_print_attempts_trans ON print_job_attempts(transaction_id);
                CREATE INDEX IF NOT EXISTS idx_print_attempts_date ON print_job_attempts(spooled_at);

                CREATE TABLE IF NOT EXISTS transaction_audit_trail (
                    audit_id TEXT PRIMARY KEY,
                    transaction_id TEXT NOT NULL REFERENCES business_transactions(transaction_id) ON DELETE RESTRICT,
                    action_type TEXT NOT NULL,
                    field_changed TEXT,
                    old_value TEXT,
                    new_value TEXT,
                    mandatory_reason TEXT NOT NULL,
                    operator_id TEXT NOT NULL DEFAULT 'Admin',
                    timestamp TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_audit_trans ON transaction_audit_trail(transaction_id);
            ";

            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task<BusinessTransaction> CreateTransactionAsync(CreateTransactionCommand command, CancellationToken ct = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            await InitializeDatabaseAsync(ct);
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            try
            {
                long grossTotal = command.UnitPricePaise * command.Quantity;
                long netTotal = Math.Max(0, grossTotal - command.DiscountPaise);

                string invoiceNumber = !string.IsNullOrWhiteSpace(command.InvoiceNumber)
                    ? command.InvoiceNumber
                    : (command.IsTestMode
                        ? $"TEST-{Random.Shared.Next(1000, 9999)}"
                        : $"INV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}");

                var transaction = new BusinessTransaction
                {
                    TransactionId = Guid.NewGuid().ToString("N"),
                    InvoiceNumber = invoiceNumber,
                    SessionId = command.SessionId,
                    CustomerName = command.CustomerName,
                    CustomerPhone = command.CustomerPhone,
                    DocumentType = command.DocumentType,
                    TemplateId = command.TemplateId,
                    PrinterName = command.PrinterName,
                    Quantity = command.Quantity,
                    UnitPricePaise = command.UnitPricePaise,
                    DiscountPaise = command.DiscountPaise,
                    NetTotalPaise = netTotal,
                    PaidPaise = 0,
                    BalancePaise = netTotal,
                    Status = netTotal == 0 ? "PAID" : "PENDING",
                    IsTestMode = command.IsTestMode,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                const string insertSql = @"
                    INSERT INTO business_transactions (
                        transaction_id, invoice_number, session_id, customer_name, customer_phone,
                        document_type, template_id, printer_name, quantity, unit_price_paise,
                        discount_paise, net_total_paise, paid_paise, balance_paise, status,
                        is_test_mode, created_at, updated_at
                    ) VALUES (
                        @TransactionId, @InvoiceNumber, @SessionId, @CustomerName, @CustomerPhone,
                        @DocumentType, @TemplateId, @PrinterName, @Quantity, @UnitPricePaise,
                        @DiscountPaise, @NetTotalPaise, @PaidPaise, @BalancePaise, @Status,
                        @IsTestMode, @CreatedAtStr, @UpdatedAtStr
                    );";

                await conn.ExecuteAsync(insertSql, new
                {
                    transaction.TransactionId,
                    transaction.InvoiceNumber,
                    transaction.SessionId,
                    transaction.CustomerName,
                    transaction.CustomerPhone,
                    transaction.DocumentType,
                    transaction.TemplateId,
                    transaction.PrinterName,
                    transaction.Quantity,
                    transaction.UnitPricePaise,
                    transaction.DiscountPaise,
                    transaction.NetTotalPaise,
                    transaction.PaidPaise,
                    transaction.BalancePaise,
                    transaction.Status,
                    IsTestMode = transaction.IsTestMode ? 1 : 0,
                    CreatedAtStr = transaction.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    UpdatedAtStr = transaction.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }, tx);

                // Record audit
                await InsertAuditAsync(conn, tx, transaction.TransactionId, "INVOICE_CREATED", null, null, netTotal.ToString(), "Invoice created");

                // If initial payment provided
                if (command.InitialPaymentPaise > 0)
                {
                    if (command.InitialPaymentPaise > netTotal)
                    {
                        throw new OverpaymentNotPermittedException($"Initial payment of {command.InitialPaymentPaise / 100.0:F2} exceeds net invoice total of {netTotal / 100.0:F2}.");
                    }

                    var pay = new TransactionPayment
                    {
                        PaymentId = Guid.NewGuid().ToString("N"),
                        TransactionId = transaction.TransactionId,
                        AmountPaise = command.InitialPaymentPaise,
                        IsRefund = false,
                        PaymentMethod = command.InitialPaymentMethod,
                        ReferenceNumber = command.ReferenceNumber,
                        IsTestMode = command.IsTestMode,
                        OperatorId = "Admin",
                        CollectedAt = DateTime.UtcNow
                    };

                    await InsertPaymentAsync(conn, tx, pay);

                    transaction.PaidPaise = command.InitialPaymentPaise;
                    transaction.BalancePaise = netTotal - command.InitialPaymentPaise;
                    transaction.Status = transaction.BalancePaise == 0 ? "PAID" : "PARTIAL";

                    await conn.ExecuteAsync(@"
                        UPDATE business_transactions
                        SET paid_paise = @PaidPaise, balance_paise = @BalancePaise, status = @Status, updated_at = @UpdatedAtStr
                        WHERE transaction_id = @TransactionId;",
                        new
                        {
                            transaction.PaidPaise,
                            transaction.BalancePaise,
                            transaction.Status,
                            UpdatedAtStr = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                            transaction.TransactionId
                        }, tx);

                    await InsertAuditAsync(conn, tx, transaction.TransactionId, "PAYMENT_ADDED", "paid_paise", "0", transaction.PaidPaise.ToString(), $"Initial payment via {command.InitialPaymentMethod}");
                }

                tx.Commit();
                return transaction;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<TransactionPayment> RecordPaymentAsync(AddPaymentCommand command, CancellationToken ct = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (command.AmountPaise <= 0) throw new ArgumentException("Payment amount must be greater than zero.", nameof(command));

            await InitializeDatabaseAsync(ct);
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            try
            {
                var transaction = await GetTransactionInternalAsync(conn, tx, command.TransactionId);
                if (transaction == null) throw new KeyNotFoundException($"Transaction '{command.TransactionId}' not found.");

                if (transaction.IsTestMode != command.IsTestMode)
                {
                    throw new SecurityIsolationException("Security Isolation Violation: Test mode mismatch between payment and transaction.");
                }

                if (transaction.Status == "VOID")
                {
                    throw new InvalidOperationException("Cannot record payment against a VOID transaction.");
                }

                var payments = await GetPaymentsInternalAsync(conn, tx, command.TransactionId);
                long currentNetPaid = payments.Where(p => !p.IsRefund).Sum(p => p.AmountPaise) - payments.Where(p => p.IsRefund).Sum(p => p.AmountPaise);

                if (currentNetPaid + command.AmountPaise > transaction.NetTotalPaise)
                {
                    long remaining = Math.Max(0, transaction.NetTotalPaise - currentNetPaid);
                    throw new OverpaymentNotPermittedException(
                        $"Payment of {command.AmountPaise / 100.0:F2} exceeds remaining balance of {remaining / 100.0:F2}. Overpayment is rejected by policy.");
                }

                var payment = new TransactionPayment
                {
                    PaymentId = Guid.NewGuid().ToString("N"),
                    TransactionId = command.TransactionId,
                    AmountPaise = command.AmountPaise,
                    IsRefund = false,
                    PaymentMethod = command.PaymentMethod,
                    ReferenceNumber = command.ReferenceNumber,
                    IsTestMode = command.IsTestMode,
                    OperatorId = command.OperatorId,
                    CollectedAt = DateTime.UtcNow
                };

                await InsertPaymentAsync(conn, tx, payment);

                long newPaid = currentNetPaid + command.AmountPaise;
                long newBalance = transaction.NetTotalPaise - newPaid;
                string newStatus = newBalance == 0 ? "PAID" : "PARTIAL";

                await conn.ExecuteAsync(@"
                    UPDATE business_transactions
                    SET paid_paise = @newPaid, balance_paise = @newBalance, status = @newStatus, updated_at = @nowStr
                    WHERE transaction_id = @TransactionId;",
                    new
                    {
                        newPaid,
                        newBalance,
                        newStatus,
                        nowStr = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        transaction.TransactionId
                    }, tx);

                await InsertAuditAsync(conn, tx, transaction.TransactionId, "PAYMENT_ADDED", "paid_paise", currentNetPaid.ToString(), newPaid.ToString(), command.Notes ?? $"Payment via {command.PaymentMethod}");

                tx.Commit();
                return payment;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<TransactionPayment> RecordRefundAsync(AddRefundCommand command, CancellationToken ct = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (command.AmountPaise <= 0) throw new ArgumentException("Refund amount must be greater than zero.", nameof(command));
            if (string.IsNullOrWhiteSpace(command.MandatoryReason)) throw new ArgumentException("Mandatory refund reason must be provided.", nameof(command));

            await InitializeDatabaseAsync(ct);
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            try
            {
                var transaction = await GetTransactionInternalAsync(conn, tx, command.TransactionId);
                if (transaction == null) throw new KeyNotFoundException($"Transaction '{command.TransactionId}' not found.");

                if (transaction.IsTestMode != command.IsTestMode)
                {
                    throw new SecurityIsolationException("Security Isolation Violation: Test mode mismatch between refund and transaction.");
                }

                var payments = await GetPaymentsInternalAsync(conn, tx, command.TransactionId);
                long totalCollected = payments.Where(p => !p.IsRefund).Sum(p => p.AmountPaise);
                long totalRefunded = payments.Where(p => p.IsRefund).Sum(p => p.AmountPaise);
                long maxRefundable = totalCollected - totalRefunded;

                if (command.AmountPaise > maxRefundable)
                {
                    throw new RefundExceedsCollectedException(
                        $"Requested refund of {command.AmountPaise / 100.0:F2} exceeds total net collected amount of {maxRefundable / 100.0:F2}.");
                }

                var refund = new TransactionPayment
                {
                    PaymentId = Guid.NewGuid().ToString("N"),
                    TransactionId = command.TransactionId,
                    AmountPaise = command.AmountPaise,
                    IsRefund = true,
                    PaymentMethod = command.PaymentMethod,
                    IsTestMode = command.IsTestMode,
                    OperatorId = command.OperatorId,
                    CollectedAt = DateTime.UtcNow
                };

                await InsertPaymentAsync(conn, tx, refund);

                long newPaid = maxRefundable - command.AmountPaise;
                long newBalance = transaction.NetTotalPaise - newPaid;
                string newStatus = transaction.Status == "VOID"
                    ? "VOID"
                    : (newPaid == 0 ? "PENDING" : (newBalance == 0 ? "PAID" : "PARTIAL"));

                await conn.ExecuteAsync(@"
                    UPDATE business_transactions
                    SET paid_paise = @newPaid, balance_paise = @newBalance, status = @newStatus, updated_at = @nowStr
                    WHERE transaction_id = @TransactionId;",
                    new
                    {
                        newPaid,
                        newBalance,
                        newStatus,
                        nowStr = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        transaction.TransactionId
                    }, tx);

                await InsertAuditAsync(conn, tx, transaction.TransactionId, "REFUNDED", "paid_paise", maxRefundable.ToString(), newPaid.ToString(), command.MandatoryReason);

                tx.Commit();
                return refund;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<BusinessTransaction> VoidTransactionAsync(VoidTransactionCommand command, CancellationToken ct = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (string.IsNullOrWhiteSpace(command.MandatoryReason)) throw new ArgumentException("Mandatory void reason must be provided.", nameof(command));

            await InitializeDatabaseAsync(ct);
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            try
            {
                var transaction = await GetTransactionInternalAsync(conn, tx, command.TransactionId);
                if (transaction == null) throw new KeyNotFoundException($"Transaction '{command.TransactionId}' not found.");

                if (transaction.IsTestMode != command.IsTestMode)
                {
                    throw new SecurityIsolationException("Security Isolation Violation: Test mode mismatch between void command and transaction.");
                }

                if (transaction.Status == "VOID")
                {
                    tx.Commit();
                    return transaction;
                }

                // If refunding all collected
                if (command.RefundAllCollected)
                {
                    var payments = await GetPaymentsInternalAsync(conn, tx, command.TransactionId);
                    long totalCollected = payments.Where(p => !p.IsRefund).Sum(p => p.AmountPaise);
                    long totalRefunded = payments.Where(p => p.IsRefund).Sum(p => p.AmountPaise);
                    long netPaid = totalCollected - totalRefunded;

                    if (netPaid > 0)
                    {
                        var refund = new TransactionPayment
                        {
                            PaymentId = Guid.NewGuid().ToString("N"),
                            TransactionId = command.TransactionId,
                            AmountPaise = netPaid,
                            IsRefund = true,
                            PaymentMethod = command.RefundPaymentMethod,
                            IsTestMode = command.IsTestMode,
                            OperatorId = command.OperatorId,
                            CollectedAt = DateTime.UtcNow
                        };
                        await InsertPaymentAsync(conn, tx, refund);
                    }
                }

                await conn.ExecuteAsync(@"
                    UPDATE business_transactions
                    SET status = 'VOID', balance_paise = 0, updated_at = @nowStr
                    WHERE transaction_id = @TransactionId;",
                    new
                    {
                        nowStr = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        transaction.TransactionId
                    }, tx);

                await InsertAuditAsync(conn, tx, transaction.TransactionId, "VOIDED", "status", transaction.Status, "VOID", command.MandatoryReason);

                tx.Commit();
                transaction.Status = "VOID";
                transaction.BalancePaise = 0;
                return transaction;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<DailyReconciliationReport> GetDailyReconciliationAsync(DateOnly istDate, bool isTestMode, CancellationToken ct = default)
        {
            await InitializeDatabaseAsync(ct);
            var (startUtc, nextDayStartUtc) = IndianBusinessDay.GetIstDayUtcBounds(istDate);

            using var conn = CreateConnection();

            const string query = @"
                SELECT 
                    COALESCE(SUM(CASE WHEN is_refund = 0 THEN amount_paise ELSE 0 END), 0) AS TotalCollectedPaise,
                    COALESCE(SUM(CASE WHEN is_refund = 1 THEN amount_paise ELSE 0 END), 0) AS TotalRefundedPaise,
                    COALESCE(SUM(CASE WHEN is_refund = 0 AND payment_method = 'CASH' THEN amount_paise WHEN is_refund = 1 AND payment_method = 'CASH' THEN -amount_paise ELSE 0 END), 0) AS NetCashPaise,
                    COALESCE(SUM(CASE WHEN is_refund = 0 AND payment_method = 'UPI' THEN amount_paise WHEN is_refund = 1 AND payment_method = 'UPI' THEN -amount_paise ELSE 0 END), 0) AS UpiPaise,
                    COALESCE(SUM(CASE WHEN is_refund = 0 AND payment_method = 'CARD' THEN amount_paise WHEN is_refund = 1 AND payment_method = 'CARD' THEN -amount_paise ELSE 0 END), 0) AS CardPaise,
                    COUNT(DISTINCT transaction_id) AS TransactionCount
                FROM transaction_payments
                WHERE is_test_mode = @isTestModeInt
                  AND collected_at >= @startUtc 
                  AND collected_at < @nextDayStartUtc;";

            var report = await conn.QueryFirstOrDefaultAsync<DailyReconciliationReport>(query, new
            {
                isTestModeInt = isTestMode ? 1 : 0,
                startUtc,
                nextDayStartUtc
            });

            report ??= new DailyReconciliationReport();
            report.BusinessDateIst = istDate;
            report.IsTestMode = isTestMode;
            report.TotalRevenuePaise = report.TotalCollectedPaise - report.TotalRefundedPaise;
            return report;
        }

        public async Task<IReadOnlyList<BusinessTransaction>> GetRecentTransactionsAsync(int limit = 100, bool? isTestMode = null, CancellationToken ct = default)
        {
            await InitializeDatabaseAsync(ct);
            using var conn = CreateConnection();

            string sql = @"
                SELECT 
                    transaction_id AS TransactionId,
                    invoice_number AS InvoiceNumber,
                    session_id AS SessionId,
                    customer_name AS CustomerName,
                    customer_phone AS CustomerPhone,
                    document_type AS DocumentType,
                    template_id AS TemplateId,
                    printer_name AS PrinterName,
                    quantity AS Quantity,
                    unit_price_paise AS UnitPricePaise,
                    discount_paise AS DiscountPaise,
                    net_total_paise AS NetTotalPaise,
                    paid_paise AS PaidPaise,
                    balance_paise AS BalancePaise,
                    status AS Status,
                    is_test_mode AS IsTestMode,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM business_transactions";

            if (isTestMode.HasValue)
            {
                sql += " WHERE is_test_mode = @testModeInt";
            }

            sql += " ORDER BY created_at DESC LIMIT @limit;";

            var rows = await conn.QueryAsync<BusinessTransaction>(sql, new
            {
                testModeInt = isTestMode.HasValue && isTestMode.Value ? 1 : 0,
                limit
            });

            return rows.ToList().AsReadOnly();
        }

        public async Task<BusinessTransaction?> GetTransactionByIdAsync(string transactionId, CancellationToken ct = default)
        {
            await InitializeDatabaseAsync(ct);
            using var conn = CreateConnection();
            return await GetTransactionInternalAsync(conn, null, transactionId);
        }

        public async Task<IReadOnlyList<TransactionPayment>> GetPaymentsForTransactionAsync(string transactionId, CancellationToken ct = default)
        {
            await InitializeDatabaseAsync(ct);
            using var conn = CreateConnection();
            return await GetPaymentsInternalAsync(conn, null, transactionId);
        }

        public async Task<IReadOnlyList<TransactionAuditTrail>> GetAuditTrailAsync(string transactionId, CancellationToken ct = default)
        {
            await InitializeDatabaseAsync(ct);
            using var conn = CreateConnection();

            const string sql = @"
                SELECT 
                    audit_id AS AuditId,
                    transaction_id AS TransactionId,
                    action_type AS ActionType,
                    field_changed AS FieldChanged,
                    old_value AS OldValue,
                    new_value AS NewValue,
                    mandatory_reason AS MandatoryReason,
                    operator_id AS OperatorId,
                    timestamp AS Timestamp
                FROM transaction_audit_trail
                WHERE transaction_id = @transactionId
                ORDER BY timestamp ASC;";

            var rows = await conn.QueryAsync<TransactionAuditTrail>(sql, new { transactionId });
            return rows.ToList().AsReadOnly();
        }

        private static async Task<BusinessTransaction?> GetTransactionInternalAsync(SqliteConnection conn, SqliteTransaction? tx, string transactionId)
        {
            const string sql = @"
                SELECT 
                    transaction_id AS TransactionId,
                    invoice_number AS InvoiceNumber,
                    session_id AS SessionId,
                    customer_name AS CustomerName,
                    customer_phone AS CustomerPhone,
                    document_type AS DocumentType,
                    template_id AS TemplateId,
                    printer_name AS PrinterName,
                    quantity AS Quantity,
                    unit_price_paise AS UnitPricePaise,
                    discount_paise AS DiscountPaise,
                    net_total_paise AS NetTotalPaise,
                    paid_paise AS PaidPaise,
                    balance_paise AS BalancePaise,
                    status AS Status,
                    is_test_mode AS IsTestMode,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM business_transactions
                WHERE transaction_id = @transactionId;";

            return await conn.QueryFirstOrDefaultAsync<BusinessTransaction>(sql, new { transactionId }, tx);
        }

        private static async Task<List<TransactionPayment>> GetPaymentsInternalAsync(SqliteConnection conn, SqliteTransaction? tx, string transactionId)
        {
            const string sql = @"
                SELECT 
                    payment_id AS PaymentId,
                    transaction_id AS TransactionId,
                    amount_paise AS AmountPaise,
                    is_refund AS IsRefund,
                    payment_method AS PaymentMethod,
                    reference_number AS ReferenceNumber,
                    is_test_mode AS IsTestMode,
                    operator_id AS OperatorId,
                    collected_at AS CollectedAt
                FROM transaction_payments
                WHERE transaction_id = @transactionId
                ORDER BY collected_at ASC;";

            var rows = await conn.QueryAsync<TransactionPayment>(sql, new { transactionId }, tx);
            return rows.ToList();
        }

        private static async Task InsertPaymentAsync(SqliteConnection conn, SqliteTransaction tx, TransactionPayment payment)
        {
            const string sql = @"
                INSERT INTO transaction_payments (
                    payment_id, transaction_id, amount_paise, is_refund, payment_method,
                    reference_number, is_test_mode, operator_id, collected_at
                ) VALUES (
                    @PaymentId, @TransactionId, @AmountPaise, @IsRefund, @PaymentMethod,
                    @ReferenceNumber, @IsTestMode, @OperatorId, @CollectedAtStr
                );";

            await conn.ExecuteAsync(sql, new
            {
                payment.PaymentId,
                payment.TransactionId,
                payment.AmountPaise,
                IsRefund = payment.IsRefund ? 1 : 0,
                payment.PaymentMethod,
                payment.ReferenceNumber,
                IsTestMode = payment.IsTestMode ? 1 : 0,
                payment.OperatorId,
                CollectedAtStr = payment.CollectedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            }, tx);
        }

        private static async Task InsertAuditAsync(
            SqliteConnection conn,
            SqliteTransaction tx,
            string transactionId,
            string actionType,
            string? fieldChanged,
            string? oldValue,
            string? newValue,
            string reason)
        {
            const string sql = @"
                INSERT INTO transaction_audit_trail (
                    audit_id, transaction_id, action_type, field_changed, old_value,
                    new_value, mandatory_reason, operator_id, timestamp
                ) VALUES (
                    @AuditId, @TransactionId, @ActionType, @FieldChanged, @OldValue,
                    @NewValue, @MandatoryReason, @OperatorId, @TimestampStr
                );";

            await conn.ExecuteAsync(sql, new
            {
                AuditId = Guid.NewGuid().ToString("N"),
                TransactionId = transactionId,
                ActionType = actionType,
                FieldChanged = fieldChanged,
                OldValue = oldValue,
                NewValue = newValue,
                MandatoryReason = reason,
                OperatorId = "Admin",
                TimestampStr = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            }, tx);
        }
    }
}
