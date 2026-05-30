using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Messaging.Dto;

public class MessageDto
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public Guid RecipientId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsMine { get; set; }
}

public class ConversationSummaryDto
{
    public Guid OtherUserId { get; set; }
    public string OtherUserName { get; set; } = string.Empty;
    public string OtherUserRole { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string LastMessageBody { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public Guid LastMessageSenderId { get; set; }
    public int UnreadCount { get; set; }
}

public class SendMessageRequest
{
    [Required]
    public Guid RecipientId { get; set; }
    [Required, MinLength(1), MaxLength(4000)]
    public string Body { get; set; } = string.Empty;
}

public class ContactDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Email { get; set; }
}
