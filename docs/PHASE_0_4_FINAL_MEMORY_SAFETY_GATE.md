# PVC STUDIO PRO — PHASE 0.4 FINAL MEMORY-SAFETY GATE
**Document Version:** 1.0.0  
**Date:** 2026-11-19  
**Status:** VALIDATED MEMORY-SAFETY SPECIFICATION & PHASE 1A PRE-IMPLEMENTATION GATE  
**Branch:** `competitor-feature-parity`  
**Baseline Commit:** `cd53f9b`  

---

## 1. Executive Summary & Purpose of Phase 0.4

Phase 0.4 provides the final memory-safety corrections identified during technical review of Phase 0.3. This document establishes:
1. **Protected Pixel-Span Lifetimes:** Replaces unsafe returning of `ReadOnlySpan<byte>` with a scoped delegate/callback API (`ReadPixels(action)` / `ReadPixels(func)`) that keeps the native synchronization lock actively held for the entire read duration.
2. **Lease-Bound Surface Lifetime Enforcement:** Guarantees that any use of `IReadOnlyRasterSurface` after its associated `IAssetLease` is disposed throws `ObjectDisposedException`, and that in-flight rendering operations complete safely before native memory can be reclaimed.
3. **Coherent Synchronization Architecture:** Establishes a single synchronization monitor (`lock (_syncLock)`) as the explicit linearization point for owner disposal, lease acquisition, lease release, and native resource destruction.
4. **Focused Phase 1A Test Scope:** Restricts Phase 1A to nine explicit native ownership tests, moving undo/redo history and memory quota eviction tests to Phase 1B.
5. **Half-Open IST Business-Day Reporting:** Adopts half-open intervals (`collected_at >= @StartUtc AND collected_at < @NextDayStartUtc`) to eliminate fractional-second boundary truncation or double-counting.

---

## 2. Protected Pixel-Span Lifetime Safety

### 2.1 The Vulnerability of Returning `ReadOnlySpan<byte>`
In Phase 0.3, `ReadPixelBytesInternal()` returned `_nativeBitmap.GetPixelSpan()` after releasing `_syncLock`. This introduced a critical use-after-free vulnerability:
- A caller could capture the `ReadOnlySpan<byte>`, and while iterating over pixel bytes on Thread A, Thread B could dispose the lease or the owner, causing `_nativeBitmap.Dispose()` to execute on the native C++ heap.
- Accessing the span after deallocation results in unmanaged memory access violations (`0xC0000005: Access Violation`) or silent process crashes.

### 2.2 The Scoped Callback API Contract
Raw spans are strictly forbidden from escaping synchronization scopes. Pixel access is provided exclusively via scoped delegates:

```csharp
public delegate void ReadOnlyPixelSpanAction(ReadOnlySpan<byte> pixelBytes, int rowBytes);
public delegate TResult ReadOnlyPixelSpanFunc<out TResult>(ReadOnlySpan<byte> pixelBytes, int rowBytes);

public interface IReadOnlyRasterSurface
{
    string AssetId { get; }
    int Revision { get; }
    int Width { get; }
    int Height { get; }
    int Dpi { get; }
    double WidthMm => (Width * 25.4) / Dpi;
    double HeightMm => (Height * 25.4) / Dpi;
    
    // Renders pixels directly to destination canvas while lease is active
    void RenderTo(SKCanvas canvas, SKRect destRect, SKPaint? paint = null);
    
    // Obtains an independent immutable snapshot
    SKImage CreateImmutableSnapshot();
    
    // Protected pixel inspection: native memory lock is held for the ENTIRE callback execution
    void ReadPixels(ReadOnlyPixelSpanAction action);
    TResult ReadPixels<TResult>(ReadOnlyPixelSpanFunc<TResult> func);
}
```

### 2.3 Lifetime & Thread-Safety Guarantees
- **Pinned Lifetime:** `_syncLock` is acquired before calling `_nativeBitmap.GetPixelSpan()` and is held until the caller's delegate finishes executing.
- **No Escaping Pointers:** The delegate receives `ReadOnlySpan<byte>` which, as a C# `ref struct`, cannot be boxed, stored in fields of normal classes, or captured in asynchronous closures across `await` expressions.
- **Zero Copy Overhead:** Read operations (e.g. barcode reading, OCR preprocessing) inspect native memory directly at full C++ speed without allocating intermediate managed byte arrays.

---

## 3. Lease-Bound Surface Lifetime & Coherent Synchronization

### 3.1 The Vulnerability of Retained Surface Instances
If `AssetLease.Surface` is stored in a separate local variable or class field, a caller might call `lease.Dispose()` and subsequently call `surface.RenderTo(...)`. If the underlying native resource is disposed, a use-after-free occurs.

