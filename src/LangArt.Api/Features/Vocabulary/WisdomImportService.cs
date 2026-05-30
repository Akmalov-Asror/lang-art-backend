using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Vocabulary;

/// <summary>
/// Background importer that mirrors a slice of wisdomlugati.uz' public catalogue
/// into our own Wordlist + Word tables. The site's robots.txt explicitly allows
/// crawling and the catalogue API is unauthenticated; we still throttle to one
/// request per ~600ms and store provenance in each Wordlist.Description.
/// Job history is persisted to <c>wisdom_import_jobs</c> so it survives
/// backend restarts.
/// </summary>
public class WisdomImportService(IHttpClientFactory httpFactory, IServiceScopeFactory scopes, ILogger<WisdomImportService> log)
{
    private const string ApiBase = "https://new-api.wisdomedu.uz";
    private const int ThrottleMs = 600;
    private const int PersistEveryNPages = 1;   // flush job row on every page

    // Cancellation flags for jobs that are still running in THIS process.
    private static readonly ConcurrentDictionary<Guid, bool> CancelFlags = new();

    public async Task<WisdomImportJob> StartImportAsync(Guid ownerId, string? lettersOverride, int minStar)
    {
        var jobId = Guid.NewGuid();
        var letters = (lettersOverride ?? "abcdefghijklmnopqrstuvwxyz").ToLowerInvariant();

        var job = new WisdomImportJob
        {
            Id = jobId,
            StartedAt = DateTime.UtcNow,
            Letters = letters,
            State = "running",
            CurrentLetter = letters[0].ToString(),
            CurrentPage = 0,
            MinStar = minStar,
            OwnerId = ownerId,
        };

        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.WisdomImportJobs.Add(job);
            await db.SaveChangesAsync();
        }

