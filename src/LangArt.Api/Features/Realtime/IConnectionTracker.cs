namespace LangArt.Api.Features.Realtime;

/// <summary>
/// Tracks live SignalR connections by user. Keyed on the user's <see cref="Guid"/>
/// (sub claim), values are zero-or-more <c>ConnectionId</c>s (a user may have
/// multiple tabs/devices open simultaneously).
///
/// In-memory only — see CLAUDE.md "## Real-time (Sprint 2)" for the single-instance
/// caveat. When horizontal scale-out is needed, replace with a Redis-backed
/// implementation; the interface is intentionally Redis-shaped already.
/// </summary>
public interface IConnectionTracker
{
    /// <summary>Register a new connection. Adding the same id twice is a no-op.</summary>
    void AddConnection(Guid userId, string connectionId);

    /// <summary>Drop the connection. Removing a non-existent connection is a no-op.</summary>
    void RemoveConnection(string connectionId);

    /// <summary>All currently-live connection ids for the user (empty if offline).</summary>
    IReadOnlyCollection<string> GetConnections(Guid userId);

    /// <summary>True if the user has at least one live connection.</summary>
    bool IsOnline(Guid userId);

    /// <summary>Snapshot of every user currently online.</summary>
    IReadOnlyCollection<Guid> GetOnlineUsers();
}
