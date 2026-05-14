namespace LangArt.Api.Features.Live;

/// <summary>
/// Hub-side bookkeeping: which session a single WebSocket connection belongs to.
/// Stored in <see cref="SessionRegistry"/> so <c>OnDisconnectedAsync</c> can clean up
/// without scanning every session.
/// </summary>
public record ConnectionInfo(string ConnectionId, Guid SessionId, Guid UserId, string Role);
