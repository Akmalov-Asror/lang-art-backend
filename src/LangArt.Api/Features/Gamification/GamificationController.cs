using LangArt.Api.Common.Auth;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Features.Gamification.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Gamification;

[ApiController]
[Route("api/gamification")]
[Authorize]
public class GamificationController : ControllerBase
{
    private readonly IGamificationService _svc;
    private readonly ICurrentUser _currentUser;
    private readonly AppDbContext _db;

    public GamificationController(IGamificationService svc, ICurrentUser currentUser, AppDbContext db)
    {
        _svc = svc;
        _currentUser = currentUser;
        _db = db;
    }

    [HttpGet("me")]
    public Task<GamificationProfileDto> Me(CancellationToken ct) =>
        _svc.GetProfileAsync(_currentUser.Id, ct);

    [HttpGet("badges")]
    public async Task<IReadOnlyList<BadgeDto>> Badges(CancellationToken ct) =>
        await _svc.GetBadgeCatalogAsync(_currentUser.Id, ct);

    [HttpGet("ledger/me")]
    public async Task<IReadOnlyList<LedgerEntryDto>> MyLedger([FromQuery] int limit = 20, CancellationToken ct = default) =>
        await _svc.GetRecentLedgerAsync(_currentUser.Id, limit, ct);

    [HttpGet("leaderboard/{groupId:guid}")]
    public async Task<LeaderboardDto> Leaderboard(Guid groupId, CancellationToken ct)
    {
        // Membership/role gate: admins and teachers see any group; students must
        // be a member of the group they're querying.
        if (_currentUser.Role == "student")
        {
            var isMember = await _db.GroupStudents
                .AnyAsync(gs => gs.GroupId == groupId && gs.StudentId == _currentUser.Id, ct);
            if (!isMember)
            {
                throw new ForbiddenException("You are not a member of this group");
            }
        }
        return await _svc.GetGroupLeaderboardAsync(groupId, _currentUser.Id, ct);
    }
}
