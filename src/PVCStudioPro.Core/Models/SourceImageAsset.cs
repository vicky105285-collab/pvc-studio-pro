using System;
using SkiaSharp;

namespace PVCStudioPro.Core.Models
{
    /// <summary>
    /// Represents an immutable root source image asset loaded from an imported document or raster file.
    /// Manages the native unmanaged bitmap through a SafeNativeBitmap handle.
    /// </summary>
    public sealed class SourceImageAsset : ISharedAsset
    {
        private readonly SafeNativeBitmap _safeHandle;

        /// <summary>
        /// Initializes a new instance of the SourceImageAsset class.
        /// Takes initial ownership of the provided SafeNativeBitmap.
        /// </summary>
        /// <param name="assetId">Unique asset identifier.</param>
        /// <param name="sourceFilePath">Original file path on disk.</param>
        /// <param name="fileKind">Detected file format kind.</param>
        /// <param name="nativeDpi">Native resolution in dots per inch.</param>
        /// <param name="safeHandle">The reference-counted safe native bitmap handle.</param>
        public SourceImageAsset(
            string assetId,
            string sourceFilePath,
            FileMagicKind fileKind,
            int nativeDpi,
            SafeNativeBitmap safeHandle)
        {
            AssetId = string.IsNullOrWhiteSpace(assetId) ? Guid.NewGuid().ToString("N") : assetId;
            SourceFilePath = sourceFilePath ?? string.Empty;
            FileKind = fileKind;
            NativeDpi = nativeDpi > 0 ? nativeDpi : 300;
            _safeHandle = safeHandle ?? throw new ArgumentNullException(nameof(safeHandle));
            LoadedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Factory method to create a SourceImageAsset directly from an unmanaged SKBitmap.
        /// Takes ownership of the bitmap.
        /// </summary>
        public static SourceImageAsset Create(
            string sourceFilePath,
            SKBitmap bitmap,
            int nativeDpi = 300,
            FileMagicKind fileKind = FileMagicKind.Unknown,
            string? assetId = null)
        {
            var handle = new SafeNativeBitmap(bitmap);
            return new SourceImageAsset(assetId ?? Guid.NewGuid().ToString("N"), sourceFilePath, fileKind, nativeDpi, handle);
        }

        /// <inheritdoc/>
        public string AssetId { get; }

        /// <summary>
        /// Gets the original source file path on disk.
        /// </summary>
        public string SourceFilePath { get; }

        /// <summary>
        /// Gets the detected file format kind.
        /// </summary>
        public FileMagicKind FileKind { get; }

        /// <summary>
        /// Gets the native resolution in dots per inch.
        /// </summary>
        public int NativeDpi { get; }

        /// <summary>
        /// Gets the width of the source asset in pixels.
        /// </summary>
        public int Width => _safeHandle.Width;

        /// <summary>
        /// Gets the height of the source asset in pixels.
        /// </summary>
        public int Height => _safeHandle.Height;

        /// <summary>
        /// Gets the physical width in millimeters.
        /// </summary>
        public double PhysicalWidthMm => NativeDpi > 0 ? (Width * 25.4) / NativeDpi : 0.0;

        /// <summary>
        /// Gets the physical height in millimeters.
        /// </summary>
        public double PhysicalHeightMm => NativeDpi > 0 ? (Height * 25.4) / NativeDpi : 0.0;

        /// <summary>
        /// Gets the UTC timestamp when this asset was decoded into memory.
        /// </summary>
        public DateTime LoadedAtUtc { get; }

        /// <inheritdoc/>
        public int Revision => 0;

        /// <inheritdoc/>
        public int ReferenceCount => _safeHandle.ReferenceCount;

        /// <inheritdoc/>
        public bool IsDisposed => _safeHandle.IsOwnerDisposed;

        /// <inheritdoc/>
        public IAssetLease AcquireLease()
        {
            return _safeHandle.AcquireLease(AssetId, Revision, NativeDpi);
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
        }
    }
}