        CancelFlags[jobId] = false;
        _ = Task.Run(() => RunAsync(jobId, ownerId, letters, minStar));
        return job;
    }

    public async Task<WisdomImportJob?> GetStatusAsync(Guid jobId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.WisdomImportJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
    }

    public async Task<List<WisdomImportJob>> ListJobsAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.WisdomImportJobs
            .AsNoTracking()
            .OrderByDescending(j => j.StartedAt)
            .Take(20)
            .ToListAsync(ct);
    }

    public bool CancelJob(Guid jobId)
    {
        if (!CancelFlags.ContainsKey(jobId)) return false;
        CancelFlags[jobId] = true;
        return true;
    }

    private async Task RunAsync(Guid jobId, Guid ownerId, string letters, int minStar)
    {
        var http = httpFactory.CreateClient("wisdom");
        http.DefaultRequestHeaders.UserAgent.ParseAdd("LangArt-LMS-Import/1.0");
        http.DefaultRequestHeaders.Referrer = new Uri("https://wisdomlugati.uz/");

        var levelWordlists = new Dictionary<string, Guid>();

        // Local accumulators — we save them to the row every page.
        var stats = new ImportStats();

        try
        {
            foreach (var letter in letters)
            {
                if (IsCancelled(jobId)) { await Finalize(jobId, "cancelled", null, stats, letter, 0, 0); return; }

                int page = 1;
                int lastPage = 1;

                while (page <= lastPage)
                {
                    if (IsCancelled(jobId)) { await Finalize(jobId, "cancelled", null, stats, letter, page, lastPage); return; }

                    var url = $"{ApiBase}/api/v1/catalogue/search?starts_with={letter}&order=asc&page={page}";
                    WisdomApiResponse? payload;
                    try
                    {
                        payload = await http.GetFromJsonAsync<WisdomApiResponse>(url);
                    }
                    catch (Exception ex)
                    {
                        log.LogWarning(ex, "Wisdom import: fetch failed {Letter} p{Page} — retrying once", letter, page);
                        await Task.Delay(2000);
                        try { payload = await http.GetFromJsonAsync<WisdomApiResponse>(url); }
                        catch (Exception ex2)
                        {
                            log.LogError(ex2, "Wisdom import: fetch failed twice {Letter} p{Page} — skipping page", letter, page);
                            stats.FailedFetches++;
                            page++;
                            await PersistProgress(jobId, stats, letter, page, lastPage);
                            await Task.Delay(ThrottleMs);
                            continue;
                        }
                    }

                    if (payload?.Data?.Items == null || payload.Data.Items.Count == 0) break;
                    lastPage = payload.Meta?.LastPage ?? 1;

                    using (var scope = scopes.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        foreach (var item in payload.Items())
                        {
                            if (item.Translation == null || item.Translation.Count == 0) { stats.Skipped++; continue; }
                            if (item.Star < minStar) { stats.Skipped++; continue; }
                            var term = (item.Word ?? "").Trim();
                            if (term.Length == 0) { stats.Skipped++; continue; }

                            var level = string.IsNullOrWhiteSpace(item.WordLevel) ? "Other" : item.WordLevel!;
                            var wlId = await ResolveWordlistAsync(db, ownerId, level, levelWordlists);

                            var pos = item.WordClass?.Name;
                            var uz = string.Join("; ", item.Translation.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct());

                            var existing = await db.Words.FirstOrDefaultAsync(w =>
                                w.WordlistId == wlId &&
                                w.Term == term &&
                                (w.PartOfSpeech == pos || (w.PartOfSpeech == null && pos == null)));

                            if (existing == null)
                            {
                                db.Words.Add(new Word
                                {
                                    Id = Guid.NewGuid(),
                                    WordlistId = wlId,
                                    Term = term,
                                    TranslationUz = uz,
                                    TranslationRu = string.Empty,
                                    Definition = string.Empty,
                                    PartOfSpeech = pos,
                                    Position = item.Star,
                                    CreatedAt = DateTime.UtcNow,
                                });
                                stats.Inserted++;
                            }
                            else if (existing.TranslationUz != uz)
                            {
                                existing.TranslationUz = uz;
                                stats.Updated++;
                            }
                            else
                            {
                                stats.UnchangedExisting++;
                            }
                        }
                        await db.SaveChangesAsync();
                    }

                    page++;
                    if (page % PersistEveryNPages == 0)
                        await PersistProgress(jobId, stats, letter, page, lastPage);
                    await Task.Delay(ThrottleMs);
                }
                stats.LettersDone++;
                await PersistProgress(jobId, stats, letter, page, lastPage);
            }
            await Finalize(jobId, "completed", null, stats, letters[^1], 0, 0);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Wisdom import: job {JobId} failed", jobId);
            await Finalize(jobId, "failed", ex.Message, stats, '?', 0, 0);
        }
        finally
        {
            CancelFlags.TryRemove(jobId, out _);
        }
    }

    private bool IsCancelled(Guid jobId) => CancelFlags.TryGetValue(jobId, out var c) && c;

    private async Task PersistProgress(Guid jobId, ImportStats stats, char letter, int page, int lastPage)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var letterStr = letter.ToString();
            await db.WisdomImportJobs
                .Where(j => j.Id == jobId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Inserted, stats.Inserted)
                    .SetProperty(j => j.Updated, stats.Updated)
                    .SetProperty(j => j.UnchangedExisting, stats.UnchangedExisting)
                    .SetProperty(j => j.Skipped, stats.Skipped)
                    .SetProperty(j => j.FailedFetches, stats.FailedFetches)
                    .SetProperty(j => j.LettersDone, stats.LettersDone)
                    .SetProperty(j => j.CurrentLetter, letterStr)
                    .SetProperty(j => j.CurrentPage, page)
                    .SetProperty(j => j.LastPage, lastPage));
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Wisdom import: failed to persist progress for {JobId}", jobId);
        }
    }

    private async Task Finalize(Guid jobId, string state, string? error, ImportStats stats, char letter, int page, int lastPage)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var letterStr = letter.ToString();
            await db.WisdomImportJobs
                .Where(j => j.Id == jobId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.State, state)
                    .SetProperty(j => j.Error, error)
                    .SetProperty(j => j.CompletedAt, (DateTime?)DateTime.UtcNow)
                    .SetProperty(j => j.Inserted, stats.Inserted)
                    .SetProperty(j => j.Updated, stats.Updated)
                    .SetProperty(j => j.UnchangedExisting, stats.UnchangedExisting)
                    .SetProperty(j => j.Skipped, stats.Skipped)
                    .SetProperty(j => j.FailedFetches, stats.FailedFetches)
                    .SetProperty(j => j.LettersDone, stats.LettersDone)
                    .SetProperty(j => j.CurrentLetter, letterStr)
                    .SetProperty(j => j.CurrentPage, page)
                    .SetProperty(j => j.LastPage, lastPage));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Wisdom import: failed to finalize {JobId}", jobId);
        }
    }

    private static async Task<Guid> ResolveWordlistAsync(AppDbContext db, Guid ownerId, string level, Dictionary<string, Guid> cache)
    {
        if (cache.TryGetValue(level, out var id)) return id;
        var name = $"Wisdom Lug'ati — {level}";
        var wl = await db.Wordlists.FirstOrDefaultAsync(w => w.Name == name);
        if (wl == null)
        {
            wl = new Wordlist
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = name,
                Description = $"Imported from wisdomlugati.uz — {level} level. Source: https://wisdomlugati.uz/ (educational use; all dictionary content © Wisdom).",
                IsPublic = true,
                Level = level,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.Wordlists.Add(wl);
            await db.SaveChangesAsync();
        }
        cache[level] = wl.Id;
        return wl.Id;
    }

    private class ImportStats
    {
        public int Inserted;
        public int Updated;
        public int UnchangedExisting;
        public int Skipped;
        public int FailedFetches;
        public int LettersDone;
    }
}

// ---- API response shape ----
internal class WisdomApiResponse
{
    [JsonPropertyName("data")] public WisdomData? Data { get; set; }
    [JsonPropertyName("meta")] public WisdomMeta? Meta { get; set; }

    public IEnumerable<WisdomItem> Items() => Data?.Items ?? Enumerable.Empty<WisdomItem>();
}
internal class WisdomData
{
    [JsonPropertyName("items")] public List<WisdomItem>? Items { get; set; }
}
internal class WisdomMeta
{
    [JsonPropertyName("current_page")] public int CurrentPage { get; set; }
    [JsonPropertyName("last_page")] public int LastPage { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
}
internal class WisdomItem
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("word")] public string? Word { get; set; }
    [JsonPropertyName("word_class")] public WisdomWordClass? WordClass { get; set; }
    [JsonPropertyName("word_level")] public string? WordLevel { get; set; }
    [JsonPropertyName("translation")] public List<string>? Translation { get; set; }
    [JsonPropertyName("star")] public int Star { get; set; }
}
internal class WisdomWordClass
{
    [JsonPropertyName("word_class")] public string? Name { get; set; }
}
