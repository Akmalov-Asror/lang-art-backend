using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using LangArt.Api.Data.Enums;

namespace LangArt.Api.Features.Courses.Dtos;

// ---------------- Responses ----------------

public class CourseResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public decimal? PriceMonthly { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CourseWithModulesResponse : CourseResponse
{
    public List<ModuleWithLessonsResponse> Modules { get; set; } = new();
}

public class ModuleResponse
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ModuleWithLessonsResponse : ModuleResponse
{
    public List<LessonResponse> Lessons { get; set; } = new();
}

public class LessonResponse
{
    public Guid Id { get; set; }
    public Guid ModuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LessonWithContentResponse : LessonResponse
{
    public List<LessonContentResponse> Contents { get; set; } = new();
    public List<LessonResourceResponse> Resources { get; set; } = new();
}

public class LessonContentResponse
{
    public Guid Id { get; set; }
    public Guid LessonId { get; set; }
    public ContentType Type { get; set; }
    public JsonElement ContentPayload { get; set; }
    public int OrderIndex { get; set; }
    public string? ExerciseType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LessonResourceResponse
{
    public Guid Id { get; set; }
    public Guid LessonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// ---------------- Requests ----------------

public class CreateCourseRequest
{
    [Required, MinLength(1), MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public decimal? PriceMonthly { get; set; }
}

public class UpdateCourseRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public decimal? PriceMonthly { get; set; }
}

public class CreateModuleRequest
{
    [Required, MinLength(1), MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    public int? OrderIndex { get; set; }
}

public class UpdateModuleRequest
{
    public string? Title { get; set; }
    public int? OrderIndex { get; set; }
}

public class CreateLessonRequest
{
    [Required, MinLength(1), MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    public int? OrderIndex { get; set; }
    public bool? IsLocked { get; set; }
}

public class UpdateLessonRequest
{
    public string? Title { get; set; }
    public int? OrderIndex { get; set; }
    public bool? IsLocked { get; set; }
}

public class CreateLessonContentRequest
{
    [Required]
    public ContentType Type { get; set; }

    [Required]
    public JsonElement ContentPayload { get; set; }

    public int? OrderIndex { get; set; }
    public string? ExerciseType { get; set; }
}

public class UpdateLessonContentRequest
{
    public ContentType? Type { get; set; }
    public JsonElement? ContentPayload { get; set; }
    public int? OrderIndex { get; set; }
    public string? ExerciseType { get; set; }
}

public class CreateLessonResourceRequest
{
    [Required, MinLength(1)]
    public string Title { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string FileUrl { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string FileType { get; set; } = string.Empty;
}
