using System.ComponentModel.DataAnnotations;
using LangArt.Api.Data.Enums;

namespace LangArt.Api.Features.Auth.Dtos;

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(50)]
    public string Password { get; set; } = string.Empty;

    [Required, MinLength(2), MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public Role? Role { get; set; }
}
