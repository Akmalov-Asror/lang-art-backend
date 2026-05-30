using System.Net.Http.Headers;
using System.Text.Json;
using LangArt.Api.Features.AiChat.Dto;
using LangArt.Api.Features.Speaking;
using Microsoft.Extensions.Options;

namespace LangArt.Api.Features.AiChat;

/// <summary>
/// "Ask from LA" — student tutor chat backed by OpenAI Chat Completions.
/// The system prompt frames the assistant as a friendly English tutor and
/// pipes in the current lesson context when provided.
/// </summary>
public class OpenAiChatService : IAiChatService
{
    private const string ChatEndpoint = "https://api.openai.com/v1/chat/completions";

    private readonly IHttpClientFactory _httpFactory;
    private readonly OpenAiOptions _opts;
    private readonly ILogger<OpenAiChatService> _logger;

    public OpenAiChatService(
        IHttpClientFactory httpFactory,
        IOptions<OpenAiOptions> opts,
        ILogger<OpenAiChatService> logger)
    {
        _httpFactory = httpFactory;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<ChatResponse> ReplyAsync(ChatRequest req, CancellationToken ct)
    {
        using var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(1);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);

        var system =
            "You are 'LA' — a friendly, patient English tutor inside a language-learning app called LangArt. " +
            "Students ask you about grammar rules, vocabulary, pronunciation, or how to translate phrases " +
            "from Uzbek / Russian into English. You explain in clear, simple language with concrete examples. " +
            "If the student writes in Uzbek or Russian, you may answer in their language with English examples. " +
            "Keep replies short and focused (2-4 paragraphs max). " +
            "Do NOT do their homework for them — help them understand instead.";

        var messages = new List<object>
        {
            new { role = "system", content = system },
        };

        if (!string.IsNullOrWhiteSpace(req.Context))
        {
            messages.Add(new
            {
                role = "system",
                content = $"Lesson context the student is currently studying:\n---\n{req.Context}\n---",
            });
        }

        foreach (var turn in req.History.TakeLast(10))
        {
            messages.Add(new { role = turn.Role, content = turn.Content });
        }
        messages.Add(new { role = "user", content = req.Message });

        var body = JsonSerializer.Serialize(new
        {
            model = _opts.ChatModel,
            messages,
            temperature = 0.4,
        });

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var resp = await http.PostAsync(ChatEndpoint, content, ct);
        var responseBody = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Chat call failed: {Status} {Body}", resp.StatusCode, responseBody);
            throw new InvalidOperationException($"AI chat failed ({(int)resp.StatusCode})");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var reply = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        return new ChatResponse { Reply = reply, Provider = "openai" };
    }
}
