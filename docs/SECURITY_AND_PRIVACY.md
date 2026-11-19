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

---

## 4. Windows DPAPI Scope & Limitations

### What Windows DPAPI Protects:
- **Local Application Secrets:** If an operator configures an administrative password, custom cloud backup credentials, or proprietary printer profile calibration locks, these are encrypted via `ProtectedData.Protect` using `DataProtectionScope.CurrentUser`.
- **Key Derivation:** DPAPI binds ciphertext directly to the Windows machine user account credentials through the Windows Local Security Authority Subsystem Service (LSASS).

### What DPAPI Does NOT Protect:
- **Transient In-Flight Memory:** DPAPI is an encryption-at-rest mechanism. It does not protect active bytes in volatile memory or graphics pipeline buffers while rendering.
- **Cross-Account Process Snooping:** An attacker with local Administrator privileges or a kernel driver on the host machine can attach a debugger to inspect process memory.
- **Conclusion:** DPAPI is strictly scoped for persisting operational credentials and offline configuration. Citizen identity documents are intentionally **never persisted to disk**, rendering DPAPI at-rest encryption unnecessary for customer document payloads.

---

## 5. Volatile Memory Scrubbing Realistic Engineering Boundaries

In compliance with independent senior architectural standards:
- **No False Guarantees:** PVC Studio Pro does not claim that zeroization guarantees complete eradication of all copies from system RAM under every conceivable operating condition.
- **Managed Runtime Limits:** Any managed `byte[]` or `string` allocated on the .NET heap before unmanaged pinning may be relocated during garbage collection heap compaction before zeroization occurs.
- **Hardware & Driver Buffers:** Print commands submitted to the Windows Print Spooler (`winspool.drv`) and vendor graphics printer drivers (e.g. Epson `e_farn0e.dll`) allocate driver-internal spool files (`.SPL`) and hardware FIFO buffers outside application control.
- **Defense-in-Depth Mitigation:** PVC Studio Pro minimizes document lifespan, avoids disk writes, zeroizes unmanaged buffers with `VirtualLock` and `SecureZeroMemory`, and clears UI ImageSource references immediately upon print completion.
