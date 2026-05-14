namespace LangArt.Api.Features.Live.Events;

/// <summary>Full session state sent to a newly-joined client on the <c>session-state</c> event.</summary>
public class SessionStateSnapshot
{
    public Guid SessionId { get; init; }
    public Guid LessonId { get; init; }
    public int CurrentBlockIndex { get; init; }
    public int BlockCount { get; init; }
    public List<ParticipantSnapshot> Participants { get; init; } = new();
}

public class ParticipantSnapshot
{
    public Guid UserId { get; init; }
    public string Role { get; init; } = "";
    public string Status { get; init; } = "";
}
