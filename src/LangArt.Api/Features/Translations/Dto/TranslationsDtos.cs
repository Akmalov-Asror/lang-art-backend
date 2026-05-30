using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Translations.Dto;

public class TranslationDto
{
    public Guid Id { get; set; }
    public Guid ContentId { get; set; }
    public string Language { get; set; } = "en";
    public string BodyMarkdown { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
    public string? SubtitleUrl { get; set; }
    public string? Script { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpsertTranslationRequest
{
    [Required, RegularExpression("^(uz|ru|en)$", ErrorMessage = "Language must be uz, ru, or en")]
    public string Language { get; set; } = "en";

    [Required]
    public string BodyMarkdown { get; set; } = string.Empty;

    public string? VideoUrl { get; set; }
    public string? SubtitleUrl { get; set; }
    public string? Script { get; set; }
}
