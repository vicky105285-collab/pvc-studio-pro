using System;
using System.Collections.Generic;
using System.Linq;

namespace PVCStudioPro.Core.Models
{
    /// <summary>
    /// Thread-safe undo/redo revision manager enforcing a 15-revision depth limit
    /// and a 250 MB aggregate native memory ceiling per document session.
    /// Safely disposes evicted native bitmaps and unlinked redo branches.
    /// </summary>
    public sealed class UndoRedoManager : IDisposable
    {
        public const int MaxUndoDepth = 15;
        public const long MaxNativeMemoryQuotaBytes = 250L * 1024 * 1024; // 250 MB

        private readonly object _syncLock = new();
        private readonly LinkedList<DocumentRevisionState> _undoStack = new();
        private readonly Stack<DocumentRevisionState> _redoStack = new();
        private DocumentRevisionState? _currentState;
        private bool _isDisposed;

        public sealed class DocumentRevisionState : IDisposable
        {
            public int RevisionNumber { get; }
            public string Description { get; }
            public DerivedImageAsset? FrontCardAsset { get; }
            public DerivedImageAsset? BackCardAsset { get; }
            public CropSettings FrontCropSettings { get; }
            public CropSettings BackCropSettings { get; }
            public long NativeMemoryBytes { get; }

            public DocumentRevisionState(
                int revisionNumber,
                string description,
                DerivedImageAsset? frontCardAsset,
                DerivedImageAsset? backCardAsset,
                CropSettings frontCropSettings,
                CropSettings backCropSettings)
            {
                RevisionNumber = revisionNumber;
                Description = description;
                FrontCardAsset = frontCardAsset;
                BackCardAsset = backCardAsset;
                FrontCropSettings = frontCropSettings.Clone();
                BackCropSettings = backCropSettings.Clone();

                long frontBytes = frontCardAsset != null ? (long)frontCardAsset.Width * frontCardAsset.Height * 4 : 0;
                long backBytes = backCardAsset != null ? (long)backCardAsset.Width * backCardAsset.Height * 4 : 0;
                NativeMemoryBytes = frontBytes + backBytes;
            }

            public void Dispose()
            {
                FrontCardAsset?.Dispose();
                BackCardAsset?.Dispose();
            }
        }

        public int CurrentRevision => _currentState?.RevisionNumber ?? 0;
        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public long TotalNativeMemoryBytes
        {
            get
            {
                lock (_syncLock)
                {
                    long total = _currentState?.NativeMemoryBytes ?? 0;
                    foreach (var state in _undoStack)
                    {
                        total += state.NativeMemoryBytes;
                    }
                    foreach (var state in _redoStack)
                    {
                        total += state.NativeMemoryBytes;
                    }
                    return total;
                }
            }
        }

        public int HistoryCount
        {
            get
            {
                lock (_syncLock)
                {
                    return _undoStack.Count;
                }
            }
        }

        public DocumentRevisionState? CurrentState
        {
            get
            {
                lock (_syncLock)
                {
                    return _currentState;
                }
            }
        }

        /// <summary>
        /// Pushes a new revision state into the history.
        /// Any existing redo branch is immediately abandoned and disposed.
        /// If depth exceeds 15 or memory exceeds 250 MB, oldest revisions are evicted and disposed.
        /// </summary>
        public void PushRevision(
            int revisionNumber,
            string description,
            DerivedImageAsset? frontCardAsset,
            DerivedImageAsset? backCardAsset,
            CropSettings frontCropSettings,
            CropSettings backCropSettings)
        {
            lock (_syncLock)
            {
                ThrowIfDisposed();

                // 1. Abandon and dispose all redo states on branch diverge
                while (_redoStack.Count > 0)
                {
                    var abandoned = _redoStack.Pop();
                    abandoned.Dispose();
                }

                // 2. Archive current state to undo stack if present
                if (_currentState != null)
                {
                    _undoStack.AddLast(_currentState);
                }

                // 3. Set current state
                _currentState = new DocumentRevisionState(
                    revisionNumber,
                    description,
                    frontCardAsset,
                    backCardAsset,
                    frontCropSettings,
                    backCropSettings);

                // 4. Enforce quota: depth limit (MaxUndoDepth = 15)
                while (_undoStack.Count > MaxUndoDepth)
                {
                    var oldest = _undoStack.First!.Value;
                    _undoStack.RemoveFirst();
                    oldest.Dispose();
                }

                // 5. Enforce quota: 250 MB native memory ceiling
                EnforceMemoryQuota();
            }
        }

        /// <summary>
        /// Reverts to the previous revision in history.
        /// Moves current state to redo stack and returns previous state.
        /// </summary>
        public DocumentRevisionState? Undo()
        {
            lock (_syncLock)
            {
                ThrowIfDisposed();

                if (_undoStack.Count == 0 || _currentState == null)
                {
                    return null;
                }

                _redoStack.Push(_currentState);
                _currentState = _undoStack.Last!.Value;
                _undoStack.RemoveLast();

                return _currentState;
            }
        }

        /// <summary>
        /// Advances to the next revision in history.
        /// Moves current state to undo stack and returns next state.
        /// </summary>
        public DocumentRevisionState? Redo()
        {
            lock (_syncLock)
            {
                ThrowIfDisposed();

                if (_redoStack.Count == 0 || _currentState == null)
                {
                    return null;
                }

                _undoStack.AddLast(_currentState);
                _currentState = _redoStack.Pop();

                EnforceMemoryQuota();
                return _currentState;
            }
        }

        private void EnforceMemoryQuota()
        {
            // Evict from oldest undo entries if aggregate memory exceeds 250 MB
            while (TotalNativeMemoryBytes > MaxNativeMemoryQuotaBytes && _undoStack.Count > 0)
            {
                var oldest = _undoStack.First!.Value;
                _undoStack.RemoveFirst();
                oldest.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(UndoRedoManager), "UndoRedoManager has been disposed.");
            }
        }

        public void Dispose()
        {
            lock (_syncLock)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;

                _currentState?.Dispose();
                _currentState = null;

                while (_undoStack.Count > 0)
                {
                    var state = _undoStack.First!.Value;
                    _undoStack.RemoveFirst();
                    state.Dispose();
                }

                while (_redoStack.Count > 0)
                {
                    var state = _redoStack.Pop();
                    state.Dispose();
                }
            }
        }
    }
}
