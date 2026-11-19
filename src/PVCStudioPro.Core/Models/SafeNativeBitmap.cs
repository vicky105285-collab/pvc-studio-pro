using System;
using System.Threading;
using SkiaSharp;

namespace PVCStudioPro.Core.Models
{
    /// <summary>
    /// Thread-safe, reference-counted lifetime manager for unmanaged SkiaSharp bitmap memory.
    /// Encapsulates native SKBitmap allocations so that no raw native pointer, mutable bitmap,
    /// or unmanaged span escapes protected scopes.
    /// </summary>
    public sealed class SafeNativeBitmap : IDisposable
    {
        private SKBitmap? _nativeBitmap;
        private int _refCount;
        private bool _isOwnerDisposed;
        private int _activeOperationsCount;
        private readonly object _syncLock = new();

        /// <summary>
        /// Initializes a new instance of SafeNativeBitmap taking initial ownership of the bitmap.
        /// </summary>
        /// <param name="bitmap">The unmanaged SkiaSharp bitmap.</param>
        public SafeNativeBitmap(SKBitmap bitmap)
        {
            _nativeBitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
            _refCount = 1; // Owner owns 1 reference
            _isOwnerDisposed = false;
            _activeOperationsCount = 0;
        }

        /// <summary>
        /// Gets the active reference count on this native bitmap handle.
        /// </summary>
        public int ReferenceCount
        {
            get
            {
                lock (_syncLock)
                {
                    return _refCount;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the owner has released its reference.
        /// </summary>
        public bool IsOwnerDisposed
        {
            get
            {
                lock (_syncLock)
                {
                    return _isOwnerDisposed;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the underlying native unmanaged bitmap has been destroyed.
        /// </summary>
        public bool IsNativeDisposed
        {
            get
            {
                lock (_syncLock)
                {
                    return _nativeBitmap == null;
                }
            }
        }

        /// <summary>
        /// Gets the width of the native bitmap under synchronization lock.
        /// </summary>
        public int Width
        {
            get
            {
                lock (_syncLock)
                {
                    if (_nativeBitmap == null)
                    {
                        throw new ObjectDisposedException(nameof(SafeNativeBitmap), "Native bitmap has been destroyed.");
                    }
                    return _nativeBitmap.Width;
                }
            }
        }

        /// <summary>
        /// Gets the height of the native bitmap under synchronization lock.
        /// </summary>
        public int Height
        {
            get
            {
                lock (_syncLock)
                {
                    if (_nativeBitmap == null)
                    {
                        throw new ObjectDisposedException(nameof(SafeNativeBitmap), "Native bitmap has been destroyed.");
                    }
                    return _nativeBitmap.Height;
                }
            }
        }

        /// <summary>
        /// Linearization Point for Lease Acquisition vs. Owner Disposal:
        /// Inside _syncLock, if _isOwnerDisposed is true or the bitmap is destroyed, acquisition fails.
        /// Otherwise, _refCount is incremented and an active IAssetLease is returned.
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

        /// <summary>
        /// Releases a reference on this handle.
        /// If both reference count and active in-flight operations reach zero, destroys the unmanaged bitmap.
        /// </summary>
        internal void ReleaseReference()
        {
            lock (_syncLock)
            {
                if (_refCount <= 0)
                {
                    throw new InvalidOperationException("Reference count underflow detected on native bitmap handle.");
                }

                _refCount--;
                CheckAndDestroyNativeResource();
            }
        }

        /// <summary>
        /// Linearization Point for Owner Disposal:
        /// Marks the owner as disposed under _syncLock and releases the initial reference.
        /// Active leases continue to keep the native resource alive until they are released.
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

        /// <summary>
        /// Destroys the native bitmap if reference count is zero AND no active operations are executing.
        /// MUST be called while holding _syncLock.
        /// </summary>
        private void CheckAndDestroyNativeResource()
        {
            if (_refCount == 0 && _activeOperationsCount == 0 && _nativeBitmap != null)
            {
                _nativeBitmap.Dispose();
                _nativeBitmap = null;
            }
        }

        /// <summary>
        /// Executes an internal rendering operation safely under operation-lifetime tracking.
        /// </summary>
        internal void RenderInternal(IAssetLease lease, SKCanvas canvas, SKRect destRect, SKPaint? paint)
        {
            ArgumentNullException.ThrowIfNull(lease);
            ArgumentNullException.ThrowIfNull(canvas);

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

                _activeOperationsCount++;
                try
                {
                    canvas.DrawBitmap(_nativeBitmap, destRect, SKSamplingOptions.Default, paint);
                }
                finally
                {
                    _activeOperationsCount--;
                    CheckAndDestroyNativeResource();
                }
            }
        }

        /// <summary>
        /// Creates an independent immutable SKImage snapshot under synchronization lock.
        /// </summary>
        internal SKImage CreateSnapshotInternal(IAssetLease lease)
        {
            ArgumentNullException.ThrowIfNull(lease);

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

                _activeOperationsCount++;
                try
                {
                    return SKImage.FromBitmap(_nativeBitmap);
                }
                finally
                {
                    _activeOperationsCount--;
                    CheckAndDestroyNativeResource();
                }
            }
        }

        /// <summary>
        /// Executes a scoped pixel-span inspection action under synchronization lock.
        /// The native memory is guaranteed pinned and protected for the entire execution duration.
        /// </summary>
        internal void ReadPixelsInternal(IAssetLease lease, ReadOnlyPixelSpanAction action)
        {
            ArgumentNullException.ThrowIfNull(lease);
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

                _activeOperationsCount++;
                try
                {
                    // Lock is actively held for the ENTIRE duration of action execution
                    action(_nativeBitmap.GetPixelSpan(), _nativeBitmap.RowBytes);
                }
                finally
                {
                    _activeOperationsCount--;
                    CheckAndDestroyNativeResource();
                }
            }
        }

        /// <summary>
        /// Executes a scoped pixel-span inspection function with return value under synchronization lock.
        /// The native memory is guaranteed pinned and protected for the entire execution duration.
        /// </summary>
        internal TResult ReadPixelsInternal<TResult>(IAssetLease lease, ReadOnlyPixelSpanFunc<TResult> func)
        {
            ArgumentNullException.ThrowIfNull(lease);
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

                _activeOperationsCount++;
                try
                {
                    // Lock is actively held for the ENTIRE duration of func execution
                    return func(_nativeBitmap.GetPixelSpan(), _nativeBitmap.RowBytes);
                }
                finally
                {
                    _activeOperationsCount--;
                    CheckAndDestroyNativeResource();
                }
            }
        }

        /// <summary>
        /// Gets surface dimensions under synchronization lock.
        /// </summary>
        internal (int Width, int Height) GetDimensionsInternal(IAssetLease lease)
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

                return (_nativeBitmap.Width, _nativeBitmap.Height);
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
                _handle = handle ?? throw new ArgumentNullException(nameof(handle));
                AssetId = assetId;
                Revision = revision;
                _surface = new LeaseBoundSurface(this, handle, assetId, revision, dpi);
            }

            public IReadOnlyRasterSurface Surface => _surface;

            public bool IsDisposed => Volatile.Read(ref _handle) == null;

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
                Dpi = dpi > 0 ? dpi : 300;
            }

            public int Width => _handle.GetDimensionsInternal(_lease).Width;

            public int Height => _handle.GetDimensionsInternal(_lease).Height;

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
}
