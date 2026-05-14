using System.Collections.Concurrent;
using LangArt.Api.Data;
using LangArt.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Live;

/// <summary>
/// Process-wide singleton holding the in-memory state for every active live session.
/// Lazily hydrates a session from the DB on first join. Survives any number of
/// connection / disconnection / page-refresh cycles; cleared only when REST ends a session
/// (via <see cref="Evict"/>) or the process restarts.
/// </summary>
public class SessionRegistry
{
    private readonly ConcurrentDictionary<Guid, LiveSessionState> _sessions = new();
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionRegistry> _logger;

    public SessionRegistry(IServiceScopeFactory scopeFactory, ILogger<SessionRegistry> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Fast in-memory lookup; returns null if the session has not been loaded yet.</summary>
    public LiveSessionState? Get(Guid sessionId) =>
        _sessions.TryGetValue(sessionId, out var s) ? s : null;

    /// <summary>
    /// Returns the live state for <paramref name="sessionId"/>, loading it from the DB on
    /// first call. Returns <c>null</c> if no active (un-ended) session row exists.
    /// Two concurrent callers cannot duplicate work — <c>GetOrAdd</c> with a factory makes
    /// the build idempotent.
    /// </summary>
    public async Task<LiveSessionState?> GetOrLoadAsync(Guid sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var existing))
            return existing;

        var loaded = await LoadFromDbAsync(sessionId);
        if (loaded is null) return null;

        // Either keep what's already there or install the newly loaded copy.
        return _sessions.GetOrAdd(sessionId, loaded);
    }

    /// <summary>
    /// Adds <paramref name="connectionId"/> to the per-user connection set.
    /// Returns <c>true</c> when this is the *first* connection for the user in this session —
    /// the hub should broadcast <c>participant-joined</c> only on the leading edge.
    /// </summary>
    public bool AddConnection(LiveSessionState session, string connectionId, Guid userId, string role)
    {
        _connections[connectionId] = new ConnectionInfo(connectionId, session.SessionId, userId, role);

        var participant = session.Participants.GetOrAdd(userId, _ => new ParticipantState
        {
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow,
        });

        lock (participant.ConnectionsLock)
        {
            var wasEmpty = participant.ConnectionIds.Count == 0;
            participant.ConnectionIds.Add(connectionId);
            if (wasEmpty)
            {
                participant.Status = "connected";
                participant.JoinedAt = DateTime.UtcNow;
            }
            return wasEmpty;
        }
    }

    /// <summary>
    /// Removes <paramref name="connectionId"/> from the registry.
    /// Returns the connection's info plus a flag indicating whether the user's *last* connection
    /// has dropped (so the hub can fire <c>participant-left</c>).
    /// </summary>
    public (ConnectionInfo Info, bool IsLastConnection)? RemoveConnection(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var info))
            return null;

        if (!_sessions.TryGetValue(info.SessionId, out var session))
            return (info, true);

        if (!session.Participants.TryGetValue(info.UserId, out var participant))
            return (info, true);

        bool isLast;
        lock (participant.ConnectionsLock)
        {
            participant.ConnectionIds.Remove(connectionId);
            isLast = participant.ConnectionIds.Count == 0;
            if (isLast) participant.Status = "disconnected";
        }
        if (isLast) session.Participants.TryRemove(info.UserId, out _);

        return (info, isLast);
    }

    /// <summary>
    /// Advances the session to <paramref name="blockIndex"/> in memory and persists the new
    /// value to the DB with a single <c>UPDATE</c> (no change tracking, no entity load).
    /// Slide changes happen at human pace, so the per-call write cost is irrelevant and
    /// guarantees that a server restart resumes at the last broadcast slide.
    /// </summary>
    public async Task UpdateCurrentBlockAsync(Guid sessionId, int blockIndex)
    {
        var session = Get(sessionId);
        if (session is null) return;

        session.CurrentBlockIndex = blockIndex;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.LiveSessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.CurrentBlockIndex, blockIndex));
    }

    /// <summary>
    /// Drops a session from in-memory state. Called by REST when a session is ended so a
    /// subsequent <c>JoinSession</c> RPC for the same id has to consult the DB (which will
    /// now have <c>ended_at IS NOT NULL</c>, so the load returns null and the hub rejects).
    /// </summary>
    public void Evict(Guid sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            foreach (var participant in session.Participants.Values)
            {
                lock (participant.ConnectionsLock)
                {
                    foreach (var cid in participant.ConnectionIds)
                        _connections.TryRemove(cid, out _);
                }
            }
            _logger.LogInformation("Evicted live session {SessionId} from registry", sessionId);
        }
    }

    private async Task<LiveSessionState?> LoadFromDbAsync(Guid sessionId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var row = await db.LiveSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.EndedAt == null);
        if (row is null) return null;

        // Authorized = classroom teacher ∪ enrolled students ∪ all admins.
        var enrolledStudents = await db.GroupStudents
            .Where(gs => gs.GroupId == row.ClassroomId)
            .Select(gs => gs.StudentId)
            .ToListAsync();

        var classroomTeacherId = await db.Groups
            .Where(g => g.Id == row.ClassroomId)
            .Select(g => g.TeacherId)
            .FirstOrDefaultAsync();

        var adminIds = await db.Profiles
            .Where(p => p.Role == Role.Admin && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync();

        var authorized = new HashSet<Guid>(enrolledStudents);
        authorized.Add(classroomTeacherId);
        authorized.Add(row.TeacherId); // safe even if equal to classroomTeacherId
        foreach (var id in adminIds) authorized.Add(id);

        // Slide count is fixed for the lifetime of the session — lessons aren't edited live.
        var blockCount = await db.LessonContent.CountAsync(c => c.LessonId == row.LessonId);

        return new LiveSessionState
        {
            SessionId = row.Id,
            ClassroomId = row.ClassroomId,
            LessonId = row.LessonId,
            TeacherId = row.TeacherId,
            StartedAt = row.StartedAt,
            CurrentBlockIndex = row.CurrentBlockIndex,
            BlockCount = blockCount,
            AuthorizedUserIds = authorized,
        };
    }
}
