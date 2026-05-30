using System.Net.Http.Headers;
using System.Text.Json;
using LangArt.Api.Common.Configuration;
using LangArt.Api.Features.Speaking.Dto;
using Microsoft.Extensions.Options;

namespace LangArt.Api.Features.Speaking;

/// <summary>
/// Real implementation: pipes the recorded audio through OpenAI Whisper for
/// transcription, then asks a chat model (default: <c>gpt-4o-mini</c>) to grade
/// the transcript against four rubric dimensions. The grader returns
/// structured JSON so we can parse scores reliably.
///
/// Falls back to throwing if <c>OPENAI_API_KEY</c> is missing — the DI wiring
/// in <c>Program.cs</c> picks the stub grader in that case, so this service
/// should only be reached when a key is configured.
/// </summary>
public class OpenAiSpeechGradingService : IAiSpeechGradingService
{
    private const string TranscriptionEndpoint = "https://api.openai.com/v1/audio/transcriptions";
    private const string ChatEndpoint = "https://api.openai.com/v1/chat/completions";

    private readonly IHttpClientFactory _httpFactory;
    private readonly UploadsOptions _uploadsOpts;
    private readonly OpenAiOptions _opts;
    private readonly ILogger<OpenAiSpeechGradingService> _logger;

    public OpenAiSpeechGradingService(
        IHttpClientFactory httpFactory,
        IOptions<UploadsOptions> uploads,
        IOptions<OpenAiOptions> opts,
        ILogger<OpenAiSpeechGradingService> logger)
    {
        _httpFactory = httpFactory;
        _uploadsOpts = uploads.Value;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<AiGradeResult> GradeAsync(string audioUrl, string promptText, CancellationToken ct)
    {
        // 1. Resolve the audio URL to a local file path. The browser uploaded the
        //    file via /api/uploads/resource, so it lives under UploadsOptions.Dir
        //    and is reachable at /uploads/<subdir>/<name>.
        var filePath = ResolveLocalPath(audioUrl)
            ?? throw new InvalidOperationException($"Cannot resolve audio file: {audioUrl}");
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Audio file not on disk: {filePath}");
        }

        // 2. Whisper transcription.
        var transcript = await TranscribeAsync(filePath, ct);

        // 3. Chat-based grading. Returns structured JSON.
        var grade = await GradeTranscriptAsync(transcript, promptText, ct);
        grade.Transcript = transcript;
        return grade;
    }

    private async Task<string> TranscribeAsync(string filePath, CancellationToken ct)
    {
        using var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(2);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);

        await using var fs = File.OpenRead(filePath);
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(fs);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GuessContentType(filePath));
        form.Add(fileContent, "file", Path.GetFileName(filePath));
        form.Add(new StringContent(_opts.TranscriptionModel), "model");
        // Language hint — null means auto-detect.
        if (!string.IsNullOrWhiteSpace(_opts.TranscriptionLanguage))
        {
            form.Add(new StringContent(_opts.TranscriptionLanguage), "language");
        }

        using var resp = await http.PostAsync(TranscriptionEndpoint, form, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Whisper failed: {Status} {Body}", resp.StatusCode, body);
            throw new InvalidOperationException($"Whisper transcription failed ({(int)resp.StatusCode})");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
    }

    private async Task<AiGradeResult> GradeTranscriptAsync(string transcript, string promptText, CancellationToken ct)
    {
        using var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(1);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);

        var system =
            "You are an English-language speaking examiner. Grade the student's transcript on four dimensions, " +
            "each from 0 to 100: pronunciation (intelligibility cues from word choice / disfluencies), fluency " +
            "(coherence, hedges, repetition), grammar (verb forms, agreement, syntax), vocabulary (range, " +
            "precision). Also compute total as the rounded average. Then write a short, supportive feedback " +
            "paragraph (2-4 sentences) addressed to the student. Return ONLY a JSON object with keys " +
            "pronunciation, fluency, grammar, vocabulary, total, feedback. No prose outside the JSON.";

        var user = string.IsNullOrWhiteSpace(promptText)
            ? $"Transcript:\n\"\"\"{transcript}\"\"\""
            : $"Speaking task prompt: \"{promptText}\"\n\nStudent transcript:\n\"\"\"{transcript}\"\"\"";

        var requestBody = JsonSerializer.Serialize(new
        {
            model = _opts.ChatModel,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user },
            },
        });

        using var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
        using var resp = await http.PostAsync(ChatEndpoint, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Chat grading failed: {Status} {Body}", resp.StatusCode, body);
            throw new InvalidOperationException($"Chat grading failed ({(int)resp.StatusCode})");
        }

        using var doc = JsonDocument.Parse(body);
        var msgContent = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        using var inner = JsonDocument.Parse(msgContent);
        var root = inner.RootElement;
        int p = ReadInt(root, "pronunciation");
        int f = ReadInt(root, "fluency");
        int g = ReadInt(root, "grammar");
        int v = ReadInt(root, "vocabulary");
        int t = root.TryGetProperty("total", out var totalEl) && totalEl.ValueKind == JsonValueKind.Number
            ? totalEl.GetInt32()
            : (int)Math.Round((p + f + g + v) / 4.0);
        string feedback = root.TryGetProperty("feedback", out var fb) ? fb.GetString() ?? "" : "";

        return new AiGradeResult
        {
            Pronunciation = Math.Clamp(p, 0, 100),
            Fluency = Math.Clamp(f, 0, 100),
            Grammar = Math.Clamp(g, 0, 100),
            Vocabulary = Math.Clamp(v, 0, 100),
            Total = Math.Clamp(t, 0, 100),
            Feedback = feedback,
        };
    }

    private static int ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var v)) return 0;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetInt32(),
            JsonValueKind.String when int.TryParse(v.GetString(), out var i) => i,
            _ => 0,
        };
    }

    private static string GuessContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            ".webm" => "audio/webm",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream",
        };

    private string? ResolveLocalPath(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/uploads/", StringComparison.Ordinal))
            return null;
        var relative = url["/uploads/".Length..].Replace('\\', '/');
        if (relative.Contains("..", StringComparison.Ordinal)) return null;
        var baseDir = Path.GetFullPath(_uploadsOpts.Dir);
        var full = Path.GetFullPath(Path.Combine(baseDir, relative));
        return full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}

/// <summary>Bound from environment in Program.cs.</summary>
public class OpenAiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public string TranscriptionModel { get; set; } = "whisper-1";
    public string? TranscriptionLanguage { get; set; } = "en";
}
