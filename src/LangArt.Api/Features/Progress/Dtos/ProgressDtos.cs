using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace LangArt.Api.Features.Progress.Dtos;

// ---------------- Completion ----------------

public class LessonCompletionResponse
{
    public Guid UserId { get; set; }
    public Guid LessonId { get; set; }
    public DateTime CompletedAt { get; set; }
}

public class CompletedStatusResponse
{
    public bool Completed { get; set; }
}

// ---------------- Quiz results ----------------

public class QuizResultResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LessonId { get; set; }
    public Guid? ContentId { get; set; }
    public int Score { get; set; }
    public bool Passed { get; set; }
    public int TotalQuestions { get; set; }
    public JsonElement? MistakesLog { get; set; }
    public JsonElement? Metadata { get; set; }
    public string? TeacherFeedback { get; set; }
    // Wire-format name is `completed_at` (snake-cased by the global JSON policy)
    // — matches the frontend's `QuizResult.completed_at` field used by ResultDetailsModal.
    public DateTime CompletedAt { get; set; }
}

public class SubmitQuizResultRequest
{
    [Required]
    public Guid LessonId { get; set; }

    public Guid? ContentId { get; set; }

    [Required, Range(0, int.MaxValue)]
    public int Score { get; set; }

    [Required, Range(0, int.MaxValue)]
    public int TotalQuestions { get; set; }

    public bool? Passed { get; set; }
    public JsonElement? MistakesLog { get; set; }
    public string? TeacherFeedback { get; set; }
}

// ---------------- Course progress ----------------

public class LessonProgressResponse
{
    public Guid Id { get; set; }
    public Guid ModuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<LessonCompletionResponse> Completions { get; set; } = new();
    public List<QuizResultResponse> QuizResults { get; set; } = new();
}

public class ModuleProgressResponse
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<LessonProgressResponse> Lessons { get; set; } = new();
}

public class CourseProgressResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ModuleProgressResponse> Modules { get; set; } = new();
}

public class CoursePercentageResponse
{
    public int Percentage { get; set; }
}

// ---------------- Access (lesson gating) ----------------

public class UnlockStatusResponse
{
    public bool IsUnlocked { get; set; }
}
