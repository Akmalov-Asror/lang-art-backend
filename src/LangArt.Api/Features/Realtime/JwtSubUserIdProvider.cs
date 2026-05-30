using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.SignalR;

namespace LangArt.Api.Features.Realtime;

/// <summary>
/// SignalR's default <see cref="DefaultUserIdProvider"/> reads <c>ClaimTypes.NameIdentifier</c>.
/// Our JWT issues BOTH that and the JWT-standard <c>sub</c> claim with the same value
/// (see <c>JwtTokenService.BuildClaims</c>), so the default would work today. Binding
/// explicitly to <c>sub</c> here keeps SignalR routing correct if the
/// NameIdentifier claim ever moves or changes shape.
/// </summary>
public class JwtSubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var user = connection.User;
        if (user is null) return null;
        return user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
               ?? user.FindFirst("sub")?.Value
               ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
}
