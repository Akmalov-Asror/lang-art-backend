namespace LangArt.Api.Data.Entities;

/// <summary>
/// A versioned legal document (public offer / Terms / Privacy / Refund policy).
/// Only one document per kind is "current" at a time. Newer versions force
/// users to re-accept on next login.
/// </summary>
public class LegalDocument
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "public_offer";   // public_offer, terms, privacy, refund
    public int Version { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BodyMarkdown { get; set; } = string.Empty;
    public bool IsCurrent { get; set; } = true;
    public Guid? CreatedBy { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LegalAcceptance
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DocumentId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int Version { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public DateTime AcceptedAt { get; set; }

    public LegalDocument Document { get; set; } = null!;
}
