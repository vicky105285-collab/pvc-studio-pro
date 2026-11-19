using System;

namespace PVCStudioPro.Core.Models
{
    public enum SessionEventType
    {
        Opened,
        ActiveChanged,
        Closed,
        DocumentModified,
        Disposed
    }

    /// <summary>
    /// Event broadcast by IDocumentSessionManager whenever session lifecycle or active selection changes.
    /// </summary>
    public sealed record SessionEvent(
        SessionEventType EventType,
        string SessionId,
        IDocumentSession? Session = null,
        string? Description = null);

    /// <summary>
    /// Event broadcast by an IDocumentSession to observers whenever internal state or revisions change.
    /// </summary>
    public sealed record DocumentChange(
        string SessionId,
        int Revision,
        string ChangeType,
        string? Description = null);
}
