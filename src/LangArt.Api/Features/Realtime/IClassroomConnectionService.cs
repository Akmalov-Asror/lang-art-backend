using LangArt.Api.Features.Realtime.Dto;

namespace LangArt.Api.Features.Realtime;

public record JoinResult(bool Allowed, string? Reason = null);

/// <summary>
/// Manages SignalR group membership for the <c>classroom:{groupId}</c> rooms. Hub
/// group membership IS the permission boundary for classroom traffic — every
/// broadcast goes to that group, so a wrong join = data leak. Authorisation lives
/// here in the service (not just an attribute) so it can't be bypassed by adding
/// another hub method later.
/// </summary>
public interface IClassroomConnectionService
{
    Task<JoinResult> JoinAsync(Guid userId, Guid groupId, string connectionId, CancellationToken ct);
    Task LeaveAsync(string connectionId, Guid groupId, CancellationToken ct);

    /// <summary>Server-only broadcast to all live members of a classroom. Best-effort.</summary>
    Task BroadcastToClassroomAsync(Guid groupId, ClassroomEventDto payload, CancellationToken ct);
}
