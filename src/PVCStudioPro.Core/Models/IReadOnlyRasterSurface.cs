using System;
using SkiaSharp;

namespace PVCStudioPro.Core.Models
{
    /// <summary>
    /// Scoped action delegate for protected pixel inspection.
    /// The unmanaged pixel memory remains locked and pinned for the duration of this callback.
    /// </summary>
    /// <param name="pixelBytes">Read-only span of raw pixel bytes.</param>
    /// <param name="rowBytes">Number of bytes per row in the pixel buffer.</param>
    public delegate void ReadOnlyPixelSpanAction(ReadOnlySpan<byte> pixelBytes, int rowBytes);

    /// <summary>
    /// Scoped function delegate for protected pixel inspection with a return value.
    /// The unmanaged pixel memory remains locked and pinned for the duration of this callback.
    /// </summary>
    /// <typeparam name="TResult">The type of the computed result.</typeparam>
    /// <param name="pixelBytes">Read-only span of raw pixel bytes.</param>
    /// <param name="rowBytes">Number of bytes per row in the pixel buffer.</param>
    /// <returns>The computed result.</returns>
    public delegate TResult ReadOnlyPixelSpanFunc<out TResult>(ReadOnlySpan<byte> pixelBytes, int rowBytes);

    /// <summary>
    /// Represents an immutable, non-disposable read-only raster surface.
    /// Consumers can render the surface, create independent immutable snapshots, or inspect pixels
    /// through scoped callbacks without direct access to mutable SKBitmap objects or raw unmanaged pointers.
    /// </summary>
    public interface IReadOnlyRasterSurface
    {
        /// <summary>
        /// Unique identifier of the underlying asset.
        /// </summary>
        string AssetId { get; }

        /// <summary>
        /// Revision index of the underlying asset.
        /// </summary>
        int Revision { get; }

        /// <summary>
        /// Width of the raster surface in pixels.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Height of the raster surface in pixels.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Native resolution in dots per inch.
        /// </summary>
        int Dpi { get; }

        /// <summary>
        /// Physical width in millimeters computed from Width and Dpi.
        /// </summary>
        double WidthMm => Dpi > 0 ? (Width * 25.4) / Dpi : 0.0;

        /// <summary>
        /// Physical height in millimeters computed from Height and Dpi.
        /// </summary>
        double HeightMm => Dpi > 0 ? (Height * 25.4) / Dpi : 0.0;

        /// <summary>
        /// Renders the surface to the target canvas within the destination rectangle.
        /// Throws ObjectDisposedException if the owning lease has been disposed.
        /// </summary>
        /// <param name="canvas">The destination Skia canvas.</param>
        /// <param name="destRect">The destination bounding rectangle.</param>
        /// <param name="paint">Optional paint properties.</param>
        void RenderTo(SKCanvas canvas, SKRect destRect, SKPaint? paint = null);

        /// <summary>
        /// Creates an independent, immutable SKImage snapshot.
        /// The returned SKImage is owned by the caller and must be disposed by the caller.
        /// Throws ObjectDisposedException if the owning lease has been disposed.
        /// </summary>
        /// <returns>An immutable SKImage snapshot.</returns>
        SKImage CreateImmutableSnapshot();

        /// <summary>
        /// Inspects pixel bytes under a native memory lock for the duration of the callback.
        /// The span cannot escape the callback's call stack.
        /// Throws ObjectDisposedException if the owning lease has been disposed.
        /// </summary>
        /// <param name="action">Action to execute with read-only access to pixel bytes.</param>
        void ReadPixels(ReadOnlyPixelSpanAction action);

        /// <summary>
        /// Inspects pixel bytes under a native memory lock for the duration of the callback and returns a result.
        /// The span cannot escape the callback's call stack.
        /// Throws ObjectDisposedException if the owning lease has been disposed.
        /// </summary>
        /// <typeparam name="TResult">The type of the computed result.</typeparam>
        /// <param name="func">Function to execute with read-only access to pixel bytes.</param>
        /// <returns>The computed result.</returns>
        TResult ReadPixels<TResult>(ReadOnlyPixelSpanFunc<TResult> func);
    }
}
