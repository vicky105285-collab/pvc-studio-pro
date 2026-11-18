# ADR-004: Embedded SQLite Database with Windows DPAPI Encryption

**Status:** Accepted  
**Date:** 2026-11-18  
**Deciders:** Senior Windows Desktop Software Architect  

---

## Context
Commercial e-Sevai operations require tracking daily transactions, revenue, customer print jobs, and reprint histories. However, handling citizen identity information creates severe legal exposure under the Indian Digital Personal Data Protection (DPDP) Act 2023 and UIDAI regulations. We need a zero-maintenance local database engine that safeguards citizen privacy.

## Decision
We select **SQLite 3** via Microsoft.Data.Sqlite and Dapper, combined with **Windows DPAPI** field-level encryption for sensitive citizen PII.

## Rationale
1. **Zero Operational Overhead:** SQLite is a self-contained, serverless database engine stored in a single file (`pvc_studio.db`). It requires no background service, no administrator setup, and never fails due to port conflicts or network dropouts.
2. **ACID Transactions:** Ensures that print jobs, revenue tallies, and daily register updates are recorded atomically.
3. **Data Protection API (DPAPI):**
   - Storing citizen names, phone numbers, or card identifiers in plaintext violates DPDP security safeguard mandates.
   - Using `ProtectedData.Protect(data, optionalEntropy, DataProtectionScope.CurrentUser)`, sensitive fields are encrypted with the logged-in Windows user's cryptographic keys. If the database file is copied to another machine, the sensitive fields cannot be decrypted.
4. **Automated Purging:** Configurable data retention policies (e.g. 24 hours, 7 days, or never) automatically purge customer records, preventing accidental long-term data accumulation.

## Consequences
- Clean, fast, and compliant persistence infrastructure.
- Automated daily backup snapshots (`pvc_studio_backup_YYYYMMDD.db`) created with one click.
