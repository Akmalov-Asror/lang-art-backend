using LangArt.Api.Features.Realtime.Dto;

namespace LangArt.Api.Features.Realtime.Hubs;

/// <summary>
/// Strongly-typed client interface for <see cref="NotificationsHub"/>. The hub method
/// names here are also the wire method names the frontend subscribes to via
/// <c>connection.on("NotificationReceived", …)</c>. Renaming any of these is a
/// breaking change for the React client.
/// </summary>
public interface INotificationsClient
{
    Task NotificationReceived(NotificationDto payload);
    Task BadgeEarned(BadgeEarnedDto payload);

    /// <summary>Broadcast to everyone in a classroom group when interesting state changes.</summary>
    Task ClassroomEvent(ClassroomEventDto payload);
}
