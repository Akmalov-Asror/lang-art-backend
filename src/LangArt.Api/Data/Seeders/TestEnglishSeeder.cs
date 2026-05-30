using System.Text.Json;
using System.Text.Json.Serialization;
using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Data.Seeders;

/// <summary>
/// Imports grammar curricula from JSON files produced by
/// <c>scripts/scrape-test-english/</c>. Each JSON describes one grammar topic
/// (explanation markdown + 1-4 exercises). The seeder upserts one course per
/// CEFR level ("English A1 Grammar", "English A2 Grammar", …), one module
/// ("Grammar"), and one lesson per topic.
///
/// Looks for JSON in (in order): TEST_ENGLISH_SEED_DIR env var,
/// {ContentRoot}/Data/Seeders/test-english/, {repo}/scripts/scrape-test-english/output/.
///
/// Idempotent: re-running skips lessons that already exist in the module
/// (matched by Title).
/// </summary>
public static class TestEnglishSeeder
{
    private static readonly JsonSerializerOptions JsonReadOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions JsonWriteOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly Dictionary<string, string> LevelCourseTitles = new()
    {
        ["a1"] = "English A1 Grammar",
        ["a2"] = "English A2 Grammar",
        ["b1"] = "English B1 Grammar",
        ["b1plus"] = "English B1+ Grammar",
        ["b2"] = "English B2 Grammar",
        ["c1"] = "English C1 Grammar",
    };

    private static readonly Dictionary<string, string> LevelDescriptions = new()
    {
        ["a1"] = "Beginner-level English grammar: tenses, basic word order, articles, prepositions.",
        ["a2"] = "Elementary English grammar: past tenses, comparatives, modals, daily-routine vocabulary.",
        ["b1"] = "Intermediate English grammar: conditionals, reported speech, passives, relative clauses.",
        ["b1plus"] = "Upper-elementary English grammar bridging B1 to B2.",
        ["b2"] = "Upper-intermediate English grammar: advanced tenses, inversion, complex conditionals.",
        ["c1"] = "Advanced English grammar: cleft sentences, ellipsis, nuanced modality, register.",
    };

    private static readonly Dictionary<string, string> LevelThumbnails = new()
    {
        ["a1"] = "https://images.unsplash.com/photo-1503676260728-1c00da094a0b?w=800&q=80",
        ["a2"] = "https://images.unsplash.com/photo-1456513080510-7bf3a84b82f8?w=800&q=80",
        ["b1"] = "https://images.unsplash.com/photo-1457369804613-52c61a468e7d?w=800&q=80",
        ["b1plus"] = "https://images.unsplash.com/photo-1532153975070-2e9ab71f1b14?w=800&q=80",
        ["b2"] = "https://images.unsplash.com/photo-1519682337058-a94d519337bc?w=800&q=80",
        ["c1"] = "https://images.unsplash.com/photo-1481627834876-b7833e8f5570?w=800&q=80",
    };

    public static async Task RunAsync(
        AppDbContext db,
        IWebHostEnvironment env,
        ILogger logger,
        bool force = false)
    {
        var dir = ResolveSeedDirectory(env, logger);
        if (dir is null)
        {
            logger.LogWarning(
                "TestEnglishSeeder: no JSON directory found (set TEST_ENGLISH_SEED_DIR or place files under Data/Seeders/test-english/). Skipping.");
            return;
        }

        logger.LogInformation("TestEnglishSeeder: reading from {Dir}", dir);

        var lessons = await LoadLessonsAsync(dir, logger);
        if (lessons.Count == 0)
        {
            logger.LogWarning("TestEnglishSeeder: no lesson JSON files found in {Dir}", dir);
            return;
        }

        var byLevel = lessons
            .GroupBy(l => l.Level.ToLowerInvariant())
            .Where(g => LevelCourseTitles.ContainsKey(g.Key))
            .ToList();

        foreach (var group in byLevel)
        {
            await SeedLevelAsync(db, group.Key, group.OrderBy(l => l.OrderIndex).ToList(), logger, force);
        }
    }

