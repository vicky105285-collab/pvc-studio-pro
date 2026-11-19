using System;

namespace PVCStudioPro.Core.Models
{
    /// <summary>
    /// Isolated lifetime-managed state for an open document in PVC Studio Pro.
    /// Encapsulates root immutable source assets, derived cropped assets, independent revision history,
    /// and per-workspace subscription channels without cross-document state leaks.
    /// </summary>
    public interface IDocumentSession : IDisposable
    {
        string SessionId { get; }
        string SourceFilePath { get; }
        string FileName { get; }
        string DocumentType { get; set; }
        int ActiveRevision { get; }
        CropSettings FrontCropSettings { get; }
        CropSettings BackCropSettings { get; }
        SourceImageAsset SourceAsset { get; }
        DerivedImageAsset? FrontCardAsset { get; }
        DerivedImageAsset? BackCardAsset { get; }
        UndoRedoManager History { get; }
        long TotalNativeMemoryBytes { get; }
        bool IsDisposed { get; }

        IDisposable Subscribe(IObserver<DocumentChange> observer);
        IDisposable Subscribe(Action<DocumentChange> onNext);

        void UpdateCropSettings(CropSettings? frontCrop, CropSettings? backCrop);
        void SetCardAssets(DerivedImageAsset? frontAsset, DerivedImageAsset? backAsset);
        void CommitRevision(
            string description,
            DerivedImageAsset? frontAsset = null,
            DerivedImageAsset? backAsset = null,
            CropSettings? frontCrop = null,
            CropSettings? backCrop = null);

        bool Undo();
        bool Redo();
    }
}