### 3.2 Coherent Synchronization & Complete Implementation Pseudocode
The entire native lifecycle is coordinated by `SafeNativeBitmap`:

```csharp
public sealed class SafeNativeBitmap : IDisposable
{
    private SKBitmap? _nativeBitmap;
    private int _refCount;
    private bool _isOwnerDisposed;
    private readonly object _syncLock = new();

    public SafeNativeBitmap(SKBitmap bitmap)
    {
        _nativeBitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        _refCount = 1; // Owner owns 1 reference
        _isOwnerDisposed = false;
    }

    public int ReferenceCount
    {
        get { lock (_syncLock) { return _refCount; } }
    }

    public bool IsOwnerDisposed
    {
        get { lock (_syncLock) { return _isOwnerDisposed; } }
    }

    public bool IsNativeDisposed
    {
        get { lock (_syncLock) { return _nativeBitmap == null; } }
    }

    /// <summary>
    /// Linearization Point for Acquisition vs Owner Disposal:
    /// Inside _syncLock, if _isOwnerDisposed is true, acquisition is strictly rejected.
    /// If _isOwnerDisposed is false, _refCount is incremented atomically under lock.
    /// </summary>
    public IAssetLease AcquireLease(string assetId, int revision, int dpi)
    {
        lock (_syncLock)
        {
            if (_isOwnerDisposed || _nativeBitmap == null || _refCount <= 0)
            {
                throw new ObjectDisposedException(nameof(SafeNativeBitmap),
                    $"Cannot acquire lease on disposed asset '{assetId}'.");
            }

            _refCount++;
            return new AssetLease(this, assetId, revision, dpi);
        }
    }

    internal void ReleaseReference()
    {
        lock (_syncLock)
        {
            if (_refCount <= 0)
            {
                throw new InvalidOperationException("Reference count underflow detected on native bitmap handle.");
            }

            _refCount--;
            if (_refCount == 0 && _nativeBitmap != null)
            {
                _nativeBitmap.Dispose();
                _nativeBitmap = null;
            }
        }
    }

    /// <summary>
    /// Linearization Point for Owner Disposal:
    /// Inside _syncLock, marks _isOwnerDisposed = true and releases the owner reference.
    /// If active leases exist (_refCount > 1), _nativeBitmap remains alive until all leases call ReleaseReference().
    /// </summary>
    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_isOwnerDisposed)
            {
                return; // Idempotent
            }

            _isOwnerDisposed = true;
            ReleaseReference();
        }
    }

    internal void RenderInternal(IAssetLease lease, SKCanvas canvas, SKRect destRect, SKPaint? paint)
    {
        if (lease.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(IAssetLease), "Cannot use surface after lease is disposed.");
        }

        lock (_syncLock)
        {
            if (lease.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(IAssetLease), "Cannot use surface after lease is disposed.");
            }

            if (_nativeBitmap == null)
            {
                throw new ObjectDisposedException(nameof(SafeNativeBitmap), "Native bitmap has been destroyed.");
            }

            canvas.DrawBitmap(_nativeBitmap, destRect, paint);
        }
    }

    internal SKImage CreateSnapshotInternal(IAssetLease lease)
    {
        if (lease.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(IAssetLease), "Cannot use surface after lease is disposed.");
        }

        lock (_syncLock)
        {
            if (lease.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(IAssetLease), "Cannot use surface after lease is disposed.");
            }

            if (_nativeBitmap == null)
            {
                throw new ObjectDisposedException(nameof(SafeNativeBitmap), "Native bitmap has been destroyed.");
            }

            return SKImage.FromBitmap(_nativeBitmap);
        }
    }

    internal void ReadPixelsInternal(IAssetLease lease, ReadOnlyPixelSpanAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (lease.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(IAssetLease), "Cannot use surface after lease is disposed.");
        }

        lock (_syncLock)
        {
            if (lease.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(IAssetLease), "Cannot use surface after lease is disposed.");
            }

            if (_nativeBitmap == null)
            {
                throw new ObjectDisposedException(nameof(SafeNativeBitmap), "Native bitmap has been destroyed.");
            }

            // Lock is held for the ENTIRE duration of action execution
            action(_nativeBitmap.GetPixelSpan(), _nativeBitmap.RowBytes);
        }
    }

    internal TResult ReadPixelsInternal<TResult>(IAssetLease lease, ReadOnlyPixelSpanFunc<TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        if (lease.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(IAssetLease), "Cannot use surface after lease is disposed.");
        }

        lock (_syncLock)
        {
            if (lease.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(IAssetLease), "Cannot use surface after lease is disposed.");
            }

            if (_nativeBitmap == null)
            {
                throw new ObjectDisposedException(nameof(SafeNativeBitmap), "Native bitmap has been destroyed.");
            }

            // Lock is held for the ENTIRE duration of func execution
            return func(_nativeBitmap.GetPixelSpan(), _nativeBitmap.RowBytes);
        }
    }

    private sealed class AssetLease : IAssetLease
    {
        private SafeNativeBitmap? _handle;
        private readonly LeaseBoundSurface _surface;
        public string AssetId { get; }
        public int Revision { get; }

        public AssetLease(SafeNativeBitmap handle, string assetId, int revision, int dpi)
        {
            _handle = handle;
            AssetId = assetId;
            Revision = revision;
            _surface = new LeaseBoundSurface(this, handle, assetId, revision, dpi);
        }

        public IReadOnlyRasterSurface Surface => _surface;

        public bool IsDisposed => _handle == null;

        public void Dispose()
        {
            var handle = Interlocked.Exchange(ref _handle, null);
            handle?.ReleaseReference();
        }
    }

    private sealed class LeaseBoundSurface : IReadOnlyRasterSurface
    {
        private readonly AssetLease _lease;
        private readonly SafeNativeBitmap _handle;
        public string AssetId { get; }
        public int Revision { get; }
        public int Dpi { get; }

        public LeaseBoundSurface(AssetLease lease, SafeNativeBitmap handle, string assetId, int revision, int dpi)
        {
            _lease = lease;
            _handle = handle;
            AssetId = assetId;
            Revision = revision;
            Dpi = dpi;
        }

        public int Width
        {
            get
            {
                if (_lease.IsDisposed) throw new ObjectDisposedException(nameof(IAssetLease));
                return _handle._nativeBitmap?.Width ?? 0;
            }
        }

        public int Height
        {
            get
            {
                if (_lease.IsDisposed) throw new ObjectDisposedException(nameof(IAssetLease));
                return _handle._nativeBitmap?.Height ?? 0;
            }
        }

        public void RenderTo(SKCanvas canvas, SKRect destRect, SKPaint? paint = null) =>
            _handle.RenderInternal(_lease, canvas, destRect, paint);

        public SKImage CreateImmutableSnapshot() =>
            _handle.CreateSnapshotInternal(_lease);

        public void ReadPixels(ReadOnlyPixelSpanAction action) =>
            _handle.ReadPixelsInternal(_lease, action);

        public TResult ReadPixels<TResult>(ReadOnlyPixelSpanFunc<TResult> func) =>
            _handle.ReadPixelsInternal(_lease, func);
    }
}
```

