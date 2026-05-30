using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Notifications.Push.Dto;

public class SubscribeRequest
{
    [Required, MinLength(1)]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public Keys Keys { get; set; } = new();

    public string? UserAgent { get; set; }
}

public class Keys
{
    [Required, MinLength(1)]
    public string P256dh { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string Auth { get; set; } = string.Empty;
}

public class UnsubscribeRequest
{
    [Required, MinLength(1)]
    public string Endpoint { get; set; } = string.Empty;
}
