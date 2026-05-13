namespace LangArt.Api.Features.Auth.Dtos;

/// <summary>
/// Response shape for login/register/refresh. Snake-cased on the wire by the global JSON
/// naming policy, so the frontend receives <c>{ access_token, refresh_token }</c>.
/// </summary>
public class AuthTokensResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class ProfileResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? AvatarUrl { get; set; }
    public bool TotpEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateMyProfileRequest
{
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
}
