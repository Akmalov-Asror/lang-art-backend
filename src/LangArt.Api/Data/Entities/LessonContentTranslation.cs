namespace LangArt.Api.Data.Entities;

/// <summary>
/// Per-language localization for a <see cref="LessonContent"/> row.
/// One row per (content_id, language). Used primarily for Grammar Explanation
/// content where students can switch between Uz/Ru/En, but the same table
/// can hold subtitle/script translations for video and audio content too.
/// </summary>
public class LessonContentTranslation
{
    public Guid Id { get; set; }
    public Guid ContentId { get; set; }
    public string Language { get; set; } = "en";
    public string BodyMarkdown { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
    public string? SubtitleUrl { get; set; }
    public string? Script { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public LessonContent Content { get; set; } = null!;
}
