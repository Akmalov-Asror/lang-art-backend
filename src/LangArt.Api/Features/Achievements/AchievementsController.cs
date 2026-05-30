using LangArt.Api.Common.Auth;
using LangArt.Api.Features.Achievements.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Achievements;

[ApiController]
[Route("api/achievements")]
[Authorize]
public class AchievementsController : ControllerBase
{
    private readonly AchievementsService _svc;
    private readonly ICurrentUser _currentUser;

    public AchievementsController(AchievementsService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpGet("")]
    public Task<IReadOnlyList<AchievementDto>> ListAll(CancellationToken ct) =>
        _svc.ListAllAsync(ct);

    [HttpGet("by-month")]
    public Task<IReadOnlyList<AchievementMonthlyDto>> ListByMonth(CancellationToken ct) =>
        _svc.ListGroupedByMonthAsync(ct);

    [HttpGet("me")]
    public Task<IReadOnlyList<AchievementDto>> Mine(CancellationToken ct) =>
        _svc.ListForUserAsync(_currentUser.Id, ct);

    [HttpGet("user/{userId:guid}")]
    public Task<IReadOnlyList<AchievementDto>> ForUser(Guid userId, CancellationToken ct) =>
        _svc.ListForUserAsync(userId, ct);

    /// <summary>
    /// Admin-only: compute monthly winners for the given period (defaults to last month).
    /// Idempotent — re-running for the same period is safe.
    /// </summary>
    [HttpPost("compute")]
    [Authorize(Roles = "admin")]
    public Task<ComputeAwardsResult> Compute([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var lastMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
        return _svc.ComputeAwardsAsync(year ?? lastMonth.Year, month ?? lastMonth.Month, ct);
    }

    [HttpGet("categories")]
    public IActionResult Categories() =>
        Ok(AchievementsService.Categories.Select(c => new { code = c.Code, label = c.Label, emoji = c.Emoji }));
}
