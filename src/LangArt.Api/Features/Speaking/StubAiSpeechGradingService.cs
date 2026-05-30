using LangArt.Api.Features.Speaking.Dto;

namespace LangArt.Api.Features.Speaking;

/// <summary>
/// Development-time stub. Produces plausible grades and a placeholder transcript
/// without making any external API calls. Swap to a real implementation (Whisper
/// for transcription + Claude for rubric grading) by binding a different impl in
/// Program.cs.
/// </summary>
public class StubAiSpeechGradingService : IAiSpeechGradingService
{
    public Task<AiGradeResult> GradeAsync(string audioUrl, string promptText, CancellationToken ct)
    {
        // Deterministic per-audio "grades" so the same recording yields the same
        // numbers on retries — keeps the demo coherent.
        var seed = audioUrl.GetHashCode();
        var rng = new Random(seed);

        int pronunciation = 70 + rng.Next(0, 26); // 70-95
        int fluency = 65 + rng.Next(0, 31);       // 65-95
        int grammar = 70 + rng.Next(0, 26);
        int vocabulary = 70 + rng.Next(0, 26);
        var total = (int)Math.Round((pronunciation + fluency + grammar + vocabulary) / 4.0);

        var transcript = string.IsNullOrWhiteSpace(promptText)
            ? "[stub] Student speaking response transcript would appear here."
            : $"[stub] Response to prompt: \"{promptText.Trim()}\". Real transcript pending Whisper integration.";

        var feedback = BuildFeedback(pronunciation, fluency, grammar, vocabulary);
        return Task.FromResult(new AiGradeResult
        {
            Transcript = transcript,
            Pronunciation = pronunciation,
            Fluency = fluency,
            Grammar = grammar,
            Vocabulary = vocabulary,
            Total = total,
            Feedback = feedback,
        });
    }

    private static string BuildFeedback(int p, int f, int g, int v)
    {
        var parts = new List<string>();
        parts.Add(p >= 85 ? "Pronunciation is clear and natural." :
                  p >= 70 ? "Pronunciation is mostly intelligible — keep practicing tricky sounds." :
                            "Pronunciation needs more practice; focus on slowing down and articulating.");
        parts.Add(f >= 85 ? "Fluency flows well with few hesitations." :
                  f >= 70 ? "Fluency is good with occasional pauses." :
                            "Try to reduce pauses and connect ideas more smoothly.");
        parts.Add(g >= 85 ? "Grammar is solid." :
                  g >= 70 ? "Grammar is mostly correct; watch verb tenses." :
                            "Review basic grammar patterns and verb agreement.");
        parts.Add(v >= 85 ? "Vocabulary is rich and varied." :
                  v >= 70 ? "Vocabulary is adequate; try using more synonyms." :
                            "Expand your vocabulary with topical word lists.");
        return string.Join(" ", parts);
    }
}
