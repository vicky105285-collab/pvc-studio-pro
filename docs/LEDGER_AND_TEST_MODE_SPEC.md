# PVC Studio Pro — Business Ledger, Payment Consistency & Test Mode Specification (Phase 0.3 Validated)
**Enterprise Financial Ledger, Normalized Multi-Payment Architecture & Total Environment Sandbox Isolation**

- **Document:** `docs/LEDGER_AND_TEST_MODE_SPEC.md`
- **Revision:** Phase 0.3 Validated
- **Date:** 2026-11-19
- **Status:** Architectural Specification (Pre-Implementation Gate)

---

## 1. Normalized Financial Ledger & Print Attempt Architecture

To support commercial e-Sevai operations without financial drift or rounding discrepancies, financial tracking and print tracking are structured into an interconnected, normalized architecture:

```
┌────────────────────────────────────────────────────────┐
│              business_transactions (Master)            │
│  - transaction_id (PK)         - invoice_number (INV)  │
│  - customer_name               - customer_phone        │
│  - document_type               - template_id           │
│  - quantity                    - unit_price_paise      │
│  - discount_paise              - net_total_paise       │
│  - paid_paise (Derived)        - balance_paise(Derived)│
│  - status (PENDING/PARTIAL/PAID/VOID)                  │
│  - is_test_mode (0/1)                                  │
│  - created_at (UTC)            - updated_at (UTC)      │
└────────────┬───────────────────────────────┬───────────┘
             │ 1 : N                         │ 1 : N
             ▼                               ▼
┌──────────────────────────────┐ ┌──────────────────────────────┐
│ transaction_payments         │ │ print_job_attempts           │
│ - payment_id (PK)            │ │ - attempt_id (PK)            │
│ - transaction_id (FK)        │ │ - transaction_id (FK)        │
│ - payment_method (CASH/UPI)  │ │ - attempt_number (1, 2, ...) │
│ - amount_paise               │ │ - printer_name               │
│ - is_refund (0/1)            │ │ - spooler_status             │
│ - is_test_mode (0/1)         │ │ - physical_verification_stat │
│ - collected_at (UTC)         │ │ - defect_reason              │
└────────────┬─────────────────┘ │ - is_billable (1=Orig,0=Repr)│
             │ 1 : N             │ - spooled_at (UTC)           │
             ▼                   └──────────────────────────────┘
┌──────────────────────────────┐
│ transaction_audit_trail      │
│ - audit_id (PK)              │
│ - transaction_id (FK)        │
│ - action_type                │
│ - old_value / new_value      │
│ - mandatory_reason           │
│ - timestamp (UTC)            │
└──────────────────────────────┘
```

---

## 2. DDL Schema Definition (SQLite)

All currency fields are strictly stored in **integer paise** (64-bit integer, where ₹1.00 = 100 paise) to prevent floating-point rounding errors. Timestamps are stored in ISO 8601 UTC strings (`strftime('%Y-%m-%dT%H:%M:%fZ', 'now')`).

### Table 1: `business_transactions`
```sql
CREATE TABLE IF NOT EXISTS business_transactions (
    transaction_id TEXT PRIMARY KEY,
    invoice_number TEXT NOT NULL UNIQUE,          -- Format: INV-YYYYMMDD-XXXX
    session_id TEXT,                              -- Linked document session ID
    customer_name TEXT,                           -- Customer name
    customer_phone TEXT,                          -- Customer contact number
    document_type TEXT NOT NULL,                  -- e.g. AADHAAR_CR80, PAN_NSDL, PHOTO_8UP
    template_id TEXT,                             -- Reference to TemplateRegistry ID
    printer_name TEXT NOT NULL,                   -- Windows spooler queue name
    quantity INTEGER NOT NULL DEFAULT 1 CHECK (quantity > 0),
    unit_price_paise INTEGER NOT NULL DEFAULT 0 CHECK (unit_price_paise >= 0),
    discount_paise INTEGER NOT NULL DEFAULT 0 CHECK (discount_paise >= 0),
    net_total_paise INTEGER NOT NULL CHECK (net_total_paise >= 0),
    paid_paise INTEGER NOT NULL DEFAULT 0 CHECK (paid_paise >= 0),
    balance_paise INTEGER NOT NULL CHECK (balance_paise >= 0),
    status TEXT NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'PARTIAL', 'PAID', 'VOID')),
    is_test_mode INTEGER NOT NULL DEFAULT 0 CHECK (is_test_mode IN (0, 1)),
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX IF NOT EXISTS idx_trans_created ON business_transactions(created_at, is_test_mode);
CREATE INDEX IF NOT EXISTS idx_trans_status ON business_transactions(status, is_test_mode);
CREATE INDEX IF NOT EXISTS idx_trans_invoice ON business_transactions(invoice_number);
```

