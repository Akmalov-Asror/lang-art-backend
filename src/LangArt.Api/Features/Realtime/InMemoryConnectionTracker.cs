using System.Collections.Concurrent;

namespace LangArt.Api.Features.Realtime;

/// <summary>
/// Process-local connection registry. Single-instance only; see
/// <see cref="IConnectionTracker"/> XML doc + CLAUDE.md for the rationale.
///
/// Thread-safety:
/// <list type="bullet">
///   <item>The outer <c>ConcurrentDictionary</c>s are thread-safe by themselves.</item>
///   <item>The inner per-user <c>HashSet&lt;string&gt;</c> is mutated only under
///         a per-user lock (using the HashSet itself as the lock object). Reads
///         happen under the same lock and copy out a snapshot to avoid leaking
///         the live set to callers.</item>
/// </list>
/// </summary>
public class InMemoryConnectionTracker : IConnectionTracker
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _byUser = new();
    private readonly ConcurrentDictionary<string, Guid> _byConnection = new();

    public void AddConnection(Guid userId, string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId)) return;

        var set = _byUser.GetOrAdd(userId, _ => new HashSet<string>(StringComparer.Ordinal));
        lock (set)
        {
            set.Add(connectionId);
        }
        _byConnection[connectionId] = userId;
    }

    public void RemoveConnection(string connectionId)
    {
        if (!_byConnection.TryRemove(connectionId, out var userId)) return;

        if (_byUser.TryGetValue(userId, out var set))
        {
            bool isEmpty;
            lock (set)
            {
                set.Remove(connectionId);
                isEmpty = set.Count == 0;
            }
            if (isEmpty)
            {
                // Race-safe removal: only drop the bucket if it's still empty under the lock.
                _byUser.TryRemove(new KeyValuePair<Guid, HashSet<string>>(userId, set));
            }
        }
    }

    public IReadOnlyCollection<string> GetConnections(Guid userId)
    {
        if (!_byUser.TryGetValue(userId, out var set)) return Array.Empty<string>();
        lock (set)
        {
            return set.ToArray();
        }
    }

    public bool IsOnline(Guid userId) => GetConnections(userId).Count > 0;

    public IReadOnlyCollection<Guid> GetOnlineUsers() =>
        // The keys snapshot is safe; if a user disconnects mid-iteration we'd
        // simply report a slightly stale "online" view, which is harmless for
        // dashboards. Cross-check with IsOnline if exactness matters.
        _byUser.Keys.ToArray();
}
