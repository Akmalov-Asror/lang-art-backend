using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Analytics.Dto;

/// <summary>
/// Per-skill 0-100 score derived from a student's submission history.
/// Null means the student hasn't attempted any exercise of that kind yet.
/// </summary>
public class SkillLevelsDto
{
    public int? Speaking { get; set; }
    public int? Writing { get; set; }
    public int? Reading { get; set; }
    public int? Listening { get; set; }
    public int? Grammar { get; set; }
    public int? Vocabulary { get; set; }
    /// <summary>Simple average of the six axes; null if no attempts at all.</summary>
    public int? Overall { get; set; }
}

public class StudentSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int TotalXp { get; set; }
    public int Level { get; set; }
    public int CurrentStreak { get; set; }
    public SkillLevelsDto Skills { get; set; } = new();
    public int LessonsCompleted { get; set; }
    public int SubmissionsLast7Days { get; set; }
    public string? GroupName { get; set; }
    /// <summary>"struggling" | "advanced" | "on_track" | "no_data" (Phase 6 adaptive).</summary>
    public string AdaptiveStatus { get; set; } = "no_data";
}

public class RecentSubmissionDto
{
    public Guid Id { get; set; }
    /// <summary>One of: quiz, reading, listening, writing, speaking, fill_blank, lesson</summary>
    public string Kind { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public int? Score { get; set; }
    public int? OutOf { get; set; }
    public bool? Passed { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid? LessonId { get; set; }
    public Guid? ContentId { get; set; }
}

public class StudentDetailDto
{
    public StudentSummaryDto Student { get; set; } = new();
    public List<RecentSubmissionDto> RecentSubmissions { get; set; } = new();
    public List<TeacherNoteDto> Notes { get; set; } = new();
    /// <summary>Adaptive insights: struggling/advanced flags + per-lesson lists + recommendations.</summary>
    public AdaptiveStatusDto Adaptive { get; set; } = new();
}

public class TeacherNoteDto
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Kind { get; set; } = "observation";
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpsertNoteRequest
{
    [Required, RegularExpression("^(observation|praise|warning|plan)$")]
    public string Kind { get; set; } = "observation";

    [Required, MinLength(1), MaxLength(5000)]
    public string Body { get; set; } = string.Empty;
}

// ===== Phase 9 — Admin Teacher Quality =====

public class TeacherSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public int GroupsCount { get; set; }
    public int StudentsCount { get; set; }
    /// <summary>Average overall skill score across all of this teacher's students (0-100). Null if no students with data.</summary>
    public int? AvgStudentOverall { get; set; }
    /// <summary>How many speaking + writing submissions this teacher has reviewed (teacher_id == this teacher).</summary>
    public int SubmissionsReviewed { get; set; }
    /// <summary>Average hours between a student's submission and this teacher's review. Null if no reviews yet.</summary>
    public double? AvgResponseHours { get; set; }
    public int CoursesOwned { get; set; }
    public int TotalNotesWritten { get; set; }
}

public class TeacherDetailDto
{
    public TeacherSummaryDto Teacher { get; set; } = new();
    /// <summary>The same student summary shape the teacher sees on /teacher/students.</summary>
    public List<StudentSummaryDto> Students { get; set; } = new();
}

// ===== Phase 6 — Adaptive Learning =====

public class AdaptiveStatusDto
{
    /// <summary>One of: struggling | advanced | on_track | no_data.</summary>
    public string Status { get; set; } = "no_data";
    /// <summary>Plain-language explanation, e.g. "8 attempts, 45% accuracy on Past Simple".</summary>
    public string Summary { get; set; } = string.Empty;
    public List<StruggleAlertDto> Struggling { get; set; } = new();
    public List<MasteryFlagDto> MasteredQuickly { get; set; } = new();
    public List<AdaptiveRecommendationDto> Recommendations { get; set; } = new();
}

public class StruggleAlertDto
{
    public Guid LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public string? CourseTitle { get; set; }
    public int AttemptCount { get; set; }
    public int AccuracyPct { get; set; }
    /// <summary>"high" | "medium" — how serious the struggle is.</summary>
    public string Severity { get; set; } = "medium";
}

public class MasteryFlagDto
{
    public Guid LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public string? CourseTitle { get; set; }
    public int AccuracyPct { get; set; }
    public int Attempts { get; set; }
}

public class AdaptiveRecommendationDto
{
    /// <summary>"review_lesson" | "harder_practice" | "teacher_intervention".</summary>
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public Guid? LessonId { get; set; }
}