### Table 2: `transaction_payments` (Normalized Receipts & Refunds)
```sql
CREATE TABLE IF NOT EXISTS transaction_payments (
    payment_id TEXT PRIMARY KEY,
    transaction_id TEXT NOT NULL REFERENCES business_transactions(transaction_id) ON DELETE RESTRICT,
    amount_paise INTEGER NOT NULL CHECK (amount_paise > 0),
    is_refund INTEGER NOT NULL DEFAULT 0 CHECK (is_refund IN (0, 1)),
    payment_method TEXT NOT NULL CHECK (payment_method IN ('CASH', 'UPI', 'CARD', 'OTHER')),
    reference_number TEXT,                          -- UPI reference ID / Card approval code
    is_test_mode INTEGER NOT NULL DEFAULT 0 CHECK (is_test_mode IN (0, 1)),
    operator_id TEXT NOT NULL DEFAULT 'Admin',
    collected_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX IF NOT EXISTS idx_pay_trans ON transaction_payments(transaction_id);
CREATE INDEX IF NOT EXISTS idx_pay_collected ON transaction_payments(collected_at, is_test_mode);
CREATE INDEX IF NOT EXISTS idx_pay_method ON transaction_payments(payment_method, is_test_mode);
```

### Table 3: `print_job_attempts` (Linked Attempts & Non-Billable Reprints)
```sql
CREATE TABLE IF NOT EXISTS print_job_attempts (
    attempt_id TEXT PRIMARY KEY,
    transaction_id TEXT NOT NULL REFERENCES business_transactions(transaction_id) ON DELETE CASCADE,
    attempt_number INTEGER NOT NULL CHECK (attempt_number > 0),
    printer_name TEXT NOT NULL,
    media_tray TEXT NOT NULL,
    spooler_job_id INTEGER,
    spooler_status TEXT NOT NULL CHECK (spooler_status IN (
        'SubmittedToSpooler',
        'AcceptedBySpooler',
        'SpoolerProcessingCompleted',
        'SpoolerErrorOrCancelled'
    )),
    physical_verification_status TEXT NOT NULL DEFAULT 'PendingVerification' CHECK (
        physical_verification_status IN (
            'PendingVerification',
            'ConfirmedSuccessful',
            'DefectiveOutput'
        )
    ),
    defect_reason TEXT CHECK (defect_reason IN (
        NULL,
        'PaperJam',
        'RibbonScuff',
        'AlignmentShift',
        'ColorBanding',
        'CardFeedError',
        'OperatorError',
        'Other'
    )),
    is_billable INTEGER NOT NULL DEFAULT 1 CHECK (is_billable IN (0, 1)),
    operator_notes TEXT,
    spooled_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    verified_at TEXT
);

CREATE INDEX IF NOT EXISTS idx_print_attempts_trans ON print_job_attempts(transaction_id);
CREATE INDEX IF NOT EXISTS idx_print_attempts_date ON print_job_attempts(spooled_at);
```

### Table 4: `transaction_audit_trail` (Immutable Change History)
```sql
CREATE TABLE IF NOT EXISTS transaction_audit_trail (
    audit_id TEXT PRIMARY KEY,
    transaction_id TEXT NOT NULL REFERENCES business_transactions(transaction_id) ON DELETE RESTRICT,
    action_type TEXT NOT NULL CHECK (action_type IN ('INVOICE_CREATED', 'PAYMENT_ADDED', 'VOIDED', 'REFUNDED', 'REPRINTED')),
    field_changed TEXT,
    old_value TEXT,
    new_value TEXT,
    mandatory_reason TEXT NOT NULL,
    operator_id TEXT NOT NULL DEFAULT 'Admin',
    timestamp TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX IF NOT EXISTS idx_audit_trans ON transaction_audit_trail(transaction_id);
```

