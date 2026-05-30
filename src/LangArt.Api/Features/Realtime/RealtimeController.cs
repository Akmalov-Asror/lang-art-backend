using LangArt.Api.Common.Auth;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Features.Realtime.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Realtime;

[ApiController]
[Route("api/realtime")]
[Authorize]
public class RealtimeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConnectionTracker _tracker;
    private readonly ICurrentUser _currentUser;

    public RealtimeController(AppDbContext db, IConnectionTracker tracker, ICurrentUser currentUser)
    {
        _db = db;
        _tracker = tracker;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Returns the user ids currently online (have at least one live hub connection)
    /// AND a member of the requested classroom group. Used by the teacher dashboard's
    /// "who's online right now?" widget; cold start hits this once, then the
    /// PresenceHub keeps the UI in sync via UserOnline / UserOffline events.
    /// </summary>
    [HttpGet("presence/classroom/{groupId:guid}")]
    public async Task<ClassroomPresenceResponse> ClassroomPresence(Guid groupId, CancellationToken ct)
    {
        // Same authorisation rule as JoinClassroomAsync — keep the two in sync
        // (extracted to ClassroomConnectionService.JoinAsync if it grows another check).
        var allowed = _currentUser.Role switch
        {
            "admin" => true,
            "teacher" => await _db.Groups.AnyAsync(g => g.Id == groupId && g.TeacherId == _currentUser.Id, ct),
            "student" => await _db.GroupStudents.AnyAsync(gs => gs.GroupId == groupId && gs.StudentId == _currentUser.Id, ct),
            _ => false,
        };
        if (!allowed) throw new ForbiddenException("Not a member of this classroom");

        var memberIds = await _db.GroupStudents
            .Where(gs => gs.GroupId == groupId)
            .Select(gs => gs.StudentId)
            .ToListAsync(ct);

        // Include the teacher in the membership view (they're members of the room too).
        var teacherId = await _db.Groups
            .Where(g => g.Id == groupId)
            .Select(g => (Guid?)g.TeacherId)
            .FirstOrDefaultAsync(ct);
        if (teacherId.HasValue) memberIds.Add(teacherId.Value);

        var memberSet = memberIds.ToHashSet();
        var online = _tracker.GetOnlineUsers().Where(memberSet.Contains).ToList();

        return new ClassroomPresenceResponse
        {
            GroupId = groupId,
            OnlineUserIds = online,
        };
    }
}
