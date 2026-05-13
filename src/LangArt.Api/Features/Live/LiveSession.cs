using LangArt.Api.Data.Entities;

namespace LangArt.Api.Features.Live;

/// <summary>
/// A synchronous, teacher-driven walkthrough of a lesson with a classroom of students.
/// Lifecycle: created on POST /api/live-sessions, ended on POST /api/live-sessions/{id}/end.
/// A row is considered "active" while <see cref="EndedAt"/> is null.
/// </summary>
public class LiveSession
{
    public Guid Id { get; set; }

    /// <summary>
    /// References <c>groups.id</c> — the codebase calls a classroom a "group",
    /// but the live-lesson feature exposes it as <c>classroom_id</c> on the wire.
    /// </summary>
    public Guid ClassroomId { get; set; }
    public Guid LessonId { get; set; }
    public Guid TeacherId { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public int CurrentBlockIndex { get; set; }

    /// <summary>One of: <c>teacher_ended</c>, <c>timeout</c>, <c>server_restart</c>. Null while active.</summary>
    public string? EndReason { get; set; }

    public Group Classroom { get; set; } = null!;
    public Lesson Lesson { get; set; } = null!;
    public Profile Teacher { get; set; } = null!;
}
