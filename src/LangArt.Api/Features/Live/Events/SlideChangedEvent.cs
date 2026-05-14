namespace LangArt.Api.Features.Live.Events;

/// <summary>Hub event broadcast to every participant in a session when the active slide changes.</summary>
public class SlideChangedEvent
{
    public int BlockIndex { get; init; }
    public DateTime ChangedAt { get; init; }
    public Guid ChangedBy { get; init; }
}