---

## 3. Transactional Payment Service (`ITransactionPaymentService`)

To prevent data drift, race conditions, or partial state updates, all financial mutations must route through `ITransactionPaymentService`.

### 3.1 Service Contract
```csharp
public interface ITransactionPaymentService
{
    Task<PaymentResult> RecordPaymentAsync(AddPaymentCommand command, CancellationToken ct = default);
    Task<RefundResult> RecordRefundAsync(AddRefundCommand command, CancellationToken ct = default);
    Task<VoidResult> VoidTransactionAsync(VoidTransactionCommand command, CancellationToken ct = default);
    Task<DailyReconciliationReport> GetDailyReconciliationAsync(DateOnly istDate, bool isTestMode, CancellationToken ct = default);
}
```

### 3.2 ACID Transaction Guarantees & Business Rules
1. **Single SQLite Transaction:** Every operation executes inside `BEGIN IMMEDIATE TRANSACTION`.
2. **Refund Ceiling Enforcement:**
   $$\text{MaxRefundablePaise} = \sum_{\text{is\_refund}=0} \text{amount\_paise} - \sum_{\text{is\_refund}=1} \text{amount\_paise}$$
   If `refund_amount > MaxRefundablePaise`, the operation throws `RefundExceedsCollectedException` and aborts. A customer can never be refunded more than total collected receipts.
3. **Overpayment Prevention Policy:**
   If $(\text{current\_paid} + \text{payment\_amount}) > \text{net\_total\_paise}$, the operation is rejected by default (`OverpaymentNotPermittedException`). In e-Sevai operations, customers pay the exact invoice amount; accidental overpayment indicates operator keying error.
4. **Environment Isolation Guard:**
   The service asserts: `payment.is_test_mode == transaction.is_test_mode`. If a test payment attempts to associate with a production invoice, the transaction aborts with `SecurityIsolationException`.
5. **Read-Only Derived UI Fields:**
   `paid_paise`, `balance_paise`, and `status` in `business_transactions` are read-only properties derived strictly from the payments table. No UI ViewModel or command can write directly to these fields.

---

## 4. Indian Financial Reporting & Explicit IST Business-Day Boundaries

### 4.1 The Flaw of SQLite `date('now')`
Timestamps are stored in UTC strings (`2026-11-19T03:30:00.000Z`). However, daily closing reports for e-Sevai centres must adhere strictly to **Indian Standard Time (IST, UTC+05:30)**.

In SQLite, executing `WHERE date(collected_at) = date('now')` is fatal:
- SQLite `date('now')` evaluates to **UTC date**, not IST date.
- At 02:00 AM IST on November 19, UTC is 20:30 PM on November 18.
- Using `date('now')` would exclude all transactions recorded between midnight and 5:30 AM IST on November 19, erroneously attributing them to November 18.
- Wrapping database columns in functions (e.g. `date(datetime(collected_at, '+5 hours', '30 minutes'))`) causes full table scans and prevents SQLite from using B-Tree index seeks on `idx_pay_collected`.

### 4.2 Parameterized IST Day Bounds
The service computes explicit UTC bounds for the requested IST business day:

```csharp
public static class IndianBusinessDay
{
    private static readonly TimeZoneInfo IstZone = 
        TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    public static (string StartUtcString, string EndUtcString) GetIstDayUtcBounds(DateOnly istDate)
    {
        var istStart = istDate.ToDateTime(TimeOnly.MinValue);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(istStart, IstZone);

        var istEnd = istDate.ToDateTime(new TimeOnly(23, 59, 59, 999));
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(istEnd, IstZone);

        return (
            startUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            endUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        );
    }
}
```

*Example for Business Date November 19, 2026:*
- `StartUtc`: `2026-11-18T18:30:00.000Z` ($00:00:00\text{ IST}$)
- `EndUtc`: `2026-11-19T18:29:59.999Z` ($23:59:59.999\text{ IST}$)

