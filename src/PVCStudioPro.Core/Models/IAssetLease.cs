using System;
using SkiaSharp;

namespace PVCStudioPro.Core.Models
{
    /// <summary>
    /// Represents a short-lived, thread-safe lease on an unmanaged raster asset.
    /// Disposing the lease decrements the reference count on the underlying native handle.
    /// Accessing the surface after lease disposal throws ObjectDisposedException.
    /// </summary>
    public interface IAssetLease : IDisposable
    {
        /// <summary>
        /// Gets the immutable, non-disposable raster surface bound to this lease.
        /// </summary>
        IReadOnlyRasterSurface Surface { get; }

        /// <summary>
        /// Gets the unique identifier of the leased asset.
        /// </summary>
        string AssetId { get; }

        /// <summary>
        /// Gets the revision number of the leased asset.
        /// </summary>
        int Revision { get; }

        /// <summary>
        /// Gets a value indicating whether this lease has been disposed.
        /// </summary>
        bool IsDisposed { get; }
    }

    /// <summary>
    /// Common contract for image assets that manage unmanaged native bitmap resources
    /// through thread-safe reference counting and leased surface access.
    /// </summary>
    public interface ISharedAsset : IDisposable
    {
        /// <summary>
        /// Gets the unique identifier of the asset.
        /// </summary>
        string AssetId { get; }

        /// <summary>
        /// Gets the revision number of the asset.
        /// </summary>
        int Revision { get; }

        /// <summary>
        /// Gets the active reference count of the asset.
        /// </summary>
        int ReferenceCount { get; }

        /// <summary>
        /// Gets a value indicating whether this asset has been disposed.
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Acquires a thread-safe lease on the asset, incrementing its reference count.
        /// </summary>
        /// <returns>An active asset lease that must be disposed when the operation completes.</returns>
        IAssetLease AcquireLease();

        /// <summary>
        /// Renders the asset directly to the destination canvas within the specified bounding rectangle.
        /// </summary>
        /// <param name="canvas">The destination Skia canvas.</param>
        /// <param name="destRect">The destination bounding rectangle.</param>
        /// <param name="paint">Optional paint properties.</param>
        void Render(SKCanvas canvas, SKRect destRect, SKPaint? paint = null);
    }
}
