using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    /// <summary>
    /// Thread-safe multi-document session manager.
    /// Manages open sessions, atomic tab switching, cancelable document ingestion,
    /// and unmanaged resource lifecycle management.
    /// </summary>
    public sealed class DocumentSessionManager : IDocumentSessionManager
    {
        private readonly object _syncLock = new();
        private readonly List<IDocumentSession> _sessions = new();
        private IDocumentSession? _activeSession;
        private bool _isDisposed;

        private readonly IDocumentImportService _importService;
        private readonly IPdfDocumentService _pdfService;
        private readonly IDocumentTypeDetectionService? _detectionService;

        public event Action<SessionEvent>? SessionChanged;

        public IReadOnlyList<IDocumentSession> Sessions
        {
            get
            {
                lock (_syncLock)
                {
                    return _sessions.ToList();
                }
            }
        }

        public IDocumentSession? ActiveSession
        {
            get
            {
                lock (_syncLock)
                {
                    return _activeSession;
                }
            }
        }

        public DocumentSessionManager(
            IDocumentImportService? importService = null,
            IPdfDocumentService? pdfService = null,
            IDocumentTypeDetectionService? detectionService = null)
        {
            _importService = importService ?? new DocumentImportService();
            _pdfService = pdfService ?? new PdfService();
            _detectionService = detectionService;
        }

        public async Task<IDocumentSession> OpenDocumentAsync(
            string filePath,
            string? password = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must not be null or empty.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Document file not found at: {filePath}", filePath);
            }

            // 1. Check if file is already open
            lock (_syncLock)
            {
                ThrowIfDisposed();
                var existing = _sessions.FirstOrDefault(s => string.Equals(s.SourceFilePath, filePath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    SwitchActiveSession(existing.SessionId);
                    return existing;
                }
            }

            SKBitmap? rawBitmap = null;
            SourceImageAsset? sourceAsset = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 2. Sniff document type
                var sniff = _importService.SniffFile(filePath);

                if (sniff.MagicKind == FileMagicKind.Pdf)
                {
                    await using var stream = File.OpenRead(filePath);
                    rawBitmap = await _pdfService.RenderPageAsync(stream, 0, dpi: 300, password, cancellationToken);
                }
                else
                {
                    // Raster image format (PNG, JPG, BMP, TIFF, WebP)
                    rawBitmap = await Task.Run(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        using var fileStream = File.OpenRead(filePath);
                        return SKBitmap.Decode(fileStream);
                    }, cancellationToken);

                    if (rawBitmap == null)
                    {
                        throw new InvalidDataException($"Failed to decode image data from file: {filePath}");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // 3. Create root immutable native asset
                sourceAsset = SourceImageAsset.Create(filePath, rawBitmap, nativeDpi: 300, fileKind: sniff.MagicKind);
                rawBitmap = null; // Ownership transferred to SourceImageAsset

                // 4. Document classification heuristic if available
                string documentType = "generic";
                if (_detectionService != null)
                {
                    try
                    {
                        using var lease = sourceAsset.AcquireLease();
                        using var snapshot = lease.Surface.CreateImmutableSnapshot();
                        using var snapshotBmp = SKBitmap.FromImage(snapshot);
                        var detection = await _detectionService.DetectDocumentTypeAsync(snapshotBmp, string.Empty);
                        documentType = detection.DetectedKind.ToString().ToLowerInvariant();
                    }
                    catch
                    {
                        // Fallback to generic
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // 5. Create new session and add to registry
                var session = new DocumentSession(filePath, sourceAsset, documentType);
                sourceAsset = null; // Ownership transferred to DocumentSession

                lock (_syncLock)
                {
                    ThrowIfDisposed();
                    _sessions.Add(session);
                    _activeSession = session;
                }

                // 6. Broadcast lifecycle events
                SessionChanged?.Invoke(new SessionEvent(SessionEventType.Opened, session.SessionId, session, $"Opened document {session.FileName}"));
                SessionChanged?.Invoke(new SessionEvent(SessionEventType.ActiveChanged, session.SessionId, session, $"Switched active session to {session.FileName}"));

                return session;
            }
            catch
            {
                // Unwind and cleanly release native memory if loading failed or was cancelled
                rawBitmap?.Dispose();
                sourceAsset?.Dispose();
                throw;
            }
        }

        public void SwitchActiveSession(string sessionId)
        {
            IDocumentSession? newActive = null;

            lock (_syncLock)
            {
                ThrowIfDisposed();
                if (_activeSession?.SessionId == sessionId)
                {
                    return; // Already active
                }

                newActive = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
                if (newActive != null)
                {
                    _activeSession = newActive;
                }
            }

            if (newActive != null)
            {
                SessionChanged?.Invoke(new SessionEvent(SessionEventType.ActiveChanged, newActive.SessionId, newActive, $"Switched active session to {newActive.FileName}"));
            }
        }

        public void CloseSession(string sessionId)
        {
            IDocumentSession? toClose = null;
            IDocumentSession? nextActive = null;

            lock (_syncLock)
            {
                ThrowIfDisposed();
                toClose = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
                if (toClose == null)
                {
                    return;
                }

                _sessions.Remove(toClose);

                if (_activeSession?.SessionId == sessionId)
                {
                    _activeSession = _sessions.LastOrDefault();
                    nextActive = _activeSession;
                }
            }

            toClose.Dispose();
            SessionChanged?.Invoke(new SessionEvent(SessionEventType.Closed, sessionId, null, $"Closed session {sessionId}"));

            if (nextActive != null)
            {
                SessionChanged?.Invoke(new SessionEvent(SessionEventType.ActiveChanged, nextActive.SessionId, nextActive, $"Switched active session to {nextActive.FileName}"));
            }
            else if (_sessions.Count == 0)
            {
                SessionChanged?.Invoke(new SessionEvent(SessionEventType.ActiveChanged, string.Empty, null, "No active sessions remain"));
            }
        }

        public void CloseAllSessions()
        {
            List<IDocumentSession> sessionsCopy;

            lock (_syncLock)
            {
                ThrowIfDisposed();
                sessionsCopy = new List<IDocumentSession>(_sessions);
                _sessions.Clear();
                _activeSession = null;
            }

            foreach (var session in sessionsCopy)
            {
                session.Dispose();
            }

            SessionChanged?.Invoke(new SessionEvent(SessionEventType.Disposed, string.Empty, null, "All document sessions closed"));
        }

        public IDocumentSession? GetSession(string sessionId)
        {
            lock (_syncLock)
            {
                return _sessions.FirstOrDefault(s => s.SessionId == sessionId);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(DocumentSessionManager), "DocumentSessionManager has been disposed.");
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
                foreach (var session in _sessions)
                {
                    session.Dispose();
                }
                _sessions.Clear();
                _activeSession = null;
            }

            SessionChanged?.Invoke(new SessionEvent(SessionEventType.Disposed, string.Empty, null, "DocumentSessionManager disposed"));
        }
    }
}
