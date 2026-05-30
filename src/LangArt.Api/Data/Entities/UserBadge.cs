namespace LangArt.Api.Data.Entities;

public class UserBadge
{
    public Guid UserId { get; set; }
    public Guid BadgeId { get; set; }
    public DateTime EarnedAtUtc { get; set; }

    public Profile User { get; set; } = null!;
    public Badge Badge { get; set; } = null!;
}