### 4.3 High-Performance Indexed SQL Reporting
```sql
-- Daily Cash Reconciliation Query (IST Business Day)
SELECT 
    COALESCE(SUM(CASE WHEN is_refund = 0 THEN amount_paise ELSE 0 END), 0) AS total_collected_paise,
    COALESCE(SUM(CASE WHEN is_refund = 1 THEN amount_paise ELSE 0 END), 0) AS total_refunded_paise,
    COALESCE(SUM(CASE WHEN is_refund = 0 THEN amount_paise ELSE -amount_paise END), 0) AS net_cash_paise
FROM transaction_payments
WHERE payment_method = 'CASH'
  AND is_test_mode = 0
  AND collected_at >= @StartUtcString 
  AND collected_at <= @EndUtcString;
```

---

## 5. SQL Queries & Expected Results for the 5 Accounting Scenarios

### Scenario 1: ₹100 cash receipt and ₹20 cash refund
- **Action:** Customer pays ₹100 cash for 2 PVC cards. 1 card is defective; operator issues ₹20 cash refund.
- **Ledger Entries:**
  - `Payment 1`: `amount_paise = 10000`, `is_refund = 0`, `payment_method = 'CASH'`
  - `Payment 2`: `amount_paise = 2000`, `is_refund = 1`, `payment_method = 'CASH'`
- **SQL Query (Daily Cash Drawer Reconciliation):**
```sql
SELECT 
    COALESCE(SUM(CASE WHEN is_refund = 0 THEN amount_paise ELSE 0 END), 0) AS total_collected_paise,
    COALESCE(SUM(CASE WHEN is_refund = 1 THEN amount_paise ELSE 0 END), 0) AS total_refunded_paise,
    COALESCE(SUM(CASE WHEN is_refund = 0 THEN amount_paise ELSE -amount_paise END), 0) AS net_cash_paise
FROM transaction_payments
WHERE payment_method = 'CASH' 
  AND is_test_mode = 0
  AND collected_at >= @StartUtcString 
  AND collected_at <= @EndUtcString;
```
- **Expected Output:**
  - `total_collected_paise`: `10000` (₹100.00)
  - `total_refunded_paise`: `2000` (₹20.00)
  - `net_cash_paise`: `8000` (₹80.00 net in drawer)

### Scenario 2: ₹100 cash + ₹40 UPI split payment
- **Action:** Invoice total ₹140.00 (`14000` paise). Customer pays ₹100 cash and ₹40 via GooglePay.
- **Ledger Entries:**
  - `Payment 1`: `amount_paise = 10000`, `is_refund = 0`, `payment_method = 'CASH'`
  - `Payment 2`: `amount_paise = 4000`, `is_refund = 0`, `payment_method = 'UPI'`
- **SQL Query (Payment Method Breakdown):**
```sql
SELECT 
    payment_method,
    SUM(amount_paise) AS collected_paise
FROM transaction_payments
WHERE transaction_id = 'TX-1002' AND is_refund = 0
GROUP BY payment_method;
```
- **Expected Output:**
  - `CASH`: `10000` (₹100.00)
  - `UPI`: `4000` (₹40.00)
  - Total Paid: `14000` (₹140.00), Invoice status: `PAID`, Balance due: `0`.

### Scenario 3: ₹140 invoice with ₹100 paid and ₹40 outstanding
- **Action:** Invoice net total ₹140.00. Customer pays ₹100 cash; ₹40 is owed.
- **Ledger Entries:**
  - Transaction: `net_total_paise = 14000`, `paid_paise = 10000`, `balance_paise = 4000`, `status = 'PARTIAL'`
  - `Payment 1`: `amount_paise = 10000`, `is_refund = 0`, `payment_method = 'CASH'`
- **SQL Query (Outstanding Receivables Report):**
```sql
SELECT 
    transaction_id, customer_name, net_total_paise, paid_paise, balance_paise, status
FROM business_transactions
WHERE status IN ('PENDING', 'PARTIAL') AND is_test_mode = 0;
```
- **Expected Output:**
  - `TX-1003 | Customer A | 14000 | 10000 | 4000 | PARTIAL`
  - Balance due correctly surfaces as ₹40.00 outstanding.

### Scenario 4: Older outstanding invoice paid today
- **Action:** Invoice `TX-0901` generated 9 days ago with ₹40 outstanding. Customer visits today and pays ₹40 cash.
- **Ledger Entries:**
  - Transaction updated: `paid_paise = 14000`, `balance_paise = 0`, `status = 'PAID'`, `updated_at = now()`
  - New Payment inserted: `amount_paise = 4000`, `payment_method = 'CASH'`, `collected_at = date('now')`
