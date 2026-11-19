using System;
using SkiaSharp;

namespace PVCStudioPro.Core.Models
{
    /// <summary>
    /// Represents an immutable derived image asset generated from an image transform operation
    /// (e.g. card crop, perspective warp, rotation, or enhancement).
    /// Holds its own SafeNativeBitmap and an optional leased reference to its parent asset.
    /// </summary>
    public sealed class DerivedImageAsset : ISharedAsset
    {
        private readonly SafeNativeBitmap _safeHandle;
        private readonly IAssetLease? _parentLease;

        /// <summary>
        /// Initializes a new instance of the DerivedImageAsset class.
        /// Takes ownership of the provided SafeNativeBitmap handle.
        /// </summary>
        /// <param name="assetId">Unique identifier of this derived asset.</param>
        /// <param name="parentAssetId">Identifier of the parent asset from which this was derived.</param>
        /// <param name="revision">Revision sequence number.</param>
        /// <param name="dpi">Target resolution in dots per inch.</param>
        /// <param name="transformDescription">Human-readable description of the applied transform.</param>
        /// <param name="safeHandle">Safe native bitmap handle for the derived pixel buffer.</param>
        /// <param name="parentLease">Optional parent lease retained to keep parent lineage alive.</param>
        public DerivedImageAsset(
            string assetId,
            string parentAssetId,
            int revision,
            int dpi,
            string transformDescription,
            SafeNativeBitmap safeHandle,
            IAssetLease? parentLease = null)
        {
            AssetId = string.IsNullOrWhiteSpace(assetId) ? Guid.NewGuid().ToString("N") : assetId;
            ParentAssetId = parentAssetId ?? string.Empty;
            Revision = revision;
            Dpi = dpi > 0 ? dpi : 300;
            TransformDescription = transformDescription ?? "Derived";
            _safeHandle = safeHandle ?? throw new ArgumentNullException(nameof(safeHandle));
            _parentLease = parentLease;
            CreatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Factory method to create a DerivedImageAsset directly from a newly rendered SKBitmap.
        /// Takes ownership of the bitmap.
        /// </summary>
        public static DerivedImageAsset Create(
            string parentAssetId,
            int revision,
            int dpi,
            string transformDescription,
            SKBitmap bitmap,
            IAssetLease? parentLease = null,
            string? assetId = null)
        {
            var handle = new SafeNativeBitmap(bitmap);
            return new DerivedImageAsset(
                assetId ?? Guid.NewGuid().ToString("N"),
                parentAssetId,
                revision,
                dpi,
                transformDescription,
                handle,
                parentLease);
        }

        /// <inheritdoc/>
        public string AssetId { get; }

        /// <summary>
        /// Gets the identifier of the parent asset from which this revision was derived.
        /// </summary>
        public string ParentAssetId { get; }

        /// <inheritdoc/>
        public int Revision { get; }

        /// <summary>
        /// Gets the target resolution in dots per inch.
        /// </summary>
        public int Dpi { get; }

        /// <summary>
        /// Gets the description of the transformation that produced this revision.
        /// </summary>
        public string TransformDescription { get; }

        /// <summary>
        /// Gets the width of this derived asset in pixels.
        /// </summary>
        public int Width => _safeHandle.Width;

        /// <summary>
        /// Gets the height of this derived asset in pixels.
        /// </summary>
        public int Height => _safeHandle.Height;

        /// <summary>
        /// Gets the physical width in millimeters.
        /// </summary>
        public double PhysicalWidthMm => Dpi > 0 ? (Width * 25.4) / Dpi : 0.0;

        /// <summary>
        /// Gets the physical height in millimeters.
        /// </summary>
        public double PhysicalHeightMm => Dpi > 0 ? (Height * 25.4) / Dpi : 0.0;

        /// <summary>
        /// Gets a value indicating whether the derived dimensions conform to ISO/IEC 7810 CR80 standard
        /// (85.60 mm x 53.98 mm within 0.3 mm tolerance).
        /// </summary>
        public bool IsCr80Standard =>
            Math.Abs(PhysicalWidthMm - 85.60) < 0.3 &&
            Math.Abs(PhysicalHeightMm - 53.98) < 0.3;

        /// <summary>
        /// Gets the UTC creation timestamp of this revision.
        /// </summary>
        public DateTime CreatedAtUtc { get; }

        /// <inheritdoc/>
        public int ReferenceCount => _safeHandle.ReferenceCount;

        /// <inheritdoc/>
        public bool IsDisposed => _safeHandle.IsOwnerDisposed;

        /// <inheritdoc/>
        public IAssetLease AcquireLease()
        {
            return _safeHandle.AcquireLease(AssetId, Revision, Dpi);
        }

        /// <inheritdoc/>
        public void Render(SKCanvas canvas, SKRect destRect, SKPaint? paint = null)
        {
            using var lease = AcquireLease();
            lease.Surface.RenderTo(canvas, destRect, paint);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _safeHandle.Dispose();
            _parentLease?.Dispose();
        }
    }
}
