using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    /// <summary>
    /// Central multi-document registry managing concurrent open document sessions,
    /// atomic active-tab switching, cancelable loading, and safe native resource cleanup.
    /// </summary>
    public interface IDocumentSessionManager : IDisposable
    {
        IReadOnlyList<IDocumentSession> Sessions { get; }
        IDocumentSession? ActiveSession { get; }
        event Action<SessionEvent>? SessionChanged;

        Task<IDocumentSession> OpenDocumentAsync(string filePath, string? password = null, CancellationToken cancellationToken = default);
        void SwitchActiveSession(string sessionId);
        void CloseSession(string sessionId);
        void CloseAllSessions();
        IDocumentSession? GetSession(string sessionId);
    }
}
