using LangArt.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Gamification;

/// <summary>
/// Hourly sweep that zeroes <c>current_streak</c> for anyone whose last
/// recorded activity is older than yesterday (UTC). <c>longest_streak</c> is
/// preserved — we only reset the live counter.
/// </summary>
public class StreakResetService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<StreakResetService> _logger;

    public StreakResetService(IServiceScopeFactory scopes, ILogger<StreakResetService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run once on startup, then every hour. Tick failures are logged but never crash the host.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StreakResetService tick failed; will retry next interval");
            }

            try
            {
                await Task.Delay(TickInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        var resetCount = await db.UserStreaks
            .Where(s => s.CurrentStreak > 0 && s.LastActivityDateUtc < yesterday)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CurrentStreak, 0), ct);

        if (resetCount > 0)
        {
            _logger.LogInformation("StreakResetService reset {Count} expired streak(s)", resetCount);
        }
    }
}