- **SQL Query (Audit Trail & Today's Cash Inflow):**
```sql
SELECT 
    p.payment_id, p.transaction_id, t.created_at AS invoice_date, p.collected_at AS payment_date, p.amount_paise
FROM transaction_payments p
JOIN business_transactions t ON p.transaction_id = t.transaction_id
WHERE p.collected_at >= @StartUtcString 
  AND p.collected_at <= @EndUtcString 
  AND p.transaction_id = 'TX-0901';
```
- **Expected Output:**
  - Invoice Date: `2026-11-10T10:00:00Z`
  - Payment Date: `2026-11-19T09:15:00Z`
  - Today's Cash Drawer correctly captures the ₹40 cash inflow despite the invoice having been created earlier.

### Scenario 5: Voided invoice with previous payment and refund
- **Action:** Invoice ₹70.00 paid cash, printed card is destroyed by operator; invoice is voided and ₹70.00 refunded.
- **Ledger Entries:**
  - Transaction: `status = 'VOID'`, `balance_paise = 0`
  - `Payment 1`: `amount_paise = 7000`, `is_refund = 0`, `payment_method = 'CASH'`
  - `Payment 2`: `amount_paise = 7000`, `is_refund = 1`, `payment_method = 'CASH'`
- **SQL Query (Net Revenue vs Drawer Verification):**
```sql
SELECT 
    t.status,
    COALESCE(SUM(CASE WHEN p.is_refund = 0 THEN p.amount_paise ELSE -p.amount_paise END), 0) AS net_cash_effect
FROM business_transactions t
LEFT JOIN transaction_payments p ON t.transaction_id = p.transaction_id
WHERE t.transaction_id = 'TX-VOID-01'
GROUP BY t.status;
```
- **Expected Output:**
  - `VOID | 0 net_cash_effect`
  - Revenue report excludes the transaction; cash drawer correctly accounts for +₹70 in, -₹70 out.

---

## 6. Total Environment Test Mode Sandbox Isolation

### 6.1 Centralized Database Path Resolution
All database interactions route through a centralized path provider interface:

```csharp
public interface IDatabasePathProvider
{
    bool IsTestMode { get; }
    string DatabasePath { get; }
    string ConfigDirectory { get; }
    string ExportDirectory { get; }
    void SetTestMode(bool enabled);
}
```

### 6.2 Physical Directory & Path Separation
| Asset Category | Production Mode Path / Resource | Test Mode Sandbox Path / Resource |
| :--- | :--- | :--- |
| **Financial Database** | `%LOCALAPPDATA%\PVCStudioPro\pvc_studio.db` | `%LOCALAPPDATA%\PVCStudioPro\TestMode\pvc_studio_test.db` |
| **Shop Settings** | `%LOCALAPPDATA%\PVCStudioPro\business_profile.json` | `%LOCALAPPDATA%\PVCStudioPro\TestMode\business_profile.json` |
| **Printer Calibration**| `%LOCALAPPDATA%\PVCStudioPro\printer_profiles.json`| `%LOCALAPPDATA%\PVCStudioPro\TestMode\printer_profiles.json` |
| **Invoice Numbers** | Sequence: `INV-YYYYMMDD-XXXX` (Permanent) | Sequence: `TEST-XXXX` (Disposable) |
| **Exported Files** | `%USERPROFILE%\Documents\PVC Studio Pro\Exports\` | `%LOCALAPPDATA%\PVCStudioPro\TestMode\Exports\` |

### 6.3 Enforceable Defensive Assertions
To prevent catastrophic accidental data loss, destructive reset actions enforce strict path validation:

```csharp
public void ResetTestDatabase()
{
    string activeDb = _pathProvider.DatabasePath;
    if (!activeDb.Contains("TestMode", StringComparison.OrdinalIgnoreCase) || 
        !Path.GetFileName(activeDb).Equals("pvc_studio_test.db", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "FATAL SAFETY VIOLATION: Attempted to reset database when not running in TestMode sandbox!");
    }

    File.Delete(activeDb);
}
```
