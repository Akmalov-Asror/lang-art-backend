using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.AiChat.Dto;

public class ChatRequest
{
    [Required]
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional: the lesson/grammar context the student is asking about (Markdown text).</summary>
    public string? Context { get; set; }

    /// <summary>Optional: previous turns for multi-turn conversation.</summary>
    public List<ChatTurn> History { get; set; } = new();
}

public class ChatTurn
{
    public string Role { get; set; } = "user"; // user | assistant
    public string Content { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Reply { get; set; } = string.Empty;
    /// <summary>Indicates the source — "stub" or "openai".</summary>
    public string Provider { get; set; } = "stub";
}