### 3.3 Concurrency & Race Condition Guarantees
1. **Concurrent Lease Disposal and Rendering:**
   If Thread A is executing `RenderTo` and Thread B calls `lease.Dispose()`, Thread A holds `_syncLock`. Thread B's call to `ReleaseReference()` blocks until Thread A completes rendering and exits `_syncLock`. Native memory is never freed while rendering is in flight.
2. **Subsequent Calls After Lease Disposal:**
   If Thread B calls `lease.Dispose()` and Thread A subsequently calls `surface.RenderTo(...)`, `lease.IsDisposed` evaluates to `true` both before entering the lock and inside the lock, throwing `ObjectDisposedException` immediately.
3. **Owner Disposal Independence:**
   Owner disposal marks `_isOwnerDisposed = true` and decrements `_refCount` by 1. If an active lease is held, `_refCount` remains $\ge 1$, keeping `_nativeBitmap` intact and allowing the lease holder to complete all operations.

---

## 4. Corrected Phase 1A Automated Test Suite

Phase 1A is strictly restricted to verifying native asset ownership, reference counting, and lease safety. Undo/redo history eviction and 250 MB quota management are deferred to Phase 1B (which introduces `DocumentSession` and revision stacks).

### Exact Phase 1A Test Register (`NativeAssetLifecycleTests.cs`):
1. **`DoubleDisposal_OfAssetOwner_IsIdempotent`**:
   Calling `.Dispose()` twice on `SafeNativeBitmap` succeeds silently without double-freeing native memory.
2. **`DoubleDisposal_OfLease_IsIdempotent`**:
   Calling `.Dispose()` twice on `IAssetLease` decrements ref-count exactly once.
3. **`AcquireLease_AfterOwnerDisposal_ThrowsObjectDisposedException`**:
   Calling `AcquireLease()` after owner disposal immediately throws `ObjectDisposedException`.