    private static string? ResolveSeedDirectory(IWebHostEnvironment env, ILogger logger)
    {
        var fromEnv = Environment.GetEnvironmentVariable("TEST_ENGLISH_SEED_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
        {
            return Path.GetFullPath(fromEnv);
        }

        var inContent = Path.Combine(env.ContentRootPath, "Data", "Seeders", "test-english");
        if (Directory.Exists(inContent)) return inContent;

        var inRepo = Path.GetFullPath(Path.Combine(
            env.ContentRootPath, "..", "..", "..", "scripts", "scrape-test-english", "output"));
        if (Directory.Exists(inRepo)) return inRepo;

        return null;
    }

    private static async Task<List<LessonJson>> LoadLessonsAsync(string dir, ILogger logger)
    {
        var files = Directory
            .EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith("_"))
            .ToList();

        var lessons = new List<LessonJson>(files.Count);
        foreach (var file in files)
        {
            try
            {
                await using var fs = File.OpenRead(file);
                var lesson = await JsonSerializer.DeserializeAsync<LessonJson>(fs, JsonReadOpts);
                if (lesson is null) continue;
                if (string.IsNullOrWhiteSpace(lesson.Level) || string.IsNullOrWhiteSpace(lesson.Slug))
                {
                    logger.LogWarning("TestEnglishSeeder: skipping {File} (missing level/slug)", file);
                    continue;
                }
                lessons.Add(lesson);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "TestEnglishSeeder: failed to parse {File}", file);
            }
        }
        return lessons;
    }

    private static async Task SeedLevelAsync(
        AppDbContext db,
        string level,
        List<LessonJson> lessons,
        ILogger logger,
        bool force)
    {
        var courseTitle = LevelCourseTitles[level];
        var thumbnail = LevelThumbnails.GetValueOrDefault(level);
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Title == courseTitle);

        if (course is null)
        {
            course = new Course
            {
                Title = courseTitle,
                Description = LevelDescriptions.GetValueOrDefault(level),
                ThumbnailUrl = thumbnail,
                PriceMonthly = 19.99m,
            };
            db.Courses.Add(course);
            await db.SaveChangesAsync();
            logger.LogInformation("TestEnglishSeeder: created course {Title}", courseTitle);
        }
        else if (string.IsNullOrEmpty(course.ThumbnailUrl) && !string.IsNullOrEmpty(thumbnail))
        {
            course.ThumbnailUrl = thumbnail;
            await db.SaveChangesAsync();
        }

        var module = await db.Modules.FirstOrDefaultAsync(m => m.CourseId == course.Id && m.Title == "Grammar");
        if (module is null)
        {
            module = new Data.Entities.Module
            {
                CourseId = course.Id,
                Title = "Grammar",
                OrderIndex = 0,
            };
            db.Modules.Add(module);
            await db.SaveChangesAsync();
        }

        int created = 0, skipped = 0;
        foreach (var lessonJson in lessons)
        {
            var existing = await db.Lessons
                .FirstOrDefaultAsync(l => l.ModuleId == module.Id && l.Title == lessonJson.Title);

            if (existing is not null && !force)
            {
                skipped += 1;
                continue;
            }
            if (existing is not null && force)
            {
                await db.LessonContent.Where(c => c.LessonId == existing.Id).ExecuteDeleteAsync();
                db.Lessons.Remove(existing);
                await db.SaveChangesAsync();
            }

            await CreateLessonAsync(db, module.Id, lessonJson);
            created += 1;
        }

        var attached = await AttachCourseToGroupsAsync(db, course.Id);

        logger.LogInformation(
            "TestEnglishSeeder: level={Level} lessons created={Created} skipped={Skipped} groups_attached={Attached}",
            level, created, skipped, attached);
    }

    /// <summary>
    /// Adds this course to every existing group that doesn't already have it,
    /// so all current students see the new grammar lessons on their dashboard.
    /// Idempotent (skips existing group_courses rows).
    /// </summary>
    private static async Task<int> AttachCourseToGroupsAsync(AppDbContext db, Guid courseId)
    {
        var groupIds = await db.Groups.Select(g => g.Id).ToListAsync();
        if (groupIds.Count == 0) return 0;

        var alreadyAttached = await db.GroupCourses
            .Where(gc => gc.CourseId == courseId)
            .Select(gc => gc.GroupId)
            .ToListAsync();
        var alreadySet = new HashSet<Guid>(alreadyAttached);

        int added = 0;
        foreach (var gid in groupIds)
        {
            if (alreadySet.Contains(gid)) continue;
            db.GroupCourses.Add(new GroupCourse { GroupId = gid, CourseId = courseId });
            added += 1;
        }
        if (added > 0) await db.SaveChangesAsync();
        return added;
    }

    private static async Task CreateLessonAsync(AppDbContext db, Guid moduleId, LessonJson lessonJson)
    {
        var lesson = new Lesson
        {
            ModuleId = moduleId,
            Title = string.IsNullOrWhiteSpace(lessonJson.Title) ? lessonJson.Slug : lessonJson.Title,
            OrderIndex = lessonJson.OrderIndex,
        };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();

        var contentIndex = 0;

        if (!string.IsNullOrWhiteSpace(lessonJson.Explanation?.Markdown))
        {
            db.LessonContent.Add(new LessonContent
            {
                LessonId = lesson.Id,
                Type = ContentType.Text,
                ContentPayload = ToDoc(new
                {
                    title = lessonJson.Title,
                    body = lessonJson.Explanation!.Markdown,
                    kind = "grammar_explanation",
                }),
                OrderIndex = contentIndex++,
            });
        }

        foreach (var ex in lessonJson.Exercises ?? new List<ExerciseJson>())
        {
            var payload = BuildExercisePayload(ex);
            if (payload is null) continue;

            db.LessonContent.Add(new LessonContent
            {
                LessonId = lesson.Id,
                Type = ContentType.Exercise,
                ExerciseType = payload.ExerciseType,
                ContentPayload = payload.Doc,
                OrderIndex = contentIndex++,
            });
        }

        await db.SaveChangesAsync();
    }

    private record ExercisePayloadBuilt(string ExerciseType, JsonDocument Doc);

    private static ExercisePayloadBuilt? BuildExercisePayload(ExerciseJson ex)
    {
        return ex.Kind switch
        {
            "fill_blank" => BuildFillBlank(ex),
            "quiz" => BuildQuiz(ex),
            _ => null,
        };
    }

    private static ExercisePayloadBuilt? BuildFillBlank(ExerciseJson ex)
    {
        if (ex.Sentences is null || ex.Sentences.Count == 0) return null;

        var rendered = new List<string>();
        foreach (var s in ex.Sentences)
        {
            var line = RenderFillBlankSentence(s);
            if (line is null) continue;
            rendered.Add(line);
        }
        if (rendered.Count == 0) return null;

        return new ExercisePayloadBuilt(
            "fill_blank",
            ToDoc(new
            {
                exercise_type = "fill_blank",
                sentences = rendered,
            }));
    }

    /// <summary>
    /// Converts the scraper's "{0} you a teacher" + blanks[] structure into
    /// the FillBlankRunner string format: "[correct|other1|other2] you a teacher".
    /// Drops the sentence if any blank is unresolved.
    /// </summary>
    private static string? RenderFillBlankSentence(SentenceJson s)
    {
        if (string.IsNullOrWhiteSpace(s.Template)) return null;
        if (s.Blanks is null || s.Blanks.Count == 0) return null;

        var result = s.Template;
        for (int i = 0; i < s.Blanks.Count; i++)
        {
            var blank = s.Blanks[i];
            if (string.IsNullOrEmpty(blank.Correct))
            {
                return null;
            }
            string bracket;
            if (blank.Options is { Count: > 0 })
            {
                var ordered = new List<string> { blank.Correct };
                foreach (var opt in blank.Options)
                {
                    if (!string.Equals(opt, blank.Correct, StringComparison.Ordinal))
                        ordered.Add(opt);
                }
                bracket = "[" + string.Join("|", ordered) + "]";
            }
            else
            {
                bracket = "[" + blank.Correct + "]";
            }
            result = result.Replace("{" + i + "}", bracket);
        }
        return result;
    }

    private static ExercisePayloadBuilt? BuildQuiz(ExerciseJson ex)
    {
        if (ex.Questions is null || ex.Questions.Count == 0) return null;

        var qs = new List<object>();
        foreach (var q in ex.Questions)
        {
            if (q.CorrectAnswerIndex is null) continue;
            if (q.Options is null || q.Options.Count < 2) continue;
            qs.Add(new
            {
                id = q.Id ?? Guid.NewGuid().ToString("N").Substring(0, 8),
                question = q.Question ?? "",
                options = q.Options,
                correct_answer_index = q.CorrectAnswerIndex.Value,
                allow_multiple_selection = q.AllowMultipleSelection ?? false,
                explanation = (string?)null,
            });
        }
        if (qs.Count == 0) return null;

        return new ExercisePayloadBuilt(
            "quiz",
            ToDoc(new
            {
                exercise_type = "quiz",
                passing_score = ex.PassingScore ?? 70,
                questions = qs,
            }));
    }

    private static JsonDocument ToDoc(object value) =>
        JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(value, JsonWriteOpts));

    // ==================================================================================
    // JSON DTOs — must match scripts/scrape-test-english/output/<level>/<slug>.json
    // ==================================================================================

    private sealed class LessonJson
    {
        public int SchemaVersion { get; set; }
        public string Level { get; set; } = "";
        public string Slug { get; set; } = "";
        public int OrderIndex { get; set; }
        public string Title { get; set; } = "";
        public string? SourceUrl { get; set; }
        public ExplanationJson? Explanation { get; set; }
        public List<ExerciseJson>? Exercises { get; set; }
        public List<string>? Warnings { get; set; }
    }

    private sealed class ExplanationJson
    {
        public string Markdown { get; set; } = "";
        public List<ImageJson>? Images { get; set; }
        public List<string>? Warnings { get; set; }
    }

    private sealed class ImageJson
    {
        public string? OriginalUrl { get; set; }
        public string? LocalFilename { get; set; }
        public string? PublicPath { get; set; }
        public string? Alt { get; set; }
    }

    private sealed class ExerciseJson
    {
        public int TabIndex { get; set; }
        public string Title { get; set; } = "";
        public string? Instructions { get; set; }
        public string Kind { get; set; } = "";
        public int? PassingScore { get; set; }
        public List<SentenceJson>? Sentences { get; set; }
        public List<QuestionJson>? Questions { get; set; }
        public List<string>? Warnings { get; set; }
    }

    private sealed class SentenceJson
    {
        public int Index { get; set; }
        public string Template { get; set; } = "";
        public List<BlankJson>? Blanks { get; set; }
    }

    private sealed class BlankJson
    {
        public string Kind { get; set; } = "select";
        public List<string>? Options { get; set; }
        public string? Correct { get; set; }
        public bool Resolved { get; set; }
    }

    private sealed class QuestionJson
    {
        public string? Id { get; set; }
        public string? Question { get; set; }
        public List<string>? Options { get; set; }
        public int? CorrectAnswerIndex { get; set; }
        public bool? AllowMultipleSelection { get; set; }
        public bool Resolved { get; set; }
    }
}
