namespace LangArt.Api.Data.Entities;

/// <summary>
/// Catalog of badges users can earn. <c>Code</c> is the stable identifier
/// referenced in <see cref="Features.Gamification.BadgeCriteriaEvaluator"/>;
/// renaming a code is a breaking change. <c>Criteria</c> is a JSON document
/// describing the rule so future badges can be data-driven without a code
/// change (today the evaluator just switches on <c>Code</c>).
/// </summary>
public class Badge
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string Criteria { get; set; } = "{}";
    public int XpReward { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
}
