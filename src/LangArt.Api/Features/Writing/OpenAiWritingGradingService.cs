using System.Net.Http.Headers;
using System.Text.Json;
using LangArt.Api.Features.Speaking;  // reuse OpenAiOptions
using LangArt.Api.Features.Writing.Dto;
using Microsoft.Extensions.Options;

namespace LangArt.Api.Features.Writing;

/// <summary>
/// Real implementation: feeds the student text to GPT (default
/// <c>gpt-4o-mini</c>) and asks for IELTS-style structured scores.
/// </summary>
public class OpenAiWritingGradingService : IAiWritingGradingService
{
    private const string ChatEndpoint = "https://api.openai.com/v1/chat/completions";

    private readonly IHttpClientFactory _httpFactory;
    private readonly OpenAiOptions _opts;
    private readonly ILogger<OpenAiWritingGradingService> _logger;

    public OpenAiWritingGradingService(
        IHttpClientFactory httpFactory,
        IOptions<OpenAiOptions> opts,
        ILogger<OpenAiWritingGradingService> logger)
    {
        _httpFactory = httpFactory;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<AiWritingGradeResult> GradeAsync(string text, string promptText, int? minWordCount, CancellationToken ct)
    {
        using var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(1);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);

        var system =
            "You are an English-language writing examiner. Grade the student's response on four IELTS-style " +
            "dimensions, each from 0 to 100: task_achievement (does it address the prompt fully and stay on topic), " +
            "coherence (paragraph organisation, linking words, logical flow), grammar (sentence structure, " +
            "tenses, agreement), vocabulary (range, precision, collocation). Also compute total as the rounded " +
            "average. Then write a short, supportive feedback paragraph (2-4 sentences) addressed directly to " +
            "the student. Return ONLY a JSON object with keys task_achievement, coherence, grammar, vocabulary, " +
            "total, feedback. No prose outside the JSON.";

        var userMsg = $"Writing task prompt: \"{promptText}\"\n" +
            (minWordCount.HasValue ? $"Minimum word count: {minWordCount.Value}\n\n" : "\n") +
            $"Student response:\n\"\"\"\n{text}\n\"\"\"";

        var body = JsonSerializer.Serialize(new
        {
            model = _opts.ChatModel,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = userMsg },
            },
        });

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var resp = await http.PostAsync(ChatEndpoint, content, ct);
        var responseBody = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Writing grading failed: {Status} {Body}", resp.StatusCode, responseBody);
            throw new InvalidOperationException($"Writing grading failed ({(int)resp.StatusCode})");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var msgContent = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        using var inner = JsonDocument.Parse(msgContent);
        var root = inner.RootElement;
        int ReadInt(string name) =>
            root.TryGetProperty(name, out var v)
                ? v.ValueKind switch
                {
                    JsonValueKind.Number => v.GetInt32(),
                    JsonValueKind.String when int.TryParse(v.GetString(), out var i) => i,
                    _ => 0,
                }
                : 0;

        int task = Math.Clamp(ReadInt("task_achievement"), 0, 100);
        int coh = Math.Clamp(ReadInt("coherence"), 0, 100);
        int gra = Math.Clamp(ReadInt("grammar"), 0, 100);
        int voc = Math.Clamp(ReadInt("vocabulary"), 0, 100);
        int total = root.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number
            ? Math.Clamp(t.GetInt32(), 0, 100)
            : (int)Math.Round((task + coh + gra + voc) / 4.0);
        string feedback = root.TryGetProperty("feedback", out var fb) ? fb.GetString() ?? "" : "";

        return new AiWritingGradeResult
        {
            TaskAchievement = task,
            Coherence = coh,
            Grammar = gra,
            Vocabulary = voc,
            Total = total,
            Feedback = feedback,
        };
    }
}
