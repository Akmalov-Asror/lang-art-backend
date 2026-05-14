namespace LangArt.Api.Features.Live.Events;

/// <summary>Hub event broadcast to everyone in the session group when a new participant joins.</summary>
public class ParticipantJoinedEvent
{
    public Guid UserId { get; init; }
    public string Role { get; init; } = "";
    public DateTime JoinedAt { get; init; }
}
