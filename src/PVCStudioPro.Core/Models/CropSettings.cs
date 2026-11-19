using System;
using SkiaSharp;

namespace PVCStudioPro.Core.Models
{
    /// <summary>
    /// Geometric and transform configuration for an isolated document crop region.
    /// Supports aspect ratio locking (CR80 standard), rotation, DPI metadata, and perspective quad warping.
    /// </summary>
    public sealed class CropSettings : IEquatable<CropSettings>
    {
        public const double Cr80AspectRatio = 85.60 / 53.98; // ~1.5857725

        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float RotationDegrees { get; set; }
        public int Dpi { get; set; } = 300;
        public bool IsAspectRatioLocked { get; set; } = true;
        public double TargetAspectRatio { get; set; } = Cr80AspectRatio;
        public SKPoint[]? PerspectiveQuad { get; set; }

        public bool IsEmpty => Width <= 0 || Height <= 0;

        public SKRect ToRect() => new(X, Y, X + Width, Y + Height);

        public CropSettings Clone()
        {
            SKPoint[]? quadClone = null;
            if (PerspectiveQuad != null)
            {
                quadClone = new SKPoint[PerspectiveQuad.Length];
                Array.Copy(PerspectiveQuad, quadClone, PerspectiveQuad.Length);
            }

            return new CropSettings
            {
                X = X,
                Y = Y,
                Width = Width,
                Height = Height,
                RotationDegrees = RotationDegrees,
                Dpi = Dpi,
                IsAspectRatioLocked = IsAspectRatioLocked,
                TargetAspectRatio = TargetAspectRatio,
                PerspectiveQuad = quadClone
            };
        }

        public bool Equals(CropSettings? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return X == other.X &&
                   Y == other.Y &&
                   Width == other.Width &&
                   Height == other.Height &&
                   Math.Abs(RotationDegrees - other.RotationDegrees) < 0.001f &&
                   Dpi == other.Dpi &&
                   IsAspectRatioLocked == other.IsAspectRatioLocked &&
                   Math.Abs(TargetAspectRatio - other.TargetAspectRatio) < 0.0001;
        }

        public override bool Equals(object? obj) => Equals(obj as CropSettings);

        public override int GetHashCode() =>
            HashCode.Combine(X, Y, Width, Height, (int)(RotationDegrees * 100), Dpi, IsAspectRatioLocked);
    }
}
