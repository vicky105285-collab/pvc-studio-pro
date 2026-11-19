using System;
using System.Collections.Generic;
using System.IO;

namespace PVCStudioPro.Core.Models
{
    /// <summary>
    /// Thread-safe concrete implementation of IDocumentSession.
    /// Manages isolated document state, immutable root assets, versioned derived cards,
    /// and leak-free workspace subscriptions.
    /// </summary>
    public sealed class DocumentSession : IDocumentSession
    {
        private readonly object _syncLock = new();
        private readonly List<IObserver<DocumentChange>> _observers = new();
        private bool _isDisposed;
        private int _revisionCounter;

        public string SessionId { get; }
        public string SourceFilePath { get; }
        public string FileName { get; }
        public string DocumentType { get; set; }
        public SourceImageAsset SourceAsset { get; }
        public UndoRedoManager History { get; }

        public CropSettings FrontCropSettings { get; private set; }
        public CropSettings BackCropSettings { get; private set; }
        public DerivedImageAsset? FrontCardAsset { get; private set; }
        public DerivedImageAsset? BackCardAsset { get; private set; }

        public int ActiveRevision
        {
            get
            {
                lock (_syncLock)
                {
                    return _revisionCounter;
                }
            }
        }

        public bool IsDisposed
        {
            get
            {
                lock (_syncLock)
                {
                    return _isDisposed;
                }
            }
        }

        public long TotalNativeMemoryBytes
        {
            get
            {
                lock (_syncLock)
                {
                    long sourceBytes = (long)SourceAsset.Width * SourceAsset.Height * 4;
                    long frontBytes = FrontCardAsset != null ? (long)FrontCardAsset.Width * FrontCardAsset.Height * 4 : 0;
                    long backBytes = BackCardAsset != null ? (long)BackCardAsset.Width * BackCardAsset.Height * 4 : 0;
                    return sourceBytes + frontBytes + backBytes + History.TotalNativeMemoryBytes;
                }
            }
        }

        public DocumentSession(string sourceFilePath, SourceImageAsset sourceAsset, string? documentType = null, string? sessionId = null)
        {
            SourceFilePath = sourceFilePath ?? throw new ArgumentNullException(nameof(sourceFilePath));
            SourceAsset = sourceAsset ?? throw new ArgumentNullException(nameof(sourceAsset));
            FileName = Path.GetFileName(sourceFilePath);
            DocumentType = documentType ?? "unknown";
            SessionId = sessionId ?? Guid.NewGuid().ToString("N");

            History = new UndoRedoManager();
            FrontCropSettings = new CropSettings();
            BackCropSettings = new CropSettings();
            _revisionCounter = 0;

            // Initialize baseline revision in history
            History.PushRevision(
                _revisionCounter,
                "Initial Document Load",
                null,
                null,
                FrontCropSettings,
                BackCropSettings);
        }

        public IDisposable Subscribe(IObserver<DocumentChange> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            lock (_syncLock)
            {
                if (!_isDisposed && !_observers.Contains(observer))
                {
                    _observers.Add(observer);
                }
                return new Unsubscriber(_observers, observer, _syncLock);
            }
        }

        public IDisposable Subscribe(Action<DocumentChange> onNext)
        {
            ArgumentNullException.ThrowIfNull(onNext);
            return Subscribe(new DelegateObserver(onNext));
        }

        public void UpdateCropSettings(CropSettings? frontCrop, CropSettings? backCrop)
        {
            List<IObserver<DocumentChange>> observersCopy;
            DocumentChange change;

            lock (_syncLock)
            {
                ThrowIfDisposed();

                if (frontCrop != null)
                {
                    FrontCropSettings = frontCrop.Clone();
                }
                if (backCrop != null)
                {
                    BackCropSettings = backCrop.Clone();
                }

                change = new DocumentChange(SessionId, _revisionCounter, "CropSettingsChanged", "Updated crop coordinates");
                observersCopy = new List<IObserver<DocumentChange>>(_observers);
            }

            NotifyObservers(observersCopy, change);
        }

        public void SetCardAssets(DerivedImageAsset? frontAsset, DerivedImageAsset? backAsset)
        {
            List<IObserver<DocumentChange>> observersCopy;
            DocumentChange change;

            lock (_syncLock)
            {
                ThrowIfDisposed();

                FrontCardAsset = frontAsset;
                BackCardAsset = backAsset;

                change = new DocumentChange(SessionId, _revisionCounter, "CardAssetsUpdated", "Updated extracted card faces");
                observersCopy = new List<IObserver<DocumentChange>>(_observers);
            }

            NotifyObservers(observersCopy, change);
        }

