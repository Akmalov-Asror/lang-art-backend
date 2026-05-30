using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Gamification;
using LangArt.Api.Features.Vocabulary.Dto;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LangArt.Api.Features.Vocabulary;

public class VocabularyService : IVocabularyService
{
    private const int CorrectAnswersForMastery = 3;
    private const int XpPerMasteredWord = 5;
    private const string PersonalWordlistName = "__personal__";

    private readonly AppDbContext _db;
    private readonly IGamificationService _gamification;
    private readonly ILogger<VocabularyService> _logger;

    public VocabularyService(AppDbContext db, IGamificationService gamification, ILogger<VocabularyService> logger)
    {
        _db = db;
        _gamification = gamification;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WordlistDto>> ListWordlistsAsync(Guid currentUserId, string currentRole, CancellationToken ct)
    {
        var q = _db.Wordlists.AsNoTracking().Where(w => w.Name != PersonalWordlistName);
        if (currentRole == "student")
        {
            q = q.Where(w => w.IsPublic || w.OwnerId == currentUserId);
        }
        else if (currentRole == "teacher")
        {
            q = q.Where(w => w.IsPublic || w.OwnerId == currentUserId);
        }
        // admin sees all

        var rows = await q
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => new
            {
                Wordlist = w,
                OwnerName = w.Owner.FullName,
                WordCount = w.Words.Count(),
            })
            .ToListAsync(ct);

        return rows.Select(r => ToDto(r.Wordlist, r.OwnerName, r.WordCount)).ToList();
    }

    public async Task<WordlistDto> GetWordlistAsync(Guid id, Guid currentUserId, string currentRole, CancellationToken ct)
    {
        var wl = await _db.Wordlists
            .AsNoTracking()
            .Include(w => w.Owner)
            .FirstOrDefaultAsync(w => w.Id == id, ct);
        if (wl is null) throw new NotFoundException("Wordlist not found");
        EnsureCanRead(wl, currentUserId, currentRole);

        var count = await _db.Words.CountAsync(w => w.WordlistId == id, ct);
        return ToDto(wl, wl.Owner.FullName, count);
    }

    public async Task<WordlistDto> CreateWordlistAsync(Guid ownerId, CreateWordlistRequest req, CancellationToken ct)
    {
        var wl = new Wordlist
        {
            OwnerId = ownerId,
            Name = req.Name.Trim(),
            Description = req.Description,
            IsPublic = req.IsPublic,
            Level = req.Level,
        };
        _db.Wordlists.Add(wl);
        await _db.SaveChangesAsync(ct);

        var owner = await _db.Profiles.AsNoTracking().FirstAsync(p => p.Id == ownerId, ct);
        return ToDto(wl, owner.FullName, 0);
    }

    public async Task<WordlistDto> UpdateWordlistAsync(Guid id, Guid currentUserId, string currentRole, UpdateWordlistRequest req, CancellationToken ct)
    {
        var wl = await _db.Wordlists.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (wl is null) throw new NotFoundException("Wordlist not found");
        EnsureCanWrite(wl, currentUserId, currentRole);

        wl.Name = req.Name.Trim();
        wl.Description = req.Description;
        wl.IsPublic = req.IsPublic;
        wl.Level = req.Level;
        wl.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var owner = await _db.Profiles.AsNoTracking().FirstAsync(p => p.Id == wl.OwnerId, ct);
        var count = await _db.Words.CountAsync(w => w.WordlistId == id, ct);
        return ToDto(wl, owner.FullName, count);
    }

