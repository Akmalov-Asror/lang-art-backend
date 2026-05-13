using System.Net;

namespace LangArt.Api.Data.Entities;

public class Session
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public string? UserAgent { get; set; }
    public IPAddress? IpAddress { get; set; }   // Postgres column type is `inet`

    public Profile User { get; set; } = null!;
}
