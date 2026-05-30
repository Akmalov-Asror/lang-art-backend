namespace LangArt.Api.Data.Entities;

/// <summary>
/// A single vocabulary item attached to a <see cref="Wordlist"/>. Holds the
/// English term plus Uzbek and Russian translations, an En-En definition,
/// an example sentence, and optionally a pronunciation audio URL (uploaded
/// via <c>/api/uploads/resource</c>).
/// </summary>
public class Word
{
    public Guid Id { get; set; }
    public Guid WordlistId { get; set; }
    public string Term { get; set; } = string.Empty;
    public string TranslationUz { get; set; } = string.Empty;
    public string TranslationRu { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string? PronunciationUrl { get; set; }
    public string? ExampleSentence { get; set; }
    public string? PartOfSpeech { get; set; }
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; }

    public string? ImageUrl { get; set; }
    public int? FrequencyRank { get; set; }
    public string[]? TopicTags { get; set; }

    public Wordlist Wordlist { get; set; } = null!;
}
