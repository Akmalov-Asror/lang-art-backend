using LangArt.Api.Data;
using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Realtime.Dto;
using LangArt.Api.Features.Realtime.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Realtime;

public class ClassroomConnectionService : IClassroomConnectionService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationsHub, INotificationsClient> _hub;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<ClassroomConnectionService> _logger;

    public ClassroomConnectionService(
        AppDbContext db,
        IHubContext<NotificationsHub, INotificationsClient> hub,
        INotificationDispatcher dispatcher,
        ILogger<ClassroomConnectionService> logger)
    {
        _db = db;
        _hub = hub;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task<JoinResult> JoinAsync(Guid userId, Guid groupId, string connectionId, CancellationToken ct)
    {
        var groupExists = await _db.Groups.AnyAsync(g => g.Id == groupId, ct);
        if (!groupExists) return new JoinResult(false, "Group not found");

        // Admins always allowed; teachers allowed if they own the group;
        // students allowed if they're enrolled in the group.
        var user = await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == userId, ct);
        if (user is null) return new JoinResult(false, "User not found");

        bool allowed = user.Role switch
        {
            Role.Admin => true,
            Role.Teacher => await _db.Groups.AnyAsync(g => g.Id == groupId && g.TeacherId == userId, ct),
            Role.Student => await _db.GroupStudents.AnyAsync(gs => gs.GroupId == groupId && gs.StudentId == userId, ct),
            _ => false,
        };

        if (!allowed)
        {
            _logger.LogInformation("Refusing classroom join: user {UserId} for group {GroupId}", userId, groupId);
            return new JoinResult(false, "Not a member of this classroom");
        }

        await _hub.Groups.AddToGroupAsync(connectionId, $"classroom:{groupId}", ct);

        // Best-effort presence announcement to the rest of the room.
        await _dispatcher.SendClassroomEventAsync(groupId, new ClassroomEventDto
        {
            Type = "user_joined",
            ActorUserId = userId,
            AtUtc = DateTime.UtcNow,
        }, ct);

        return new JoinResult(true);
    }

    public async Task LeaveAsync(string connectionId, Guid groupId, CancellationToken ct)
    {
        await _hub.Groups.RemoveFromGroupAsync(connectionId, $"classroom:{groupId}", ct);
    }

    public Task BroadcastToClassroomAsync(Guid groupId, ClassroomEventDto payload, CancellationToken ct) =>
        _dispatcher.SendClassroomEventAsync(groupId, payload, ct);
}
