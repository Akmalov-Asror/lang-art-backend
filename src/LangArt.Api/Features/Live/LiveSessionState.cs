using System.Collections.Concurrent;

namespace LangArt.Api.Features.Live;

/// <summary>
/// In-memory working copy of a live session. The <see cref="LiveSession"/> EF entity
/// is the durable source of truth for lifecycle; this object holds the hot state used
/// by the SignalR hub (participants, current slide). Built once on first join and held
/// in <see cref="SessionRegistry"/> until the session ends.
/// </summary>
public class LiveSessionState
{
    public Guid SessionId { get; init; }
    public Guid ClassroomId { get; init; }
    public Guid LessonId { get; init; }
    public Guid TeacherId { get; init; }
    public DateTime StartedAt { get; init; }

    public int CurrentBlockIndex { get; set; }

    /// <summary>
    /// Number of content blocks (slides) in the lesson. Captured once at load time —
    /// lessons aren't edited mid-session, so the in-memory copy is authoritative and
    /// lets <c>BroadcastSlide</c> bound-check without round-tripping to the DB.
    /// </summary>
    public int BlockCount { get; init; }

    /// <summary>
    /// User IDs allowed to join this session — resolved once at load time:
    /// the classroom's teacher, every enrolled student, plus every admin user.
    /// </summary>
    public HashSet<Guid> AuthorizedUserIds { get; init; } = new();

    public ConcurrentDictionary<Guid, ParticipantState> Participants { get; } = new();
}

public class ParticipantState
{
    public Guid UserId { get; init; }
    public string Role { get; init; } = "";
    public DateTime JoinedAt { get; set; }
    public string Status { get; set; } = "connected";

    /// <summary>
    /// One user can hold multiple connections (multiple tabs / devices). Mutate only
    /// under <see cref="ConnectionsLock"/> — <see cref="HashSet{T}"/> is not thread-safe.
    /// </summary>
    public HashSet<string> ConnectionIds { get; } = new();

    public object ConnectionsLock { get; } = new();
}
