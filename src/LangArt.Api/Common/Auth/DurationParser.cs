using System.Text.RegularExpressions;

namespace LangArt.Api.Common.Auth;

/// <summary>
/// Parses NestJS-style duration strings ("15m", "7d", "30s", "12h") into TimeSpan
/// so the existing JWT_*_EXPIRES_IN env values keep working unchanged.
/// </summary>
public static class DurationParser
{
    private static readonly Regex Pattern = new(@"^\s*(\d+)\s*([smhd])\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static TimeSpan Parse(string value, TimeSpan? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (fallback.HasValue) return fallback.Value;
            throw new ArgumentException("Duration string is empty.", nameof(value));
        }

        // Plain seconds
        if (int.TryParse(value, out var seconds))
            return TimeSpan.FromSeconds(seconds);

        var match = Pattern.Match(value);
        if (!match.Success)
        {
            if (fallback.HasValue) return fallback.Value;
            throw new ArgumentException($"Invalid duration: '{value}'. Expected format like '15m', '7d', '30s', '12h'.", nameof(value));
        }

        var n = int.Parse(match.Groups[1].Value);
        return match.Groups[2].Value.ToLowerInvariant() switch
        {
            "s" => TimeSpan.FromSeconds(n),
            "m" => TimeSpan.FromMinutes(n),
            "h" => TimeSpan.FromHours(n),
            "d" => TimeSpan.FromDays(n),
            _ => throw new ArgumentException($"Invalid duration unit in '{value}'.", nameof(value)),
        };
    }
}
