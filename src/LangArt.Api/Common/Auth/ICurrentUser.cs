using System.Security.Claims;
using LangArt.Api.Common.Exceptions;

namespace LangArt.Api.Common.Auth;

/// <summary>
/// Equivalent to NestJS's <c>@CurrentUser()</c>. Reads the authenticated user from
/// the JWT claims on the current HttpContext.
/// </summary>
public interface ICurrentUser
{
    Guid Id { get; }
    string Email { get; }
    string Role { get; }
    bool IsAuthenticated { get; }
    Guid? TryGetId();
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid Id =>
        TryGetId() ?? throw new UnauthorizedException("Not authenticated.");

    public string Email =>
        Principal?.FindFirstValue(ClaimTypes.Email)
        ?? Principal?.FindFirstValue("email")
        ?? throw new UnauthorizedException("Email claim missing.");

    public string Role =>
        Principal?.FindFirstValue(ClaimTypes.Role)
        ?? Principal?.FindFirstValue("role")
        ?? throw new UnauthorizedException("Role claim missing.");

    public Guid? TryGetId()
    {
        var sub = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? Principal?.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
