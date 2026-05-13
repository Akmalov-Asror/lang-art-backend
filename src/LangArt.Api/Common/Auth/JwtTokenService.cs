using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LangArt.Api.Common.Configuration;
using LangArt.Api.Data.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LangArt.Api.Common.Auth;

public class JwtTokens
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshExpiresAt { get; set; }
}

public class JwtTokenService
{
    private readonly JwtOptions _opt;
    private readonly TimeSpan _accessTtl;
    private readonly TimeSpan _refreshTtl;
    private readonly SigningCredentials _accessCreds;
    private readonly SigningCredentials _refreshCreds;
    private readonly TokenValidationParameters _refreshValidation;
    private readonly JwtSecurityTokenHandler _handler = new();

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _opt = options.Value;
        _accessTtl = DurationParser.Parse(_opt.AccessExpiresIn, TimeSpan.FromMinutes(15));
        _refreshTtl = DurationParser.Parse(_opt.RefreshExpiresIn, TimeSpan.FromDays(7));

        var accessKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Secret));
        var refreshKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.RefreshSecret));
        _accessCreds = new SigningCredentials(accessKey, SecurityAlgorithms.HmacSha256);
        _refreshCreds = new SigningCredentials(refreshKey, SecurityAlgorithms.HmacSha256);

        _refreshValidation = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = refreshKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5),
        };
    }

    public JwtTokens Generate(Profile user)
    {
        var now = DateTime.UtcNow;
        var claims = BuildClaims(user);

        var access = new JwtSecurityToken(
            claims: claims,
            notBefore: now,
            expires: now.Add(_accessTtl),
            signingCredentials: _accessCreds);

        var refresh = new JwtSecurityToken(
            claims: claims,
            notBefore: now,
            expires: now.Add(_refreshTtl),
            signingCredentials: _refreshCreds);

        return new JwtTokens
        {
            AccessToken = _handler.WriteToken(access),
            RefreshToken = _handler.WriteToken(refresh),
            RefreshExpiresAt = now.Add(_refreshTtl),
        };
    }

    public ClaimsPrincipal? TryValidateRefreshToken(string token)
    {
        try
        {
            return _handler.ValidateToken(token, _refreshValidation, out _);
        }
        catch
        {
            return null;
        }
    }

    public static string GenerateResetToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static List<Claim> BuildClaims(Profile user) => new()
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role.ToString().ToLowerInvariant()),
        new Claim("role", user.Role.ToString().ToLowerInvariant()),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
    };
}
