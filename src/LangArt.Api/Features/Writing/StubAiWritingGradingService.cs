using LangArt.Api.Features.Writing.Dto;

namespace LangArt.Api.Features.Writing;

/// <summary>
/// Dev-time stub. Derives plausible grades from text length and word variety
/// without calling OpenAI. Used when <c>OPENAI_API_KEY</c> is missing.
/// </summary>
public class StubAiWritingGradingService : IAiWritingGradingService
{
    public Task<AiWritingGradeResult> GradeAsync(string text, string promptText, int? minWordCount, CancellationToken ct)
    {
        var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var wordCount = words.Length;
        var uniqueRatio = wordCount == 0 ? 0.0 : (double)words.Select(w => w.ToLowerInvariant()).Distinct().Count() / wordCount;
        var hitMin = minWordCount is null || wordCount >= minWordCount.Value;

        // Deterministic per-text scoring so demo retries are stable.
        var seed = text.GetHashCode();
        var rng = new Random(seed);
        int task = hitMin ? 75 + rng.Next(0, 21) : 50 + rng.Next(0, 21);
        int coh = 70 + rng.Next(0, 26);
        int gra = 70 + rng.Next(0, 26);
        int voc = (int)Math.Round(60 + uniqueRatio * 40);
        var total = (int)Math.Round((task + coh + gra + voc) / 4.0);

        var feedback = BuildFeedback(task, coh, gra, voc, wordCount, minWordCount);
        return Task.FromResult(new AiWritingGradeResult
        {
            TaskAchievement = task,
            Coherence = coh,
            Grammar = gra,
            Vocabulary = voc,
            Total = total,
            Feedback = feedback,
        });
    }

    private static string BuildFeedback(int task, int coh, int gra, int voc, int wordCount, int? minWordCount)
    {
        var parts = new List<string>();
        if (minWordCount is not null && wordCount < minWordCount.Value)
        {
            parts.Add($"Your response is shorter than the requested {minWordCount} words ({wordCount} words written) — try to develop your ideas further.");
        }
        parts.Add(task >= 85 ? "You addressed the task fully and stayed on topic."
                : task >= 70 ? "You addressed the task but could develop some points more."
                             : "Focus more on the specific task — re-read the prompt and make sure each requirement is covered.");
        parts.Add(coh >= 85 ? "Ideas flow logically with clear linking words."
                : coh >= 70 ? "Cohesion is mostly clear; use more linking words like 'however', 'as a result'."
                            : "Try to organize paragraphs around one main idea and connect them with linkers.");
        parts.Add(gra >= 85 ? "Grammar is accurate and varied."
                : gra >= 70 ? "Mostly accurate grammar; watch tenses and articles."
                            : "Review basic grammar rules — sentence structure and verb tenses need attention.");
        parts.Add(voc >= 85 ? "Vocabulary is rich and precise."
                : voc >= 70 ? "Good vocabulary; try using more synonyms."
                            : "Expand your vocabulary range — many words are repeated.");
        return string.Join(" ", parts);
    }
}
