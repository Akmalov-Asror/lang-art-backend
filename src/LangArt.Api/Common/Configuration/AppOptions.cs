namespace LangArt.Api.Common.Configuration;

public class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public string RefreshSecret { get; set; } = string.Empty;
    public string AccessExpiresIn { get; set; } = "15m";
    public string RefreshExpiresIn { get; set; } = "7d";
}

public class CorsOptions
{
    public string Origin { get; set; } = "http://localhost:5173";
}

public class UploadsOptions
{
    public string Dir { get; set; } = "./uploads";
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;
    public string AllowedTypes { get; set; } = "pdf,doc,docx,ppt,pptx,jpg,jpeg,png,gif,mp3,mp4,webm";
}

public class RateLimitOptions
{
    public int WindowSeconds { get; set; } = 60;
    public int PermitLimit { get; set; } = 100;
}

public class SeedOptions
{
    public string AdminEmail { get; set; } = "admin@langartlms.com";
    public string AdminPassword { get; set; } = "admin123";
}
