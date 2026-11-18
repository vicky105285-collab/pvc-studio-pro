using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class JobLedgerService : IJobLedgerService
    {
        private readonly string _connectionString;
        private readonly string _dbPath;
        private bool _initialized;

        public JobLedgerService(string? customDbPath = null)
        {
            _dbPath = customDbPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PVCStudioPro", "data", "pvc_studio.db"
            );
            _connectionString = $"Data Source={_dbPath};";
        }

        private async Task EnsureInitializedAsync()
        {
            if (_initialized) return;
            await InitializeDatabaseAsync();
            _initialized = true;
        }

        public async Task InitializeDatabaseAsync()
        {
            string? dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            string ddl = @"
                PRAGMA journal_mode = WAL;

                CREATE TABLE IF NOT EXISTS print_jobs (
                    job_id TEXT PRIMARY KEY,
                    job_number TEXT NOT NULL,
                    profile_id TEXT NOT NULL,
                    printer_name TEXT NOT NULL,
                    document_type TEXT NOT NULL,
                    status TEXT NOT NULL,
                    quantity INTEGER NOT NULL DEFAULT 1,
                    total_amount NUMERIC NOT NULL DEFAULT 0.00,
                    amount_paid NUMERIC NOT NULL DEFAULT 0.00,
                    payment_mode TEXT NOT NULL DEFAULT 'CASH',
                    is_reprint INTEGER NOT NULL DEFAULT 0,
                    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS printer_profiles (
                    profile_id TEXT PRIMARY KEY,
                    profile_name TEXT NOT NULL,
                    printer_device_name TEXT NOT NULL,
                    tray_type TEXT NOT NULL,
                    offset_x_mm REAL NOT NULL DEFAULT 0.0,
                    offset_y_mm REAL NOT NULL DEFAULT 0.0,
                    card_gap_mm REAL NOT NULL DEFAULT 5.60,
                    target_dpi INTEGER NOT NULL DEFAULT 600,
                    mirror_mode INTEGER NOT NULL DEFAULT 0,
                    is_default INTEGER NOT NULL DEFAULT 0,
                    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
            ";

            await connection.ExecuteAsync(ddl);

            // Seed default profile if none exists
            var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM printer_profiles;");
            if (count == 0)
            {
                var defaultProfile = new PrinterProfile
                {
                    ProfileId = "epson_l805_default",
                    ProfileName = "Epson L805 (2-Card Tray)",
                    PrinterDeviceName = "Epson L805 Series",
                    TrayType = "EPSON_2CARD_TRAY",
                    OffsetXMm = 0.0,
                    OffsetYMm = 0.0,
                    CardGapMm = 5.60,
                    TargetDpi = 600,
                    IsDefault = true
                };
                await SaveProfileAsync(defaultProfile);
            }
        }

        public async Task RecordJobAsync(PrintJobRecord record)
        {
            await EnsureInitializedAsync();
            if (string.IsNullOrEmpty(record.JobNumber))
            {
                record.JobNumber = $"INV-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
            }

            using var connection = new SqliteConnection(_connectionString);
            string sql = @"
                INSERT INTO print_jobs (job_id, job_number, profile_id, printer_name, document_type, status, quantity, total_amount, amount_paid, payment_mode, is_reprint, created_at)
                VALUES (@JobId, @JobNumber, @ProfileId, @PrinterName, @DocumentType, @Status, @Quantity, @TotalAmount, @AmountPaid, @PaymentMode, @IsReprint, @CreatedAt);
            ";
            await connection.ExecuteAsync(sql, record);
        }

        public async Task<IReadOnlyList<PrintJobRecord>> GetRecentJobsAsync(int limit = 50)
        {
            await EnsureInitializedAsync();
            using var connection = new SqliteConnection(_connectionString);
            string sql = "SELECT * FROM print_jobs ORDER BY created_at DESC LIMIT @Limit;";
            var list = await connection.QueryAsync<PrintJobRecord>(sql, new { Limit = limit });
            return list.ToList().AsReadOnly();
        }

        public async Task<DailySummary> GetDailySummaryAsync(DateTime date)
        {
            await EnsureInitializedAsync();
            using var connection = new SqliteConnection(_connectionString);
            string dateStr = date.ToString("yyyy-MM-dd");

            string sql = @"
                SELECT 
                    COUNT(*) as TotalCards,
                    COALESCE(SUM(total_amount), 0) as TotalRevenue,
                    SUM(CASE WHEN document_type = 'AADHAAR' THEN 1 ELSE 0 END) as AadhaarCount,
                    SUM(CASE WHEN document_type = 'PAN' THEN 1 ELSE 0 END) as PanCount,
                    SUM(CASE WHEN document_type = 'VOTER' THEN 1 ELSE 0 END) as VoterCount,
                    SUM(CASE WHEN document_type = 'PHOTO_STUDIO' THEN 1 ELSE 0 END) as PhotoSheetCount,
                    SUM(CASE WHEN document_type NOT IN ('AADHAAR', 'PAN', 'VOTER', 'PHOTO_STUDIO') THEN 1 ELSE 0 END) as OtherCount,
                    COALESCE(SUM(CASE WHEN payment_mode = 'CASH' THEN 1 ELSE 0 END), 0) as CashPaymentCount,
                    COALESCE(SUM(CASE WHEN payment_mode = 'UPI' THEN 1 ELSE 0 END), 0) as UpiPaymentCount
                FROM print_jobs
                WHERE date(created_at) = date(@TargetDate);
            ";

            var summary = await connection.QueryFirstOrDefaultAsync<DailySummary>(sql, new { TargetDate = dateStr }) ?? new DailySummary();
            summary.Date = date.Date;
            return summary;
        }

        public async Task<PrinterProfile> GetDefaultProfileAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = "SELECT * FROM printer_profiles WHERE is_default = 1 LIMIT 1;";
            var profile = await connection.QueryFirstOrDefaultAsync<PrinterProfile>(sql);
            return profile ?? new PrinterProfile();
        }

        public async Task SaveProfileAsync(PrinterProfile profile)
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = @"
                INSERT INTO printer_profiles (profile_id, profile_name, printer_device_name, tray_type, offset_x_mm, offset_y_mm, card_gap_mm, target_dpi, mirror_mode, is_default, created_at, updated_at)
                VALUES (@ProfileId, @ProfileName, @PrinterDeviceName, @TrayType, @OffsetXMm, @OffsetYMm, @CardGapMm, @TargetDpi, @MirrorMode, @IsDefault, @CreatedAt, @UpdatedAt)
                ON CONFLICT(profile_id) DO UPDATE SET
                    profile_name = excluded.profile_name,
                    printer_device_name = excluded.printer_device_name,
                    tray_type = excluded.tray_type,
                    offset_x_mm = excluded.offset_x_mm,
                    offset_y_mm = excluded.offset_y_mm,
                    card_gap_mm = excluded.card_gap_mm,
                    target_dpi = excluded.target_dpi,
                    mirror_mode = excluded.mirror_mode,
                    is_default = excluded.is_default,
                    updated_at = CURRENT_TIMESTAMP;
            ";
            await connection.ExecuteAsync(sql, profile);
        }
    }
}
