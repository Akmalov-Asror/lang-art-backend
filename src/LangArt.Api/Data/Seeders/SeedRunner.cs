using System.Text.Json;
using LangArt.Api.Common.Configuration;
using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LangArt.Api.Data.Seeders;

/// <summary>
/// Invoked from Program.cs when the host is launched with one of:
///   dotnet run -- seed     creates the default admin + sample teachers/students/courses
///   dotnet run -- clear    truncates seeded tables
///   dotnet run -- reset    clear then seed
/// </summary>
public static class SeedRunner
{
    private const string StudentPassword = "password123";

    public static async Task<int> RunAsync(string command, IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");

        switch (command.ToLowerInvariant())
        {
            case "seed":
                await SeedAsync(db, seed, logger);
                break;
            case "clear":
                await ClearAsync(db, logger);
                break;
            case "reset":
                await ClearAsync(db, logger);
                await SeedAsync(db, seed, logger);
                break;
            default:
                logger.LogError("Unknown seed command: {Cmd}. Valid: seed | clear | reset", command);
                return 1;
        }

        return 0;
    }

    /// <summary>
    /// Lightweight migration runner — applied on every seed/reset so schema.sql + new
    /// columns we add over time stay in sync without a full EF migrations setup.
    /// </summary>
    public static async Task EnsureSchemaUpgradesAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE profiles ADD COLUMN IF NOT EXISTS avatar_url text;
            ALTER TABLE profiles ADD COLUMN IF NOT EXISTS totp_secret text;
            ALTER TABLE profiles ADD COLUMN IF NOT EXISTS totp_enabled boolean NOT NULL DEFAULT false;
            CREATE TABLE IF NOT EXISTS notifications (
                id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id       uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                kind          text NOT NULL,
                title         text NOT NULL,
                body          text,
                link_url      text,
                read_at       timestamptz,
                created_at    timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS idx_notifications_user_unread
                ON notifications(user_id) WHERE read_at IS NULL;
        """);
    }

    private static async Task SeedAsync(AppDbContext db, SeedOptions seed, ILogger logger)
    {
        await EnsureSchemaUpgradesAsync(db);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(StudentPassword, workFactor: 10);
        var adminHash = BCrypt.Net.BCrypt.HashPassword(seed.AdminPassword, workFactor: 10);

        // ---- Profiles ----
        var admin = await db.Profiles.FirstOrDefaultAsync(p => p.Email == seed.AdminEmail);
        if (admin is null)
        {
            admin = new Profile
            {
                Email = seed.AdminEmail,
                PasswordHash = adminHash,
                FullName = "LangArt Admin",
                Role = Role.Admin,
                IsActive = true,
                EmailVerified = true,
            };
            db.Profiles.Add(admin);
            logger.LogInformation("Seeded admin {Email}", admin.Email);
        }
        else
        {
            // Refresh the admin's password to the configured DEFAULT_ADMIN_PASSWORD so the
            // login works after a clean schema bootstrap (where schema.sql may seed a
            // placeholder hash that BCrypt cannot verify).
            admin.PasswordHash = adminHash;
            admin.Role = Role.Admin;
            admin.IsActive = true;
            logger.LogInformation("Refreshed admin {Email} password", admin.Email);
        }

        var teacherSeeds = new[]
        {
            ("teacher.aiko@langartlms.com", "Aiko Tanaka"),
            ("teacher.mateo@langartlms.com", "Mateo Hernandez"),
        };
        var teachers = new List<Profile>();
        foreach (var (email, name) in teacherSeeds)
        {
            var existing = await db.Profiles.FirstOrDefaultAsync(p => p.Email == email);
            if (existing is null)
            {
                existing = new Profile
                {
                    Email = email,
                    PasswordHash = passwordHash,
                    FullName = name,
                    Role = Role.Teacher,
                    IsActive = true,
                    EmailVerified = true,
                };
                db.Profiles.Add(existing);
                logger.LogInformation("Seeded teacher {Email}", existing.Email);
            }
            teachers.Add(existing);
        }

        var studentSeeds = Enumerable.Range(1, 10)
            .Select(i => ($"student{i:00}@langartlms.com", $"Student {i:00}"))
            .ToList();
        var students = new List<Profile>();
        foreach (var (email, name) in studentSeeds)
        {
            var existing = await db.Profiles.FirstOrDefaultAsync(p => p.Email == email);
            if (existing is null)
            {
                existing = new Profile
                {
                    Email = email,
                    PasswordHash = passwordHash,
                    FullName = name,
                    Role = Role.Student,
                    IsActive = true,
                    EmailVerified = true,
                };
                db.Profiles.Add(existing);
            }
            students.Add(existing);
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} students", students.Count);

        // ---- Courses ----
        var blueprints = BuildBlueprints();
        var courses = new List<Course>();
        foreach (var bp in blueprints)
        {
            var existing = await db.Courses.FirstOrDefaultAsync(c => c.Title == bp.Title);
            if (existing is null)
            {
                existing = new Course
                {
                    Title = bp.Title,
                    Description = bp.Description,
                    ThumbnailUrl = bp.ThumbnailUrl,
                    PriceMonthly = 19.99m,
                };
                db.Courses.Add(existing);
            }
            else if (string.IsNullOrEmpty(existing.ThumbnailUrl) && !string.IsNullOrEmpty(bp.ThumbnailUrl))
            {
                // Backfill thumbnail on existing courses created before the seed had one.
                existing.ThumbnailUrl = bp.ThumbnailUrl;
            }
            courses.Add(existing);
        }
        await db.SaveChangesAsync();

        // ---- Curriculum: modules → lessons → content per blueprint ----
        for (int i = 0; i < blueprints.Count; i++)
        {
            await SeedCurriculumAsync(db, courses[i], blueprints[i], logger);
        }

        // First course (English A1) and the new advanced courses get assigned to groups so
        // students immediately see varied content on the dashboard.
        var englishA1 = courses[0];
        var french = courses.First(c => c.Title.StartsWith("French", StringComparison.Ordinal));
        var german = courses.First(c => c.Title.StartsWith("German", StringComparison.Ordinal));

        // ---- Classroom: one group per teacher with 5 students each ----
        for (int i = 0; i < teachers.Count; i++)
        {
            var teacher = teachers[i];
            var name = $"{teacher.FullName.Split(' ')[0]}'s Class A";
            var group = await db.Groups.FirstOrDefaultAsync(g => g.Name == name && g.TeacherId == teacher.Id);
            if (group is null)
            {
                group = new Group
                {
                    Name = name,
                    TeacherId = teacher.Id,
                    ScheduleInfo = "Mon/Wed 18:00-19:30",
                    ScheduleDays = new[] { "Mon", "Wed" },
                    StartTime = new TimeOnly(18, 0),
                    EndTime = new TimeOnly(19, 30),
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    IsActive = true,
                };
                db.Groups.Add(group);
                await db.SaveChangesAsync();

                var slice = students.Skip(i * 5).Take(5).ToList();
                foreach (var s in slice)
                {
                    db.GroupStudents.Add(new GroupStudent { GroupId = group.Id, StudentId = s.Id });
                }
                // Aiko gets English A1 + French B1; Mateo gets English A1 + German B1.
                db.GroupCourses.Add(new GroupCourse { GroupId = group.Id, CourseId = englishA1.Id });
                db.GroupCourses.Add(new GroupCourse
                {
                    GroupId = group.Id,
                    CourseId = i == 0 ? french.Id : german.Id,
                });
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded group {Name} with {N} students", group.Name, slice.Count);
            }
        }

        // ---- A couple of payments ----
        if (!await db.Payments.AnyAsync())
        {
            var now = DateTime.UtcNow;
            var monthEnd = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59, DateTimeKind.Utc);
            foreach (var s in students.Take(3))
            {
                db.Payments.Add(new Payment
                {
                    UserId = s.Id,
                    CourseId = englishA1.Id,
                    Amount = 19.99m,
                    Currency = "USD",
                    Status = PaymentStatus.Completed,
                    PeriodStart = DateTime.SpecifyKind(now, DateTimeKind.Utc),
                    PeriodEnd = monthEnd,
                });
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded sample payments");
        }
    }

    private static async Task SeedCurriculumAsync(AppDbContext db, Course course, CourseBlueprint bp, ILogger logger)
    {
        var moduleExists = await db.Modules.AnyAsync(m => m.CourseId == course.Id);
        if (moduleExists)
        {
            return; // idempotent — leave already-seeded curriculum alone
        }

        for (int mi = 0; mi < bp.Modules.Count; mi++)
        {
            var moduleBp = bp.Modules[mi];
            var module = new Data.Entities.Module
            {
                CourseId = course.Id,
                Title = moduleBp.Title,
                OrderIndex = mi,
            };
            db.Modules.Add(module);
            await db.SaveChangesAsync();

            for (int li = 0; li < moduleBp.Lessons.Count; li++)
            {
                var lessonBp = moduleBp.Lessons[li];
                var lesson = new Lesson
                {
                    ModuleId = module.Id,
                    Title = lessonBp.Title,
                    OrderIndex = li,
                };
                db.Lessons.Add(lesson);
                await db.SaveChangesAsync();

                for (int ci = 0; ci < lessonBp.Contents.Count; ci++)
                {
                    var contentBp = lessonBp.Contents[ci];
                    db.LessonContent.Add(new LessonContent
                    {
                        LessonId = lesson.Id,
                        Type = contentBp.Type,
                        ContentPayload = contentBp.Payload,
                        OrderIndex = ci,
                        ExerciseType = contentBp.ExerciseType,
                    });
                }
                await db.SaveChangesAsync();
            }
        }
        logger.LogInformation("Seeded curriculum for {Course}", course.Title);
    }

    private static async Task ClearAsync(AppDbContext db, ILogger logger)
    {
        // Order matters because of FKs.
        await db.Payments.ExecuteDeleteAsync();
        await db.StudentLessonAccess.ExecuteDeleteAsync();
        await db.QuizResults.ExecuteDeleteAsync();
        await db.LessonCompletions.ExecuteDeleteAsync();
        await db.Enrollments.ExecuteDeleteAsync();
        await db.Attendance.ExecuteDeleteAsync();
        await db.GroupCourses.ExecuteDeleteAsync();
        await db.GroupStudents.ExecuteDeleteAsync();
        await db.Groups.ExecuteDeleteAsync();
        await db.LessonResources.ExecuteDeleteAsync();
        await db.LessonContent.ExecuteDeleteAsync();
        await db.Lessons.ExecuteDeleteAsync();
        await db.Modules.ExecuteDeleteAsync();
        await db.Courses.ExecuteDeleteAsync();
        await db.Sessions.ExecuteDeleteAsync();
        await db.Profiles.ExecuteDeleteAsync();
        logger.LogInformation("Cleared all seeded tables");
    }

    // =================================================================================
    // Curriculum blueprints
    // =================================================================================

    private record CourseBlueprint(string Title, string Description, string ThumbnailUrl, IReadOnlyList<ModuleBp> Modules);
    private record ModuleBp(string Title, IReadOnlyList<LessonBp> Lessons);
    private record LessonBp(string Title, IReadOnlyList<ContentBp> Contents);
    private record ContentBp(ContentType Type, JsonDocument Payload, string? ExerciseType);
    private record Q(string Id, string Question, string[] Options, int Correct, string? Explanation = null);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static JsonDocument ToDoc(object value) =>
        JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(value, JsonOpts));

    private static ContentBp Text(string title, string body) => new(
        ContentType.Text,
        ToDoc(new { Title = title, Body = body }),
        null);

    private static ContentBp Quiz(int passingScore, params Q[] questions) => new(
        ContentType.Exercise,
        ToDoc(new
        {
            ExerciseType = "quiz",
            PassingScore = passingScore,
            Questions = questions.Select(q => new
            {
                Id = q.Id,
                Question = q.Question,
                Options = q.Options,
                CorrectAnswerIndex = q.Correct,
                Explanation = q.Explanation,
            }),
        }),
        "quiz");

    private static ContentBp Listening(string audioUrl, string audioTitle, int passingScore, params Q[] questions) => new(
        ContentType.Exercise,
        ToDoc(new
        {
            ExerciseType = "listening",
            AudioUrl = audioUrl,
            AudioTitle = audioTitle,
            PassingScore = passingScore,
            Questions = questions.Select(q => new
            {
                Id = q.Id,
                Question = q.Question,
                Options = q.Options,
                CorrectAnswerIndex = q.Correct,
                Explanation = q.Explanation,
            }),
        }),
        "listening");

    private static ContentBp Writing(string prompt, int minWordCount) => new(
        ContentType.Exercise,
        ToDoc(new { ExerciseType = "writing", Prompt = prompt, MinWordCount = minWordCount }),
        "writing");

    private static ContentBp FillBlank(params string[] sentences) => new(
        ContentType.Exercise,
        ToDoc(new { ExerciseType = "fill_blank", Sentences = sentences }),
        "fill_blank");

    private static IReadOnlyList<CourseBlueprint> BuildBlueprints() => new[]
    {
        // -------------------------------------------------------------------- English A1
        new CourseBlueprint(
            "English A1 — Foundations",
            "Beginner-friendly path covering the essentials: greetings, present-tense verbs, everyday vocabulary.",
            "https://images.unsplash.com/photo-1543002588-bfa74002ed7e?w=800&q=80",
            new[]
            {
                new ModuleBp("Getting Started", new[]
                {
                    new LessonBp("Saying Hello", new[]
                    {
                        Text("Welcome", "# Hello!\n\nLet's start with basic greetings. **Hello**, *Hi*, and *Hey* are common in English."),
                        Quiz(70,
                            new Q("q1", "How do you say 'Hello' formally?", new[]{"Hi","Hello","Hey","Yo"}, 1),
                            new Q("q2", "Which is informal?", new[]{"Hello","Good morning","Hey","Greetings"}, 2)),
                    }),
                    new LessonBp("Numbers 1-10", new[]
                    {
                        Text("Counting", "Practice the numbers one through ten: **one, two, three, four, five, six, seven, eight, nine, ten**."),
                    }),
                }),
                new ModuleBp("Daily Conversations", new[]
                {
                    new LessonBp("Ordering Coffee", new[]
                    {
                        Text("At the Café", "Useful phrases: *Can I have...?*, *I'd like..., please.*, *How much is it?*"),
                    }),
                }),
            }),

        // -------------------------------------------------------------------- English B1
        new CourseBlueprint(
            "English B1 — Intermediate",
            "Build fluency: tenses, conditionals, idiomatic expressions, and conversational practice.",
            "https://images.unsplash.com/photo-1457369804613-52c61a468e7d?w=800&q=80",
            new[]
            {
                new ModuleBp("Conditionals & Hypotheticals", new[]
                {
                    new LessonBp("Zero & First Conditional", new[]
                    {
                        Text("If-clauses",
                            "# Conditionals\n\n**Zero conditional** — general truths: *If you heat water to 100°C, it boils.*\n\n" +
                            "**First conditional** — real future possibilities: *If it rains tomorrow, we'll stay home.*\n\n" +
                            "Form: **If + present simple, ... will + base verb**."),
                        Quiz(75,
                            new Q("q1", "Pick the zero-conditional sentence:",
                                new[]{"If I have time, I will call you.","If you press this button, the light turns on.","If I were you, I'd accept the job.","If she had studied, she would have passed."}, 1,
                                "Zero conditional uses present tense in both clauses for general truths."),
                            new Q("q2", "If it ___ tomorrow, we'll cancel the picnic.",
                                new[]{"rains","will rain","rained","would rain"}, 0,
                                "First conditional uses present simple in the if-clause."),
                            new Q("q3", "Which form follows 'If + present simple, ...'?",
                                new[]{"would + base","will + base","had + past participle","were + base"}, 1),
                            new Q("q4", "Pick the first-conditional sentence:",
                                new[]{"If I win, I'll celebrate.","If I won, I'd celebrate.","If I had won, I'd have celebrated.","If I'm winning, I celebrate."}, 0)),
                    }),
                    new LessonBp("Second Conditional Writing", new[]
                    {
                        Text("Imagining the Unreal",
                            "Second conditional describes hypothetical situations: *If I had a million dollars, I would travel the world.*\n\n" +
                            "Form: **If + past simple, ... would + base verb.**"),
                        Writing(
                            "If you could live anywhere in the world, where would you live and why? Use the second conditional at least twice. (min 80 words)",
                            80),
                    }),
                }),
                new ModuleBp("Idiomatic English", new[]
                {
                    new LessonBp("Common Idioms", new[]
                    {
                        Text("Idioms",
                            "Idioms have meanings that go beyond their literal words. Examples:\n\n" +
                            "- **Break the ice** — start a conversation\n" +
                            "- **Hit the nail on the head** — describe something exactly\n" +
                            "- **Under the weather** — feeling sick\n" +
                            "- **Piece of cake** — very easy\n" +
                            "- **Cost an arm and a leg** — very expensive"),
                        Quiz(70,
                            new Q("q1", "She felt 'under the weather' — what does that mean?",
                                new[]{"She was outside in the rain","She was feeling unwell","She was traveling","She was upset"}, 1),
                            new Q("q2", "'A piece of cake' means:",
                                new[]{"A small dessert","Something very easy","Something delicious","Something expensive"}, 1),
                            new Q("q3", "'Break the ice' is used when you:",
                                new[]{"Cool down a drink","Start a conversation","End a friendship","Cancel a meeting"}, 1),
                            new Q("q4", "Something that 'costs an arm and a leg' is:",
                                new[]{"Free","Cheap","Very expensive","Painful"}, 2),
                            new Q("q5", "'Hit the nail on the head' means:",
                                new[]{"Be exactly right","Make a mistake","Work very hard","Be lucky"}, 0)),
                    }),
                    new LessonBp("Phrasal Verbs", new[]
                    {
                        Text("Phrasal Verbs",
                            "Phrasal verbs are verb + particle combinations: **give up**, **look after**, **run into**, **bring up**.\n\n" +
                            "They often have meanings that differ from the parts."),
                        Writing(
                            "Write a short story (min 100 words) using at least four phrasal verbs. Underline each phrasal verb you use.",
                            100),
                    }),
                }),
            }),

        // -------------------------------------------------------------------- Spanish A1
        new CourseBlueprint(
            "Spanish A1 — Hola y Mucho Más",
            "Start speaking Spanish from day one with cultural notes and bite-sized lessons.",
            "https://images.unsplash.com/photo-1583422409516-2895a77efded?w=800&q=80",
            new[]
            {
                new ModuleBp("Saludos y Presentaciones", new[]
                {
                    new LessonBp("Hola y Adiós", new[]
                    {
                        Text("Saludos básicos",
                            "# Hola\n\nLos saludos más comunes:\n\n- **Hola** — Hello\n- **Buenos días** — Good morning\n- **Buenas tardes** — Good afternoon\n- **Buenas noches** — Good evening / night\n- **Adiós** — Goodbye\n- **Hasta luego** — See you later"),
                        Quiz(70,
                            new Q("q1", "How do you say 'Good morning' in Spanish?",
                                new[]{"Buenas noches","Buenas tardes","Buenos días","Hola"}, 2),
                            new Q("q2", "'Hasta luego' means:",
                                new[]{"Hello","See you later","Good night","Thank you"}, 1),
                            new Q("q3", "Pick the evening greeting:",
                                new[]{"Buenos días","Buenas tardes","Buenas noches","Adiós"}, 2),
                            new Q("q4", "'Adiós' translates to:",
                                new[]{"Hello","Yes","Goodbye","Please"}, 2)),
                    }),
                    new LessonBp("Presentándote", new[]
                    {
                        Text("Yo me llamo...",
                            "Para presentarte:\n\n- **Me llamo + name** — My name is...\n- **Yo soy de + place** — I am from...\n- **Tengo + age + años** — I am ... years old."),
                        FillBlank(
                            "Me [llamo] Ana.",
                            "Yo [soy] de España.",
                            "[Tengo] veinte años.",
                            "Mucho [gusto] en conocerte."),
                    }),
                }),
                new ModuleBp("Números y Tiempo", new[]
                {
                    new LessonBp("Los Números 1-20", new[]
                    {
                        Text("Contando",
                            "1 uno, 2 dos, 3 tres, 4 cuatro, 5 cinco, 6 seis, 7 siete, 8 ocho, 9 nueve, 10 diez, " +
                            "11 once, 12 doce, 13 trece, 14 catorce, 15 quince, 16 dieciséis, 17 diecisiete, 18 dieciocho, 19 diecinueve, 20 veinte."),
                        FillBlank(
                            "Tres más cuatro son [siete].",
                            "Diez menos dos son [ocho].",
                            "Cinco por dos son [diez]."),
                    }),
                    new LessonBp("¿Qué Hora Es?", new[]
                    {
                        Text("La Hora",
                            "**Es la una.** — It's one o'clock.\n**Son las dos.** — It's two o'clock.\n**Son las tres y media.** — It's half past three."),
                        Quiz(60,
                            new Q("q1", "How would you say 'It's one o'clock'?",
                                new[]{"Son la una","Es la una","Son las una","Es las una"}, 1),
                            new Q("q2", "'Son las tres y media' means:",
                                new[]{"3:00","3:15","3:30","3:45"}, 2),
                            new Q("q3", "Pick the correct form for 'It's five o'clock':",
                                new[]{"Es las cinco","Son las cinco","Es la cinco","Son la cinco"}, 1)),
                    }),
                }),
            }),

        // -------------------------------------------------------------------- French B1
        new CourseBlueprint(
            "French B1 — Conversational Mastery",
            "Move beyond textbook French: real-world café conversations, travel scenarios, and listening practice.",
            "https://images.unsplash.com/photo-1502602898657-3e91760cbb34?w=800&q=80",
            new[]
            {
                new ModuleBp("Au Café", new[]
                {
                    new LessonBp("Commander une boisson", new[]
                    {
                        Text("À la terrasse",
                            "# Au Café\n\nLes formules polies pour commander :\n\n" +
                            "- *Bonjour, je voudrais un café, s'il vous plaît.*\n" +
                            "- *Pourriez-vous m'apporter l'addition ?*\n" +
                            "- *Est-ce que je peux avoir un verre d'eau ?*\n\n" +
                            "**Astuce :** En France, on dit toujours *bonjour* avant de commander."),
                        Quiz(75,
                            new Q("q1", "Comment commander poliment un café ?",
                                new[]{"Je veux un café","Donnez-moi un café","Je voudrais un café, s'il vous plaît","Café !"}, 2,
                                "La forme conditionnelle 'je voudrais' est plus polie que 'je veux'."),
                            new Q("q2", "Pour demander l'addition :",
                                new[]{"L'addition, s'il vous plaît","Le menu, s'il vous plaît","La carte, s'il vous plaît","L'eau, s'il vous plaît"}, 0),
                            new Q("q3", "Quel mot signifie 'tip' en français ?",
                                new[]{"L'addition","Le pourboire","La monnaie","La caisse"}, 1)),
                    }),
                    new LessonBp("Conversation au comptoir", new[]
                    {
                        Text("Écoutez bien",
                            "Dans cet exercice, écoutez la conversation à un comptoir français et répondez aux questions de compréhension."),
                        Listening(
                            "https://upload.wikimedia.org/wikipedia/commons/9/96/Fr-bonjour.ogg",
                            "Bonjour — prononciation française",
                            60,
                            new Q("q1", "Quel salut entendez-vous au début ?",
                                new[]{"Bonjour","Bonsoir","Salut","Au revoir"}, 0),
                            new Q("q2", "Le 'r' dans 'bonjour' se prononce :",
                                new[]{"comme en anglais","de la gorge (uvulaire)","silencieux","comme un 'l'"}, 1),
                            new Q("q3", "On utilise 'bonjour' :",
                                new[]{"seulement le matin","toute la journée","seulement le soir","la nuit"}, 1)),
                    }),
                }),
                new ModuleBp("Voyages", new[]
                {
                    new LessonBp("À la gare", new[]
                    {
                        Text("Prendre le train",
                            "Phrases utiles à la gare :\n\n- *Un aller-retour pour Paris, s'il vous plaît.*\n" +
                            "- *Le prochain train part à quelle heure ?*\n- *Sur quel quai ?*"),
                        Quiz(70,
                            new Q("q1", "'Un aller-retour' veut dire :",
                                new[]{"One-way ticket","Round-trip ticket","First-class ticket","Reservation"}, 1),
                            new Q("q2", "Vous demandez le quai du train :",
                                new[]{"Quel est le prix ?","Sur quel quai ?","Quand arrive-t-il ?","Combien de temps ?"}, 1),
                            new Q("q3", "'Le prochain train' signifie :",
                                new[]{"The previous train","The first train","The next train","The last train"}, 2)),
                    }),
                    new LessonBp("Demander son chemin", new[]
                    {
                        Text("Excusez-moi…",
                            "Pour demander son chemin :\n\n- *Excusez-moi, où se trouve la gare ?*\n- *C'est loin d'ici ?*\n- *Tournez à droite, puis tout droit.*"),
                        Listening(
                            "https://upload.wikimedia.org/wikipedia/commons/9/96/Fr-bonjour.ogg",
                            "Demander son chemin (extrait)",
                            60,
                            new Q("q1", "Comment commencer poliment ?",
                                new[]{"Hé !","Excusez-moi","Toi, là !","Bonsoir"}, 1),
                            new Q("q2", "'Tournez à droite' signifie :",
                                new[]{"Turn left","Turn right","Go straight","Stop"}, 1)),
                    }),
                }),
            }),

        // -------------------------------------------------------------------- German B1
        new CourseBlueprint(
            "German B1 — Building Fluency",
            "Master separable verbs, the Konjunktiv II, and write confidently about daily life and hypotheticals.",
            "https://images.unsplash.com/photo-1467269204594-9661b134dd2b?w=800&q=80",
            new[]
            {
                new ModuleBp("Trennbare Verben", new[]
                {
                    new LessonBp("Anfangen, aufstehen, ausgehen", new[]
                    {
                        Text("Was sind trennbare Verben?",
                            "# Trennbare Verben\n\nManche deutsche Verben haben ein Präfix, das sich vom Stamm trennt:\n\n" +
                            "- **aufstehen** — Ich **stehe** um 7 Uhr **auf**.\n" +
                            "- **anfangen** — Der Film **fängt** um 20 Uhr **an**.\n" +
                            "- **ausgehen** — Wir **gehen** heute Abend **aus**.\n" +
                            "- **einkaufen** — Sie **kauft** im Supermarkt **ein**."),
                        FillBlank(
                            "Ich [stehe] um 7 Uhr [auf].",
                            "Der Film [fängt] um 20 Uhr [an].",
                            "Wir [gehen] heute Abend [aus].",
                            "Sie [kauft] gerne [ein]."),
                    }),
                    new LessonBp("Schreibübung: Mein Tag", new[]
                    {
                        Text("Tagesablauf",
                            "Schreibe über deinen typischen Tag. Benutze mindestens drei trennbare Verben (z. B. *aufstehen*, *anrufen*, *einkaufen*, *ausgehen*)."),
                        Writing(
                            "Beschreibe deinen typischen Tag von morgens bis abends. Verwende mindestens drei trennbare Verben. (mindestens 60 Wörter)",
                            60),
                    }),
                }),
                new ModuleBp("Konjunktiv II", new[]
                {
                    new LessonBp("Wenn ich…", new[]
                    {
                        Text("Hypothesen ausdrücken",
                            "Der Konjunktiv II drückt Hypothesen oder Wünsche aus:\n\n" +
                            "- *Wenn ich Zeit **hätte**, **würde** ich mehr lesen.*\n" +
                            "- *Ich **wäre** gerne in Berlin.*\n\n" +
                            "Hilfsverben im Konjunktiv II: **hätte**, **wäre**, **würde + Infinitiv**."),
                        FillBlank(
                            "Wenn ich Zeit [hätte], [würde] ich mehr lesen.",
                            "Ich [wäre] gerne in Berlin.",
                            "Wenn er hier [wäre], [würde] er helfen."),
                    }),
                    new LessonBp("Höfliche Bitten", new[]
                    {
                        Text("Höflichkeit",
                            "Mit dem Konjunktiv II kannst du höflich bitten:\n\n" +
                            "- *Könnten Sie mir helfen?*\n- *Hätten Sie einen Moment?*\n- *Würden Sie das wiederholen, bitte?*"),
                        Writing(
                            "Stell dir vor, du sprichst mit einem neuen Kollegen. Schreibe einen kurzen Dialog (mindestens 80 Wörter) mit mindestens drei höflichen Bitten im Konjunktiv II.",
                            80),
                    }),
                }),
            }),

        // -------------------------------------------------------------------- Italian B1
        new CourseBlueprint(
            "Italian B1 — Caffè e Cultura",
            "Sip, listen, and converse: B1-level Italian rooted in everyday café and kitchen scenes.",
            "https://images.unsplash.com/photo-1525610553991-2bede1a236e2?w=800&q=80",
            new[]
            {
                new ModuleBp("Al Bar", new[]
                {
                    new LessonBp("Ordinare un caffè", new[]
                    {
                        Text("Al banco",
                            "# Al Bar\n\nIn Italia il *bar* è dove si beve il caffè in piedi.\n\n" +
                            "- *Un caffè, per favore.* — un espresso\n" +
                            "- *Un cappuccino e un cornetto.* — colazione tipica\n" +
                            "- *Un caffè macchiato.* — espresso con un goccio di latte\n\n" +
                            "**Curiosità:** gli italiani non bevono cappuccino dopo pranzo!"),
                        Quiz(70,
                            new Q("q1", "Cosa significa 'un caffè' in Italia?",
                                new[]{"Un americano","Un espresso","Un cappuccino","Un latte"}, 1,
                                "In Italia 'un caffè' è sempre un espresso."),
                            new Q("q2", "Quando si beve il cappuccino?",
                                new[]{"Al mattino","A pranzo","Dopo cena","A tutte le ore"}, 0),
                            new Q("q3", "'Un caffè macchiato' è :",
                                new[]{"Un caffè con molto latte","Un espresso con un goccio di latte","Un caffè freddo","Un caffè senza zucchero"}, 1)),
                    }),
                    new LessonBp("Conversazione informale", new[]
                    {
                        Text("Ascolta",
                            "Ascolta questa breve conversazione al bar e rispondi alle domande di comprensione."),
                        Listening(
                            "https://upload.wikimedia.org/wikipedia/commons/6/64/It-buongiorno.ogg",
                            "Buongiorno — pronuncia italiana",
                            60,
                            new Q("q1", "Quale saluto senti?",
                                new[]{"Buongiorno","Buonasera","Ciao","Salve"}, 0),
                            new Q("q2", "'Buongiorno' si usa :",
                                new[]{"solo la mattina presto","dalla mattina fino al primo pomeriggio","solo la sera","di notte"}, 1),
                            new Q("q3", "Una versione più informale di 'Buongiorno' è :",
                                new[]{"Salve","Buonasera","Ciao","Buonanotte"}, 2)),
                    }),
                }),
                new ModuleBp("In Cucina", new[]
                {
                    new LessonBp("Ingredienti italiani", new[]
                    {
                        Text("Sapori autentici",
                            "Gli ingredienti chiave della cucina italiana:\n\n" +
                            "- **olio d'oliva** — olive oil\n- **pomodoro** — tomato\n- **basilico** — basil\n" +
                            "- **parmigiano** — Parmesan cheese\n- **pasta fresca** — fresh pasta"),
                        Quiz(70,
                            new Q("q1", "'Olio d'oliva' è :",
                                new[]{"Vinegar","Olive oil","Butter","Sunflower oil"}, 1),
                            new Q("q2", "Quale erba è essenziale nel pesto?",
                                new[]{"Prezzemolo","Origano","Basilico","Rosmarino"}, 2),
                            new Q("q3", "'Pasta fresca' significa :",
                                new[]{"Dry pasta","Fresh pasta","Cold pasta","Pasta sauce"}, 1)),
                    }),
                    new LessonBp("Ricette tradizionali", new[]
                    {
                        Text("Pasta al pomodoro",
                            "Una ricetta classica in pochi passi: fai bollire l'acqua, aggiungi il sale, cuoci la pasta *al dente*, poi unisci una salsa di pomodoro, basilico fresco e parmigiano grattugiato."),
                        Listening(
                            "https://upload.wikimedia.org/wikipedia/commons/6/64/It-buongiorno.ogg",
                            "Pronuncia: 'al dente'",
                            60,
                            new Q("q1", "'Al dente' descrive :",
                                new[]{"Pasta overcooked","Pasta firm to the bite","Pasta cold","Pasta with sauce"}, 1),
                            new Q("q2", "Il basilico è :",
                                new[]{"Una spezia secca","Un'erba fresca aromatica","Una salsa","Un formaggio"}, 1)),
                    }),
                }),
            }),
    };
}
