namespace LangArt.Api.Data.Entities;

/// <summary>
/// 1:1 message between two users. We don't model conversations as a separate
/// entity — they're inferred from <c>(sender_id, recipient_id)</c> pairs.
/// </summary>
public class Message
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public Guid RecipientId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }

    public Profile Sender { get; set; } = null!;
    public Profile Recipient { get; set; } = null!;
}
