namespace LangArt.Api.Common.Configuration;

/// <summary>
/// Translates a Prisma-style URL (postgresql://user:pass@host:port/db?schema=public)
/// into an Npgsql connection string so the existing DATABASE_URL env keeps working.
/// </summary>
public static class ConnectionStringConverter
{
    public static string ToNpgsql(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("DATABASE_URL is not configured.");

        // Already an Npgsql key/value string
        if (raw.Contains('=') && !raw.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
            return raw;

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

        return $"Host={host};Port={port};Username={user};Password={password};Database={database};Include Error Detail=true";
    }
}