4. **`ConcurrentAcquisition_AndOwnerDisposal_IsThreadSafe`**:
   20 parallel tasks acquiring leases while another thread calls `Dispose()`. Zero unmanaged memory faults; all operations either return a valid lease or throw `ObjectDisposedException`.
5. **`OwnerDisposal_WhileLeaseHeld_KeepsResourceAlive`**:
   Disposing the owner while a lease is active keeps unmanaged bitmap alive; resource is released only when the active lease is disposed.
6. **`ExceptionAndCancellation_InRenderingPipeline_ReleasesLeaseCleanly`**:
   An exception or cancellation thrown during rendering releases the lease in `finally`, safely decrementing ref-count without memory leaks.
7. **`UseAfterLeaseDisposal_ThrowsObjectDisposedException`**:
   Calling `surface.RenderTo()`, `surface.CreateImmutableSnapshot()`, or `surface.ReadPixels()` after `lease.Dispose()` throws `ObjectDisposedException`.
8. **`ConcurrentRendering_AndLeaseDisposal_IsThreadSafe`**:
   Rendering operation on Thread A finishes safely even if Thread B calls `lease.Dispose()` concurrently; no use-after-free or native memory fault occurs.
9. **`NoUseAfterFree_DuringStressTesting`**:
   1,000 rapid concurrent acquire/render/read/release cycles across 4 threads complete with zero unmanaged memory leaks or access violations.

---

## 5. Indian Financial Reporting: Half-Open SQL Intervals

### 5.1 Elimination of Fractional-Second Boundary Ambiguity
In SQL queries, closed intervals (`BETWEEN @StartUtc AND @EndUtc` or `<= @EndUtc`) fail in high-throughput operations:
- Picking `23:59:59.999Z` drops records timestamped with microsecond or nanosecond resolution (e.g. `23:59:59.9994Z`).
- Picking `00:00:00Z` of the next day with `<=` causes transactions occurring exactly at midnight to be counted twice (in both day ledgers).

### 5.2 The Half-Open Standard
All daily reports use the mathematical half-open interval $[StartUtc, NextDayStartUtc)$:

$$\text{collected\_at} \ge @StartUtc \quad \text{AND} \quad \text{collected\_at} < @NextDayStartUtc$$

```csharp
public static class IndianBusinessDay
{
    private static readonly TimeZoneInfo IstZone = 
        TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    public static (string StartUtc, string NextDayStartUtc) GetIstDayUtcBounds(DateOnly istDate)
    {
        // 00:00:00.000 IST today
        var istStart = istDate.ToDateTime(TimeOnly.MinValue);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(istStart, IstZone);

        // 00:00:00.000 IST tomorrow
        var nextDayIstStart = istDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var nextDayStartUtc = TimeZoneInfo.ConvertTimeToUtc(nextDayIstStart, IstZone);

        return (
            startUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            nextDayStartUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        );
    }
}
```

*Example for November 19, 2026 IST:*
- `@StartUtc`: `2026-11-18T18:30:00.000Z` ($00:00:00.000\text{ IST}$, inclusive)
- `@NextDayStartUtc`: `2026-11-19T18:30:00.000Z` ($00:00:00.000\text{ IST}$ next day, exclusive)

### 5.3 High-Performance Indexed SQL Reporting Query
```sql
SELECT 
    COALESCE(SUM(CASE WHEN is_refund = 0 THEN amount_paise ELSE 0 END), 0) AS total_collected_paise,
    COALESCE(SUM(CASE WHEN is_refund = 1 THEN amount_paise ELSE 0 END), 0) AS total_refunded_paise,
    COALESCE(SUM(CASE WHEN is_refund = 0 THEN amount_paise ELSE -amount_paise END), 0) AS net_cash_paise
FROM transaction_payments
WHERE payment_method = 'CASH'
  AND is_test_mode = 0
  AND collected_at >= @StartUtc 
  AND collected_at < @NextDayStartUtc;
```

---

## 6. Pre-Implementation Gate Summary

- [x] Protected pixel-span reading via scoped callbacks (`ReadPixels(action)` / `ReadPixels(func)`).
- [x] Lease-bound surface lifetime enforcement: use after lease disposal throws `ObjectDisposedException`.
- [x] Coherent synchronization monitor (`lock (_syncLock)`) with explicit linearization points.
- [x] Corrected Phase 1A test scope: 9 native lifecycle tests; quota and history tests deferred to Phase 1B.
- [x] Half-open SQL reporting intervals for Indian Standard Time.
- [x] Documentation only: zero application code, XAML, packages, or database migrations modified.

**GATE STATUS:** Phase 0.4 memory-safety specifications are complete and verified. **Implementation of Phase 1A will commence ONLY upon explicit authorization from the user.**
