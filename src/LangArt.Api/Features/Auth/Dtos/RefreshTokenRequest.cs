using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Auth.Dtos;

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
