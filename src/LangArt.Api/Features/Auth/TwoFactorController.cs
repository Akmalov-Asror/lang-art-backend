using System.Security.Cryptography;
using LangArt.Api.Common.Auth;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace LangArt.Api.Features.Auth;

/// <summary>
/// 2FA scaffolding. These endpoints let an admin enrol/verify/disable TOTP, but the
/// **login flow does not yet require a TOTP step** — that needs its own UX session.
/// For now this is a working API + DB schema you can wire into the login flow later.
/// </summary>
[ApiController]
[Route("api/auth/2fa")]
[Authorize(Roles = "admin")]
public class TwoFactorController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public TwoFactorController(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>Generate a new base32 TOTP secret + an <c>otpauth://</c> URL for QR display.</summary>
    [HttpPost("setup")]
    public async Task<TotpSetupResponse> Setup()
    {
        var user = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == _currentUser.Id)
                   ?? throw new UnauthorizedException("User not found");

        var bytes = RandomNumberGenerator.GetBytes(20);
        var secret = Base32Encoding.ToString(bytes);
        // Store the secret immediately — UX flow can decide later whether to require
        // a successful verify before flipping `TotpEnabled` true.
        user.TotpSecret = secret;
        user.TotpEnabled = false;
        await _db.SaveChangesAsync();

        var label = Uri.EscapeDataString($"LangArt LMS:{user.Email}");
        var issuer = Uri.EscapeDataString("LangArt LMS");
        var otpauth = $"otpauth://totp/{label}?secret={secret}&issuer={issuer}&digits=6&period=30";

        return new TotpSetupResponse { Secret = secret, OtpauthUrl = otpauth };
    }

    /// <summary>Verify a 6-digit code generated from the user's authenticator.</summary>
    [HttpPost("verify")]
    public async Task<TotpVerifyResponse> Verify([FromBody] TotpVerifyRequest dto)
    {
        var user = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == _currentUser.Id)
                   ?? throw new UnauthorizedException("User not found");
        if (string.IsNullOrEmpty(user.TotpSecret))
        {
            throw new BadRequestException("Run /api/auth/2fa/setup first to provision a secret.");
        }

        var totp = new Totp(Base32Encoding.ToBytes(user.TotpSecret));
        var ok = totp.VerifyTotp(dto.Code ?? string.Empty, out _, VerificationWindow.RfcSpecifiedNetworkDelay);
        if (ok && !user.TotpEnabled)
        {
            user.TotpEnabled = true;
            await _db.SaveChangesAsync();
        }
        return new TotpVerifyResponse { Valid = ok, Enabled = user.TotpEnabled };
    }

    /// <summary>Disable 2FA for the current user. Future: require a fresh code first.</summary>
    [HttpPost("disable")]
    public async Task<IActionResult> Disable()
    {
        var user = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == _currentUser.Id)
                   ?? throw new UnauthorizedException("User not found");
        user.TotpSecret = null;
        user.TotpEnabled = false;
        await _db.SaveChangesAsync();
        return Ok(new { });
    }
}

public class TotpSetupResponse
{
    public string Secret { get; set; } = string.Empty;
    public string OtpauthUrl { get; set; } = string.Empty;
}

public class TotpVerifyRequest
{
    public string? Code { get; set; }
}

public class TotpVerifyResponse
{
    public bool Valid { get; set; }
    public bool Enabled { get; set; }
}
