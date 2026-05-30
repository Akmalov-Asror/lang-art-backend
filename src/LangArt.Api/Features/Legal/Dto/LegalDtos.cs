using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Legal.Dto;

public class LegalDocumentDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BodyMarkdown { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool AcceptedByMe { get; set; }
}

public class PendingAcceptanceDto
{
    public List<LegalDocumentDto> Documents { get; set; } = new();
}

public class PublishDocumentRequest
{
    [Required] public string Kind { get; set; } = "public_offer";
    [Required, MinLength(2)] public string Title { get; set; } = string.Empty;
    [Required, MinLength(10)] public string BodyMarkdown { get; set; } = string.Empty;
    public DateTime? EffectiveFrom { get; set; }
}

public class AcceptDocumentRequest
{
    [Required] public Guid DocumentId { get; set; }
}

public class LegalAcceptanceDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public DateTime AcceptedAt { get; set; }
}
