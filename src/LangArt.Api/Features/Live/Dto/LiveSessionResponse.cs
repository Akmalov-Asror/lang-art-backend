namespace LangArt.Api.Features.Live.Dto;

public class LiveSessionResponse
{
    public Guid Id { get; set; }
    public Guid ClassroomId { get; set; }
    public Guid LessonId { get; set; }
    public Guid TeacherId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int CurrentBlockIndex { get; set; }
    public string? EndReason { get; set; }
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}