        public void CommitRevision(
            string description,
            DerivedImageAsset? frontAsset = null,
            DerivedImageAsset? backAsset = null,
            CropSettings? frontCrop = null,
            CropSettings? backCrop = null)
        {
            List<IObserver<DocumentChange>> observersCopy;
            DocumentChange change;

            lock (_syncLock)
            {
                ThrowIfDisposed();

                _revisionCounter++;

                if (frontCrop != null) FrontCropSettings = frontCrop.Clone();
                if (backCrop != null) BackCropSettings = backCrop.Clone();
                if (frontAsset != null) FrontCardAsset = frontAsset;
                if (backAsset != null) BackCardAsset = backAsset;

                History.PushRevision(
                    _revisionCounter,
                    description,
                    FrontCardAsset,
                    BackCardAsset,
                    FrontCropSettings,
                    BackCropSettings);

                change = new DocumentChange(SessionId, _revisionCounter, "RevisionCommitted", description);
                observersCopy = new List<IObserver<DocumentChange>>(_observers);
            }

            NotifyObservers(observersCopy, change);
        }

        public bool Undo()
        {
            List<IObserver<DocumentChange>> observersCopy;
            DocumentChange change;

            lock (_syncLock)
            {
                ThrowIfDisposed();

                var previous = History.Undo();
                if (previous == null)
                {
                    return false;
                }

                _revisionCounter = previous.RevisionNumber;
                FrontCropSettings = previous.FrontCropSettings.Clone();
                BackCropSettings = previous.BackCropSettings.Clone();
                FrontCardAsset = previous.FrontCardAsset;
                BackCardAsset = previous.BackCardAsset;

                change = new DocumentChange(SessionId, _revisionCounter, "Undo", $"Reverted to revision {previous.RevisionNumber}: {previous.Description}");
                observersCopy = new List<IObserver<DocumentChange>>(_observers);
            }

            NotifyObservers(observersCopy, change);
            return true;
        }

        public bool Redo()
        {
            List<IObserver<DocumentChange>> observersCopy;
            DocumentChange change;

            lock (_syncLock)
            {
                ThrowIfDisposed();

                var next = History.Redo();
                if (next == null)
                {
                    return false;
                }

                _revisionCounter = next.RevisionNumber;
                FrontCropSettings = next.FrontCropSettings.Clone();
                BackCropSettings = next.BackCropSettings.Clone();
                FrontCardAsset = next.FrontCardAsset;
                BackCardAsset = next.BackCardAsset;

                change = new DocumentChange(SessionId, _revisionCounter, "Redo", $"Advanced to revision {next.RevisionNumber}: {next.Description}");
                observersCopy = new List<IObserver<DocumentChange>>(_observers);
            }

            NotifyObservers(observersCopy, change);
            return true;
        }

        private static void NotifyObservers(List<IObserver<DocumentChange>> observers, DocumentChange change)
        {
            foreach (var observer in observers)
            {
                try
                {
                    observer.OnNext(change);
                }
                catch
                {
                    // Observers must not crash session state transitions
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(DocumentSession), $"Document session '{SessionId}' has been disposed.");
            }
        }

        public void Dispose()
        {
            List<IObserver<DocumentChange>> observersCopy;

            lock (_syncLock)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                observersCopy = new List<IObserver<DocumentChange>>(_observers);
                _observers.Clear();

                History.Dispose();
                FrontCardAsset?.Dispose();
                BackCardAsset?.Dispose();
                SourceAsset.Dispose();
            }

            foreach (var observer in observersCopy)
            {
                try
                {
                    observer.OnCompleted();
                }
                catch
                {
                    // Suppress observer exceptions during cleanup
                }
            }
        }

        private sealed class Unsubscriber : IDisposable
        {
            private readonly List<IObserver<DocumentChange>> _observers;
            private readonly IObserver<DocumentChange> _observer;
            private readonly object _syncLock;

            public Unsubscriber(List<IObserver<DocumentChange>> observers, IObserver<DocumentChange> observer, object syncLock)
            {
                _observers = observers;
                _observer = observer;
                _syncLock = syncLock;
            }

            public void Dispose()
            {
                lock (_syncLock)
                {
                    _observers.Remove(_observer);
                }
            }
        }

        private sealed class DelegateObserver : IObserver<DocumentChange>
        {
            private readonly Action<DocumentChange> _onNext;

            public DelegateObserver(Action<DocumentChange> onNext) => _onNext = onNext;

            public void OnNext(DocumentChange value) => _onNext(value);
            public void OnError(Exception error) { }
            public void OnCompleted() { }
        }
    }
}