    public async Task DeleteWordlistAsync(Guid id, Guid currentUserId, string currentRole, CancellationToken ct)
    {
        var wl = await _db.Wordlists.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (wl is null) throw new NotFoundException("Wordlist not found");
        EnsureCanWrite(wl, currentUserId, currentRole);
        if (wl.Name == PersonalWordlistName)
        {
            throw new BadRequestException("Cannot delete the personal wordlist directly");
        }
        _db.Wordlists.Remove(wl);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WordDto>> ListWordsAsync(Guid wordlistId, Guid currentUserId, string currentRole, CancellationToken ct)
    {
        var wl = await _db.Wordlists.AsNoTracking().FirstOrDefaultAsync(w => w.Id == wordlistId, ct);
        if (wl is null) throw new NotFoundException("Wordlist not found");
        EnsureCanRead(wl, currentUserId, currentRole);

        var words = await _db.Words
            .AsNoTracking()
            .Where(w => w.WordlistId == wordlistId)
            .OrderBy(w => w.Position)
            .ThenBy(w => w.CreatedAt)
            .ToListAsync(ct);
        return words.Select(ToDto).ToList();
    }

    public async Task<PagedWordsDto> ListWordsPagedAsync(Guid wordlistId, Guid currentUserId, string currentRole, string? search, int page, int pageSize, CancellationToken ct)
    {
        var wl = await _db.Wordlists.AsNoTracking().FirstOrDefaultAsync(w => w.Id == wordlistId, ct);
        if (wl is null) throw new NotFoundException("Wordlist not found");
        EnsureCanRead(wl, currentUserId, currentRole);

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;

        var q = _db.Words.AsNoTracking().Where(w => w.WordlistId == wordlistId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(w =>
                w.Term.ToLower().Contains(s) ||
                w.TranslationUz.ToLower().Contains(s) ||
                w.TranslationRu.ToLower().Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(w => w.Position)   // higher star/position first
            .ThenBy(w => w.Term)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedWordsDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
        };
    }

    public async Task<IReadOnlyList<WordDto>> AddWordsAsync(Guid wordlistId, Guid currentUserId, string currentRole, AddWordsRequest req, CancellationToken ct)
    {
        var wl = await _db.Wordlists.FirstOrDefaultAsync(w => w.Id == wordlistId, ct);
        if (wl is null) throw new NotFoundException("Wordlist not found");
        EnsureCanWrite(wl, currentUserId, currentRole);

        var existingCount = await _db.Words.CountAsync(w => w.WordlistId == wordlistId, ct);
        var newWords = req.Words.Select((input, i) => new Word
        {
            WordlistId = wordlistId,
            Term = input.Term.Trim(),
            TranslationUz = input.TranslationUz ?? string.Empty,
            TranslationRu = input.TranslationRu ?? string.Empty,
            Definition = input.Definition ?? string.Empty,
            PronunciationUrl = input.PronunciationUrl,
            ExampleSentence = input.ExampleSentence,
            PartOfSpeech = input.PartOfSpeech,
            Position = existingCount + i,
            ImageUrl = input.ImageUrl,
            FrequencyRank = input.FrequencyRank,
            TopicTags = input.TopicTags,
        }).ToList();
        _db.Words.AddRange(newWords);
        wl.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return newWords.Select(ToDto).ToList();
    }

    public async Task<WordDto> UpdateWordAsync(Guid wordlistId, Guid wordId, Guid currentUserId, string currentRole, UpdateWordRequest req, CancellationToken ct)
    {
        var wl = await _db.Wordlists.FirstOrDefaultAsync(w => w.Id == wordlistId, ct);
        if (wl is null) throw new NotFoundException("Wordlist not found");
        EnsureCanWrite(wl, currentUserId, currentRole);

        var word = await _db.Words.FirstOrDefaultAsync(w => w.Id == wordId && w.WordlistId == wordlistId, ct);
        if (word is null) throw new NotFoundException("Word not found");

        word.Term = (req.Term ?? word.Term).Trim();
        word.TranslationUz = req.TranslationUz ?? string.Empty;
        word.TranslationRu = req.TranslationRu ?? string.Empty;
        word.Definition = req.Definition ?? string.Empty;
        word.PronunciationUrl = req.PronunciationUrl;
        word.ExampleSentence = req.ExampleSentence;
        word.PartOfSpeech = req.PartOfSpeech;
        word.ImageUrl = req.ImageUrl;
        word.FrequencyRank = req.FrequencyRank;
        word.TopicTags = req.TopicTags;

        wl.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(word);
    }

    public async Task DeleteWordAsync(Guid wordlistId, Guid wordId, Guid currentUserId, string currentRole, CancellationToken ct)
    {
        var wl = await _db.Wordlists.FirstOrDefaultAsync(w => w.Id == wordlistId, ct);
        if (wl is null) throw new NotFoundException("Wordlist not found");
        EnsureCanWrite(wl, currentUserId, currentRole);

        var word = await _db.Words.FirstOrDefaultAsync(w => w.Id == wordId && w.WordlistId == wordlistId, ct);
        if (word is null) throw new NotFoundException("Word not found");
        _db.Words.Remove(word);
        wl.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<UserWordlistEntryDto> AddToMyWordlistAsync(Guid userId, AddToMyWordlistRequest req, CancellationToken ct)
    {
        Word word;
        if (req.WordId is { } existingId)
        {
            var existing = await _db.Words.AsNoTracking().FirstOrDefaultAsync(w => w.Id == existingId, ct);
            if (existing is null) throw new NotFoundException("Word not found");
            word = existing;
        }
        else if (req.AdHoc is { } adHoc)
        {
            // Lazily provision the user's personal wordlist on first add.
            var personal = await _db.Wordlists.FirstOrDefaultAsync(
                w => w.OwnerId == userId && w.Name == PersonalWordlistName, ct);
            if (personal is null)
            {
                personal = new Wordlist
                {
                    OwnerId = userId,
                    Name = PersonalWordlistName,
                    Description = "Personal ad-hoc word collection",
                    IsPublic = false,
                    Level = "A1",
                };
                _db.Wordlists.Add(personal);
                await _db.SaveChangesAsync(ct);
            }

            word = new Word
            {
                WordlistId = personal.Id,
                Term = adHoc.Term.Trim(),
                TranslationUz = adHoc.TranslationUz ?? string.Empty,
                TranslationRu = adHoc.TranslationRu ?? string.Empty,
                Definition = adHoc.Definition ?? string.Empty,
                ExampleSentence = adHoc.ExampleSentence,
                PartOfSpeech = adHoc.PartOfSpeech,
                ImageUrl = adHoc.ImageUrl,
                FrequencyRank = adHoc.FrequencyRank,
                TopicTags = adHoc.TopicTags,
            };
            _db.Words.Add(word);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            throw new BadRequestException("Either wordId or adHoc must be provided");
        }

        var entry = new UserWordlistEntry
        {
            UserId = userId,
            WordId = word.Id,
        };
        _db.UserWordlistEntries.Add(entry);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            // Duplicate add — return the existing entry idempotently.
            foreach (var e in _db.ChangeTracker.Entries<UserWordlistEntry>().Where(e => e.State == EntityState.Added).ToList())
            {
                e.State = EntityState.Detached;
            }
            entry = await _db.UserWordlistEntries
                .AsNoTracking()
                .FirstAsync(e => e.UserId == userId && e.WordId == word.Id, ct);
        }

        return new UserWordlistEntryDto
        {
            Id = entry.Id,
            Word = ToDto(word),
            Status = entry.Status,
            LastReviewedAt = entry.LastReviewedAt,
            CorrectCount = entry.CorrectCount,
            IncorrectCount = entry.IncorrectCount,
            AddedAt = entry.AddedAt == default ? DateTime.UtcNow : entry.AddedAt,
        };
    }

    public async Task<IReadOnlyList<UserWordlistEntryDto>> GetMyWordlistAsync(Guid userId, CancellationToken ct)
    {
        var rows = await _db.UserWordlistEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Include(e => e.Word)
            .OrderByDescending(e => e.AddedAt)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<UserWordlistEntryDto> UpdateEntryStatusAsync(Guid userId, Guid entryId, UpdateEntryStatusRequest req, CancellationToken ct)
    {
        var allowed = new[] { "new", "learning", "learned" };
        if (!allowed.Contains(req.Status))
        {
            throw new BadRequestException("Status must be one of: new, learning, learned");
        }

        var entry = await _db.UserWordlistEntries
            .Include(e => e.Word)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId, ct);
        if (entry is null) throw new NotFoundException("Entry not found");

        entry.Status = req.Status;
        entry.LastReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task RemoveFromMyWordlistAsync(Guid userId, Guid entryId, CancellationToken ct)
    {
        var deleted = await _db.UserWordlistEntries
            .Where(e => e.Id == entryId && e.UserId == userId)
            .ExecuteDeleteAsync(ct);
        if (deleted == 0) throw new NotFoundException("Entry not found");
    }

    public async Task<IReadOnlyList<UserWordlistEntryDto>> GetReviewDueAsync(Guid userId, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 50);
        var rows = await _db.UserWordlistEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Status != "learned")
            .Include(e => e.Word)
            .OrderBy(e => e.LastReviewedAt ?? DateTime.MinValue)
            .Take(limit)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<FlashcardResultResponse> RecordFlashcardResultAsync(Guid userId, FlashcardResultRequest req, CancellationToken ct)
    {
        var entry = await _db.UserWordlistEntries
            .FirstOrDefaultAsync(e => e.UserId == userId && e.WordId == req.WordId, ct);
        if (entry is null)
        {
            // Auto-add to the personal collection on first flashcard interaction.
            entry = (await AddToMyWordlistAsync(userId, new AddToMyWordlistRequest { WordId = req.WordId }, ct))
                is var dto && dto is not null
                ? await _db.UserWordlistEntries.FirstAsync(e => e.UserId == userId && e.WordId == req.WordId, ct)
                : throw new NotFoundException("Word not found");
        }

        if (req.Correct)
        {
            entry.CorrectCount += 1;
        }
        else
        {
            entry.IncorrectCount += 1;
        }
        entry.LastReviewedAt = DateTime.UtcNow;

        var becameMastered = false;
        if (req.Correct && entry.Status != "learned" && entry.CorrectCount >= CorrectAnswersForMastery)
        {
            entry.Status = "learned";
            becameMastered = true;
        }
        else if (entry.Status == "new")
        {
            entry.Status = "learning";
        }
        await _db.SaveChangesAsync(ct);

        var xpAwarded = false;
        if (becameMastered)
        {
            // wordId is a stable per-(user, word) key for idempotency.
            xpAwarded = await _gamification.AwardXpAsync(userId, XpReason.VocabularyMastered, XpPerMasteredWord, entry.WordId, ct);
            try { await _gamification.RecordActivityAsync(userId, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "RecordActivity failed for user {UserId}", userId); }
            try { _ = await _gamification.EvaluateBadgesAsync(userId, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "EvaluateBadges failed for user {UserId}", userId); }
        }

        return new FlashcardResultResponse
        {
            EntryId = entry.Id,
            Status = entry.Status,
            CorrectCount = entry.CorrectCount,
            IncorrectCount = entry.IncorrectCount,
            XpAwarded = xpAwarded,
        };
    }

    private static void EnsureCanRead(Wordlist wl, Guid currentUserId, string currentRole)
    {
        if (currentRole == "admin") return;
        if (wl.IsPublic) return;
        if (wl.OwnerId == currentUserId) return;
        throw new ForbiddenException("You do not have access to this wordlist");
    }

    private static void EnsureCanWrite(Wordlist wl, Guid currentUserId, string currentRole)
    {
        if (currentRole == "admin") return;
        if (wl.OwnerId == currentUserId) return;
        throw new ForbiddenException("You can only edit your own wordlists");
    }

    private static WordlistDto ToDto(Wordlist w, string ownerName, int wordCount) => new()
    {
        Id = w.Id,
        OwnerId = w.OwnerId,
        OwnerName = ownerName,
        Name = w.Name,
        Description = w.Description,
        IsPublic = w.IsPublic,
        Level = w.Level,
        WordCount = wordCount,
        CreatedAt = w.CreatedAt,
        UpdatedAt = w.UpdatedAt,
    };

    private static WordDto ToDto(Word w) => new()
    {
        Id = w.Id,
        WordlistId = w.WordlistId,
        Term = w.Term,
        TranslationUz = w.TranslationUz,
        TranslationRu = w.TranslationRu,
        Definition = w.Definition,
        PronunciationUrl = w.PronunciationUrl,
        ExampleSentence = w.ExampleSentence,
        PartOfSpeech = w.PartOfSpeech,
        Position = w.Position,
        ImageUrl = w.ImageUrl,
        FrequencyRank = w.FrequencyRank,
        TopicTags = w.TopicTags,
    };

    private static UserWordlistEntryDto ToDto(UserWordlistEntry e) => new()
    {
        Id = e.Id,
        Word = ToDto(e.Word),
        Status = e.Status,
        LastReviewedAt = e.LastReviewedAt,
        CorrectCount = e.CorrectCount,
        IncorrectCount = e.IncorrectCount,
        AddedAt = e.AddedAt,
        DayNumber = e.DayNumber,
        Source = e.Source,
        AssignedDate = e.AssignedDate,
    };
}
