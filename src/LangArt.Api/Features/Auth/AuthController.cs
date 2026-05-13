using LangArt.Api.Common.Auth;
using LangArt.Api.Features.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly ICurrentUser _currentUser;

    public AuthController(AuthService auth, ICurrentUser currentUser)
    {
        _auth = auth;
        _currentUser = currentUser;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest dto)
    {
        var tokens = await _auth.RegisterAsync(dto, Request.Headers.UserAgent, ClientIp());
        return Ok(tokens);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest dto)
    {
        var tokens = await _auth.LoginAsync(dto, Request.Headers.UserAgent, ClientIp());
        return Ok(tokens);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest dto)
    {
        var tokens = await _auth.RefreshAsync(dto.RefreshToken);
        return Ok(tokens);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest dto)
    {
        await _auth.LogoutAsync(_currentUser.Id, dto.RefreshToken);
        return Ok(new { });
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest dto)
    {
        await _auth.RequestPasswordResetAsync(dto.Email);
        return Ok(new { });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest dto)
    {
        await _auth.ResetPasswordAsync(dto.Token, dto.NewPassword);
        return Ok(new { });
    }

    [Authorize]
    [HttpPost("me")]
    public async Task<IActionResult> Me()
    {
        var profile = await _auth.GetProfileAsync(_currentUser.Id);
        return Ok(profile);
    }

    [Authorize]
    [HttpPost("update-password")]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest dto)
    {
        await _auth.UpdatePasswordAsync(_currentUser.Id, dto.CurrentPassword, dto.NewPassword);
        return Ok(new { });
    }

    [Authorize]
    [HttpPut("me")]
    public Task<ProfileResponse> UpdateMe([FromBody] UpdateMyProfileRequest dto) =>
        _auth.UpdateMyProfileAsync(_currentUser.Id, dto);

    private string? ClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString();
}
