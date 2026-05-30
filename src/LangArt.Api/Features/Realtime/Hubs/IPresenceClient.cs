namespace LangArt.Api.Features.Realtime.Hubs;

public interface IPresenceClient
{
    Task UserOnline(Guid userId);
    Task UserOffline(Guid userId);
}
