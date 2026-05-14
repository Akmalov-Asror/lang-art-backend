namespace LangArt.Api.Features.Live.Events;

/// <summary>Hub event broadcast when a participant's last connection drops.</summary>
public class ParticipantLeftEvent
{
    public Guid UserId { get; init; }
    public DateTime LeftAt { get; init; }
}
