using LangArt.Api.Common.Auth;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Auth.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Auth;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly ILogger<AuthService> _logger;
    private readonly IHostEnvironment _env;
    private readonly Common.Email.IEmailSender _email;

    public AuthService(AppDbContext db, JwtTokenService jwt, ILogger<AuthService> logger, IHostEnvironment env, Common.Email.IEmailSender email)
    {
        _db = db;
        _jwt = jwt;
        _logger = logger;
        _env = env;
        _email = email;
    }

    public async Task<AuthTokensResponse> RegisterAsync(RegisterRequest dto, string? userAgent, string? ipAddress)
    {
        var existing = await _db.Profiles.AnyAsync(p => p.Email == dto.Email);
        if (existing) throw new ConflictException("Email already registered");

        var user = new Profile
        {
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 10),
            FullName = dto.FullName,
            Role = dto.Role ?? Role.Student,
            IsActive = true,
        };
        _db.Profiles.Add(user);
        await _db.SaveChangesAsync();

        var tokens = _jwt.Generate(user);
        await StoreRefreshTokenAsync(user.Id, tokens, userAgent, ipAddress);

        return new AuthTokensResponse { AccessToken = tokens.AccessToken, RefreshToken = tokens.RefreshToken };
    }

    public async Task<AuthTokensResponse> LoginAsync(LoginRequest dto, string? userAgent, string? ipAddress)
    {
        var user = await _db.Profiles.FirstOrDefaultAsync(p => p.Email == dto.Email)
                   ?? throw new UnauthorizedException("Invalid credentials");

        if (!user.IsActive) throw new UnauthorizedException("Account is inactive");

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid credentials");

        user.LastLogin = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var tokens = _jwt.Generate(user);
        await StoreRefreshTokenAsync(user.Id, tokens, userAgent, ipAddress);

        return new AuthTokensResponse { AccessToken = tokens.AccessToken, RefreshToken = tokens.RefreshToken };
    }

    public async Task<AuthTokensResponse> RefreshAsync(string refreshToken)
    {
        var principal = _jwt.TryValidateRefreshToken(refreshToken)
                        ?? throw new UnauthorizedException("Invalid refresh token");

        var session = await _db.Sessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.RefreshToken == refreshToken);

        if (session is null || session.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedException("Invalid refresh token");

        session.LastUsedAt = DateTime.UtcNow;

        var tokens = _jwt.Generate(session.User);
        session.RefreshToken = tokens.RefreshToken;
        session.ExpiresAt = tokens.RefreshExpiresAt;
        await _db.SaveChangesAsync();

        return new AuthTokensResponse { AccessToken = tokens.AccessToken, RefreshToken = tokens.RefreshToken };
    }

    public async Task LogoutAsync(Guid userId, string refreshToken)
    {
        await _db.Sessions
            .Where(s => s.UserId == userId && s.RefreshToken == refreshToken)
            .ExecuteDeleteAsync();
    }

    public async Task RequestPasswordResetAsync(string email)
    {
        var user = await _db.Profiles.FirstOrDefaultAsync(p => p.Email == email);
        if (user is null) return;

        var token = JwtTokenService.GenerateResetToken();
        user.ResetToken = token;
        user.ResetTokenExpires = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync();

        var html = $"""
            <p>Hi {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>
            <p>We received a request to reset your LangArt LMS password. Use the token below within the next hour:</p>
            <p style="font-family:monospace;background:#f5f5f5;padding:8px 12px;border-radius:4px;display:inline-block;">{token}</p>
            <p>If you didn't request this, you can safely ignore this email.</p>
            <p>— The LangArt team</p>
        """;
        await _email.SendAsync(email, "Reset your LangArt password", html);

        if (_env.IsDevelopment())
        {
            _logger.LogInformation("[DEV ONLY] Password reset token for {Email}: {Token}", email, token);
        }
    }

    public async Task ResetPasswordAsync(string token, string newPassword)
    {
        var user = await _db.Profiles.FirstOrDefaultAsync(p =>
            p.ResetToken == token &&
            p.ResetTokenExpires != null &&
            p.ResetTokenExpires > DateTime.UtcNow);

        if (user is null) throw new BadRequestException("Invalid or expired reset token");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 10);
        user.ResetToken = null;
        user.ResetTokenExpires = null;
        await _db.SaveChangesAsync();
    }

    public async Task UpdatePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == userId)
                   ?? throw new UnauthorizedException("User not found");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            throw new BadRequestException("Current password is incorrect");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 10);
        await _db.SaveChangesAsync();
    }

    public async Task<ProfileResponse> GetProfileAsync(Guid userId)
    {
        var user = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == userId)
                   ?? throw new UnauthorizedException("User not found");

        return new ProfileResponse
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString().ToLowerInvariant(),
            IsActive = user.IsActive,
            AvatarUrl = user.AvatarUrl,
            TotpEnabled = user.TotpEnabled,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
        };
    }

    public async Task<ProfileResponse> UpdateMyProfileAsync(Guid userId, UpdateMyProfileRequest dto)
    {
        var user = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == userId)
                   ?? throw new UnauthorizedException("User not found");
        if (dto.FullName is not null && dto.FullName.Trim().Length > 0) user.FullName = dto.FullName.Trim();
        if (dto.AvatarUrl is not null) user.AvatarUrl = dto.AvatarUrl.Length == 0 ? null : dto.AvatarUrl;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await GetProfileAsync(userId);
    }

    private async Task StoreRefreshTokenAsync(Guid userId, JwtTokens tokens, string? userAgent, string? ipAddress)
    {
        _db.Sessions.Add(new Session
        {
            UserId = userId,
            RefreshToken = tokens.RefreshToken,
            ExpiresAt = tokens.RefreshExpiresAt,
            UserAgent = userAgent,
            IpAddress = ParseIp(ipAddress),
        });
        await _db.SaveChangesAsync();
    }

    private static System.Net.IPAddress? ParseIp(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return System.Net.IPAddress.TryParse(raw, out var ip) ? ip : null;
    }
}
