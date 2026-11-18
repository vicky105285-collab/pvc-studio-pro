# Security, Privacy & Data Protection Architecture — PVC Studio Pro (Revised)

**Target Compliance:** Indian Digital Personal Data Protection (DPDP) Act 2023, Aadhaar Act 2016, UIDAI Masked Aadhaar Directives  
**Scope:** Commercial Desktop Software Operations in Indian e-Sevai / CSC Environments  
**Status:** Revised & Hardened (Zero-PII Storage Architecture)  
**Date:** 2026-11-18  

---

## 1. The Zero-Citizen-PII Storage Principle

Following our senior architecture review, we have overhauled the persistence model to eliminate legal and regulatory exposure for the e-Sevai centre operator:

> [!IMPORTANT] Core Privacy Rule
> **PVC Studio Pro does NOT store, persist, or retain citizen personal identity data (Aadhaar numbers, names, phone numbers, addresses, biometric photos, or raw PDFs) in its database or on disk.**

### What Is Stored:
- Only non-sensitive accounting and operational metrics:
  - `JobId` (UUID v4)
  - `JobNumber` (e.g. `INV-2026-0042`)
  - `Timestamp`
  - `DocumentType` (e.g. "AADHAAR", "PAN", "PHOTO_STUDIO")
  - `Quantity`
  - `AmountCharged` (e.g. ₹70.00)
  - `PaymentMode` ("CASH" / "UPI")
  - `PrinterProfileUsed`
  - `ExecutionStatus` ("SUCCESS" / "FAILED")

By excluding citizen names and 12-digit Aadhaar numbers from permanent storage, the e-Sevai centre operator carries **zero liability** under DPDP Act 2023 penalties regarding data breaches or unauthorized citizen profiling.

---

## 2. Hardened Memory Safety & Unmanaged Zeroization

### Why Managed `MemoryStream` Overwriting Is Insufficient:
In .NET managed code, garbage collection compaction moves objects in memory, leaving stale ghost copies in previous RAM locations. Furthermore, allocations exceeding 85,000 bytes land on the Large Object Heap (LOH), and the Windows virtual memory manager can swap dirty pages to `pagefile.sys`.

### Hardened Implementation in PVC Studio Pro:
```csharp
// Unmanaged Pinned Memory Allocation
IntPtr unmanagedBuffer = Marshal.AllocHGlobal(bufferSize);
try {
    // 1. Lock memory page to prevent Windows from paging to pagefile.sys
    VirtualLock(unmanagedBuffer, (UIntPtr)bufferSize);

    // 2. Perform PDFium rendering directly into unmanaged memory
    RenderPdfDirectToNative(unmanagedBuffer, bufferSize);

    // 3. Dispatch raw bitmap bytes to Windows Spooler
    SpoolerWrite(unmanagedBuffer, bufferSize);
}
finally {
    // 4. Cryptographic Zeroization using Win32 SecureZeroMemory
    SecureZeroMemory(unmanagedBuffer, (UIntPtr)bufferSize);

    // 5. Unlock and free unmanaged allocation
    VirtualUnlock(unmanagedBuffer, (UIntPtr)bufferSize);
    Marshal.FreeHGlobal(unmanagedBuffer);
}
```

1. **Unmanaged Allocation:** Uses `Marshal.AllocHGlobal` or `NativeMemory.Alloc`.
2. **`VirtualLock`:** Pinches the memory pages into physical RAM, prohibiting the OS from writing citizen identity bytes to `pagefile.sys` on the hard drive.
3. **`SecureZeroMemory`:** Calls the native Windows kernel API to overwrite every byte with zeros, immune to compiler dead-code elimination.
4. **Immediate Deallocation:** Released immediately upon print job dispatch.

---

## 3. Ephemeral In-Memory Reprint Window (Opt-In)

If an operator enables "Quick Reprint" (in case a plastic card jams in the tray):
- The decrypted card remains **strictly in physical RAM**.
- An automated watchdog timer zeroizes and frees the buffer after **15 minutes** or upon application exit.
- At no point is the decrypted card written to Windows `%TEMP%` or application directories.
