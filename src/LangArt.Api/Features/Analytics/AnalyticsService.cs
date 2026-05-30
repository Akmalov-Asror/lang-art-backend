using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Features.Analytics.Dto;
using LangArt.Api.Features.Gamification;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Analytics;

public class AnalyticsService
{
    private readonly AppDbContext _db;

    public AnalyticsService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Roster of students the current teacher can see. For admin: every
    /// active student. For teacher: only students in groups the teacher owns.
    /// </summary>
    public async Task<IReadOnlyList<StudentSummaryDto>> ListStudentsAsync(Guid currentUserId, string currentRole, CancellationToken ct)
    {
        IQueryable<Profile> studentsQ;
        if (currentRole == "admin")
        {
            studentsQ = _db.Profiles.Where(p => p.Role == Data.Enums.Role.Student && p.IsActive);
        }
        else
        {
            // Teacher sees students in their groups.
            studentsQ = _db.GroupStudents
                .Where(gs => gs.Group.TeacherId == currentUserId)
                .Select(gs => gs.Student)
                .Where(p => p.IsActive);
        }

        var students = await studentsQ.AsNoTracking()
            .OrderBy(p => p.FullName)
            .ToListAsync(ct);

        var ids = students.Select(s => s.Id).ToList();
        var xpMap = await _db.UserXp.AsNoTracking()
            .Where(x => ids.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.TotalXp, ct);
        var streakMap = await _db.UserStreaks.AsNoTracking()
            .Where(x => ids.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.CurrentStreak, ct);

        // Per-skill computations done in bulk.
        var skillMap = await ComputeAllSkillLevelsAsync(ids, ct);

        var lessonsDone = await _db.LessonCompletions.AsNoTracking()
            .Where(c => ids.Contains(c.UserId))
            .GroupBy(c => c.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.UserId, g => g.Count, ct);

        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var weekly = await ComputeWeeklySubmissionCountsAsync(ids, weekAgo, ct);

        var groupNames = await _db.GroupStudents.AsNoTracking()
            .Where(gs => ids.Contains(gs.StudentId))
            .Select(gs => new { gs.StudentId, gs.Group.Name })
            .ToListAsync(ct);
        var groupMap = groupNames
            .GroupBy(g => g.StudentId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.Name).Distinct()));

        // Decorate each student with their adaptive status (struggling / advanced / on_track).
        // This is a per-student computation; cheap enough to inline for a ~5-50 student roster.
        var result = new List<StudentSummaryDto>(students.Count);
        foreach (var s in students)
        {
            var adaptive = await ComputeAdaptiveStatusAsync(s.Id, ct);
            result.Add(new StudentSummaryDto
            {
                Id = s.Id,
                FullName = s.FullName,
                Email = s.Email,
                AvatarUrl = s.AvatarUrl,
                TotalXp = xpMap.GetValueOrDefault(s.Id, 0),
                Level = LevelCalculator.GetLevelFromXp(xpMap.GetValueOrDefault(s.Id, 0)),
                CurrentStreak = streakMap.GetValueOrDefault(s.Id, 0),
                Skills = skillMap.GetValueOrDefault(s.Id) ?? new SkillLevelsDto(),
                LessonsCompleted = lessonsDone.GetValueOrDefault(s.Id, 0),
                SubmissionsLast7Days = weekly.GetValueOrDefault(s.Id, 0),
                GroupName = groupMap.GetValueOrDefault(s.Id),
                AdaptiveStatus = adaptive.Status,
            });
        }
        return result;
    }

    public async Task<StudentDetailDto> GetStudentAsync(Guid studentId, Guid currentUserId, string currentRole, CancellationToken ct)
    {
        await EnsureCanViewStudentAsync(studentId, currentUserId, currentRole, ct);

        var student = await _db.Profiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == studentId)
            ?? throw new NotFoundException("Student not found");

        var xp = await _db.UserXp.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == studentId, ct);
        var streak = await _db.UserStreaks.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == studentId, ct);
        var skills = (await ComputeAllSkillLevelsAsync(new[] { studentId }, ct))
            .GetValueOrDefault(studentId) ?? new SkillLevelsDto();
        var lessonsDone = await _db.LessonCompletions.AsNoTracking()
            .CountAsync(c => c.UserId == studentId, ct);
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var weekly = (await ComputeWeeklySubmissionCountsAsync(new[] { studentId }, weekAgo, ct))
            .GetValueOrDefault(studentId, 0);
        var groupName = await _db.GroupStudents.AsNoTracking()
            .Where(gs => gs.StudentId == studentId)
            .Select(gs => gs.Group.Name)
            .FirstOrDefaultAsync(ct);

        var adaptive = await ComputeAdaptiveStatusAsync(studentId, ct);

        var summary = new StudentSummaryDto
        {
            Id = student.Id,
            FullName = student.FullName,
            Email = student.Email,
            AvatarUrl = student.AvatarUrl,
            TotalXp = xp?.TotalXp ?? 0,
            Level = LevelCalculator.GetLevelFromXp(xp?.TotalXp ?? 0),
            CurrentStreak = streak?.CurrentStreak ?? 0,
            Skills = skills,
            LessonsCompleted = lessonsDone,
            SubmissionsLast7Days = weekly,
            GroupName = groupName,
            AdaptiveStatus = adaptive.Status,
        };

        var recent = await GetRecentSubmissionsAsync(studentId, 20, ct);
        var notes = await ListNotesAsync(studentId, ct);

        return new StudentDetailDto
        {
            Student = summary,
            RecentSubmissions = recent.ToList(),
            Notes = notes.ToList(),
            Adaptive = adaptive,
        };
    }

    public async Task<IReadOnlyList<RecentSubmissionDto>> GetRecentSubmissionsAsync(Guid studentId, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);

        var quiz = await _db.QuizResults.AsNoTracking()
            .Where(q => q.UserId == studentId)
            .OrderByDescending(q => q.CreatedAt)
            .Take(limit)
            .Select(q => new
            {
                q.Id,
                q.ContentId,
                LessonId = (Guid?)q.LessonId,
                Score = (int?)q.Score,
                OutOf = (int?)q.TotalQuestions,
                q.Passed,
                q.CreatedAt,
                ExerciseType = q.Content!.ExerciseType,
                LessonTitle = q.Lesson!.Title,
            })
            .ToListAsync(ct);

        var speaking = await _db.SpeakingSubmissions.AsNoTracking()
            .Where(s => s.UserId == studentId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .Select(s => new
            {
                s.Id,
                s.ContentId,
                LessonId = (Guid?)s.Content.LessonId,
                Score = s.FinalGrade,
                OutOf = (int?)100,
                Passed = (bool?)(s.FinalGrade >= 70),
                s.CreatedAt,
                ExerciseType = "speaking",
                LessonTitle = s.Content.Lesson.Title,
                s.Status,
            })
            .ToListAsync(ct);

        var writing = await _db.WritingSubmissions.AsNoTracking()
            .Where(s => s.UserId == studentId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .Select(s => new
            {
                s.Id,
                s.ContentId,
                LessonId = (Guid?)s.Content.LessonId,
                Score = s.FinalGrade,
                OutOf = (int?)100,
                Passed = (bool?)(s.FinalGrade >= 70),
                s.CreatedAt,
                ExerciseType = "writing",
                LessonTitle = s.Content.Lesson.Title,
                s.Status,
            })
            .ToListAsync(ct);

        var all = new List<RecentSubmissionDto>();
        all.AddRange(quiz.Select(q => new RecentSubmissionDto
        {
            Id = q.Id,
            Kind = string.IsNullOrEmpty(q.ExerciseType) ? "quiz" : q.ExerciseType,
            LessonTitle = q.LessonTitle,
            Score = q.Score,
            OutOf = q.OutOf,
            Passed = q.Passed,
            Status = q.Passed ? "passed" : "not_passed",
            CreatedAt = q.CreatedAt,
            LessonId = q.LessonId,
            ContentId = q.ContentId,
        }));
        all.AddRange(speaking.Select(s => new RecentSubmissionDto
        {
            Id = s.Id,
            Kind = "speaking",
            LessonTitle = s.LessonTitle,
            Score = s.Score,
            OutOf = s.OutOf,
            Passed = s.Passed,
            Status = s.Status,
            CreatedAt = s.CreatedAt,
            LessonId = s.LessonId,
            ContentId = s.ContentId,
        }));
        all.AddRange(writing.Select(w => new RecentSubmissionDto
        {
            Id = w.Id,
            Kind = "writing",
            LessonTitle = w.LessonTitle,
            Score = w.Score,
            OutOf = w.OutOf,
            Passed = w.Passed,
            Status = w.Status,
            CreatedAt = w.CreatedAt,
            LessonId = w.LessonId,
            ContentId = w.ContentId,
        }));

        return all
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToList();
    }

    // ===== Notes =====

    public async Task<IReadOnlyList<TeacherNoteDto>> ListNotesAsync(Guid studentId, CancellationToken ct)
    {
        var rows = await _db.TeacherNotes.AsNoTracking()
            .Where(n => n.StudentId == studentId)
            .OrderByDescending(n => n.CreatedAt)
            .Include(n => n.Author)
            .ToListAsync(ct);
        return rows.Select(n => new TeacherNoteDto
        {
            Id = n.Id,
            AuthorId = n.AuthorId,
            AuthorName = n.Author.FullName,
            Kind = n.Kind,
            Body = n.Body,
            CreatedAt = n.CreatedAt,
            UpdatedAt = n.UpdatedAt,
        }).ToList();
    }

    public async Task<TeacherNoteDto> AddNoteAsync(Guid studentId, Guid authorId, UpsertNoteRequest req, CancellationToken ct)
    {
        var studentExists = await _db.Profiles.AnyAsync(p => p.Id == studentId, ct);
        if (!studentExists) throw new NotFoundException("Student not found");
        var note = new TeacherNote
        {
            StudentId = studentId,
            AuthorId = authorId,
            Kind = req.Kind,
            Body = req.Body,
        };
        _db.TeacherNotes.Add(note);
        await _db.SaveChangesAsync(ct);
        var author = await _db.Profiles.AsNoTracking()
            .Where(p => p.Id == authorId)
            .Select(p => p.FullName)
            .FirstAsync(ct);
        return new TeacherNoteDto
        {
            Id = note.Id,
            AuthorId = authorId,
            AuthorName = author,
            Kind = note.Kind,
            Body = note.Body,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt,
        };
    }

    public async Task DeleteNoteAsync(Guid noteId, Guid currentUserId, string currentRole, CancellationToken ct)
    {
        var note = await _db.TeacherNotes.FirstOrDefaultAsync(n => n.Id == noteId, ct)
            ?? throw new NotFoundException("Note not found");
        if (currentRole != "admin" && note.AuthorId != currentUserId)
        {
            throw new ForbiddenException("You can only delete your own notes");
        }
        _db.TeacherNotes.Remove(note);
        await _db.SaveChangesAsync(ct);
    }

    // ===== Skill-level computation =====

    private async Task<Dictionary<Guid, SkillLevelsDto>> ComputeAllSkillLevelsAsync(IReadOnlyList<Guid> studentIds, CancellationToken ct)
    {
        if (studentIds.Count == 0) return new();

        // Speaking — average of speaking_submissions.final_grade (0-100).
        var speaking = await _db.SpeakingSubmissions.AsNoTracking()
            .Where(s => studentIds.Contains(s.UserId) && s.FinalGrade != null)
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, Avg = g.Average(x => (double)x.FinalGrade!.Value) })
            .ToListAsync(ct);

        // Writing — average of writing_submissions.final_grade.
        var writing = await _db.WritingSubmissions.AsNoTracking()
            .Where(s => studentIds.Contains(s.UserId) && s.FinalGrade != null)
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, Avg = g.Average(x => (double)x.FinalGrade!.Value) })
            .ToListAsync(ct);

        // Quiz/Reading/Listening/Fill-blank — average percentage from QuizResult,
        // partitioned by content.exercise_type.
        var quizByType = await _db.QuizResults.AsNoTracking()
            .Where(q => studentIds.Contains(q.UserId) && q.TotalQuestions > 0)
            .Select(q => new
            {
                q.UserId,
                Type = q.Content!.ExerciseType ?? "quiz",
                Pct = (double)q.Score / q.TotalQuestions * 100.0,
            })
            .ToListAsync(ct);

        var reading = quizByType
            .Where(x => x.Type == "reading")
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Pct));
        var listening = quizByType
            .Where(x => x.Type == "listening")
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Pct));
        var grammar = quizByType
            .Where(x => x.Type == "quiz" || x.Type == "fill_blank")
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Pct));

        // Vocabulary — share of wordlist entries marked as 'learned'.
        var vocabRaw = await _db.UserWordlistEntries.AsNoTracking()
            .Where(e => studentIds.Contains(e.UserId))
            .GroupBy(e => e.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Total = g.Count(),
                Learned = g.Count(e => e.Status == "learned"),
            })
            .ToListAsync(ct);
        var vocabulary = vocabRaw.ToDictionary(
            v => v.UserId,
            v => v.Total > 0 ? (double)v.Learned / v.Total * 100.0 : (double?)null);

        var speakingMap = speaking.ToDictionary(x => x.UserId, x => x.Avg);
        var writingMap = writing.ToDictionary(x => x.UserId, x => x.Avg);

        var result = new Dictionary<Guid, SkillLevelsDto>();
        foreach (var id in studentIds)
        {
            int? s = speakingMap.TryGetValue(id, out var sv) ? (int?)Math.Round(sv) : null;
            int? w = writingMap.TryGetValue(id, out var wv) ? (int?)Math.Round(wv) : null;
            int? r = reading.TryGetValue(id, out var rv) ? (int?)Math.Round(rv) : null;
            int? l = listening.TryGetValue(id, out var lv) ? (int?)Math.Round(lv) : null;
            int? g = grammar.TryGetValue(id, out var gv) ? (int?)Math.Round(gv) : null;
            int? v = vocabulary.TryGetValue(id, out var vv) && vv.HasValue
                ? (int?)Math.Round(vv.Value)
                : null;

            int? overall = null;
            var present = new[] { s, w, r, l, g, v }.Where(x => x.HasValue).Select(x => x!.Value).ToList();
            if (present.Count > 0) overall = (int)Math.Round(present.Average());

            result[id] = new SkillLevelsDto
            {
                Speaking = s,
                Writing = w,
                Reading = r,
                Listening = l,
                Grammar = g,
                Vocabulary = v,
                Overall = overall,
            };
        }
        return result;
    }

    private async Task<Dictionary<Guid, int>> ComputeWeeklySubmissionCountsAsync(IReadOnlyList<Guid> studentIds, DateTime since, CancellationToken ct)
    {
        var quiz = await _db.QuizResults.AsNoTracking()
            .Where(q => studentIds.Contains(q.UserId) && q.CreatedAt >= since)
            .GroupBy(q => q.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var speaking = await _db.SpeakingSubmissions.AsNoTracking()
            .Where(s => studentIds.Contains(s.UserId) && s.CreatedAt >= since)
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var writing = await _db.WritingSubmissions.AsNoTracking()
            .Where(s => studentIds.Contains(s.UserId) && s.CreatedAt >= since)
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, int>();
        foreach (var id in studentIds) result[id] = 0;
        foreach (var x in quiz) result[x.UserId] += x.Count;
        foreach (var x in speaking) result[x.UserId] += x.Count;
        foreach (var x in writing) result[x.UserId] += x.Count;
        return result;
    }

    private async Task EnsureCanViewStudentAsync(Guid studentId, Guid currentUserId, string currentRole, CancellationToken ct)
    {
        if (currentRole == "admin") return;
        // Teacher must share a group with the student.
        var shared = await _db.GroupStudents.AnyAsync(gs =>
            gs.StudentId == studentId && gs.Group.TeacherId == currentUserId, ct);
        if (!shared)
        {
            throw new ForbiddenException("This student is not in any of your groups");
        }
    }

    // ===== Phase 9 — Admin Teacher Quality =====

    /// <summary>
    /// Roster of teachers with aggregate quality metrics. Admin-only.
    /// </summary>
    public async Task<IReadOnlyList<TeacherSummaryDto>> ListTeachersAsync(CancellationToken ct)
    {
        var teachers = await _db.Profiles.AsNoTracking()
            .Where(p => p.Role == Data.Enums.Role.Teacher)
            .OrderBy(p => p.FullName)
            .ToListAsync(ct);

        var result = new List<TeacherSummaryDto>(teachers.Count);
        foreach (var t in teachers)
        {
            result.Add(await BuildTeacherSummaryAsync(t, ct));
        }
        return result;
    }

    public async Task<TeacherDetailDto> GetTeacherAsync(Guid teacherId, CancellationToken ct)
    {
        var teacher = await _db.Profiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == teacherId && p.Role == Data.Enums.Role.Teacher, ct)
            ?? throw new NotFoundException("Teacher not found");

        var summary = await BuildTeacherSummaryAsync(teacher, ct);

        var studentIds = await _db.GroupStudents.AsNoTracking()
            .Where(gs => gs.Group.TeacherId == teacherId)
            .Select(gs => gs.StudentId)
            .Distinct()
            .ToListAsync(ct);

        // Reuse the student-summary build path used by /api/teacher/students,
        // but inline-filtered to this teacher's roster.
        var students = await _db.Profiles.AsNoTracking()
            .Where(p => studentIds.Contains(p.Id) && p.IsActive)
            .OrderBy(p => p.FullName)
            .ToListAsync(ct);

        var ids = students.Select(s => s.Id).ToList();
        var xpMap = await _db.UserXp.AsNoTracking()
            .Where(x => ids.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.TotalXp, ct);
        var streakMap = await _db.UserStreaks.AsNoTracking()
            .Where(x => ids.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.CurrentStreak, ct);
        var skillMap = await ComputeAllSkillLevelsAsync(ids, ct);
        var lessonsDone = await _db.LessonCompletions.AsNoTracking()
            .Where(c => ids.Contains(c.UserId))
            .GroupBy(c => c.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.UserId, g => g.Count, ct);
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var weekly = await ComputeWeeklySubmissionCountsAsync(ids, weekAgo, ct);
        var groupNames = await _db.GroupStudents.AsNoTracking()
            .Where(gs => ids.Contains(gs.StudentId) && gs.Group.TeacherId == teacherId)
            .Select(gs => new { gs.StudentId, gs.Group.Name })
            .ToListAsync(ct);
        var groupMap = groupNames
            .GroupBy(g => g.StudentId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.Name).Distinct()));

        var studentDtos = students.Select(s => new StudentSummaryDto
        {
            Id = s.Id,
            FullName = s.FullName,
            Email = s.Email,
            AvatarUrl = s.AvatarUrl,
            TotalXp = xpMap.GetValueOrDefault(s.Id, 0),
            Level = LevelCalculator.GetLevelFromXp(xpMap.GetValueOrDefault(s.Id, 0)),
            CurrentStreak = streakMap.GetValueOrDefault(s.Id, 0),
            Skills = skillMap.GetValueOrDefault(s.Id) ?? new SkillLevelsDto(),
            LessonsCompleted = lessonsDone.GetValueOrDefault(s.Id, 0),
            SubmissionsLast7Days = weekly.GetValueOrDefault(s.Id, 0),
            GroupName = groupMap.GetValueOrDefault(s.Id),
        }).ToList();

        return new TeacherDetailDto
        {
            Teacher = summary,
            Students = studentDtos,
        };
    }

    private async Task<TeacherSummaryDto> BuildTeacherSummaryAsync(Profile teacher, CancellationToken ct)
    {
        var groupIds = await _db.Groups.AsNoTracking()
            .Where(g => g.TeacherId == teacher.Id)
            .Select(g => g.Id)
            .ToListAsync(ct);

        var studentIds = await _db.GroupStudents.AsNoTracking()
            .Where(gs => groupIds.Contains(gs.GroupId))
            .Select(gs => gs.StudentId)
            .Distinct()
            .ToListAsync(ct);

        // Average overall skill across this teacher's students.
        int? avgOverall = null;
        if (studentIds.Count > 0)
        {
            var skills = await ComputeAllSkillLevelsAsync(studentIds, ct);
            var overalls = skills.Values
                .Select(s => s.Overall)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (overalls.Count > 0) avgOverall = (int)Math.Round(overalls.Average());
        }

        // Submissions this teacher actually reviewed.
        var speakingReviewed = await _db.SpeakingSubmissions.AsNoTracking()
            .Where(s => s.TeacherId == teacher.Id)
            .Select(s => new { Created = s.CreatedAt, Reviewed = s.TeacherReviewedAt })
            .ToListAsync(ct);
        var writingReviewed = await _db.WritingSubmissions.AsNoTracking()
            .Where(s => s.TeacherId == teacher.Id)
            .Select(s => new { Created = s.CreatedAt, Reviewed = s.TeacherReviewedAt })
            .ToListAsync(ct);

        var reviews = speakingReviewed.Concat(writingReviewed)
            .Where(r => r.Reviewed.HasValue)
            .ToList();
        double? avgHours = null;
        if (reviews.Count > 0)
        {
            avgHours = Math.Round(reviews.Average(r => (r.Reviewed!.Value - r.Created).TotalHours), 2);
        }

        var coursesOwned = await _db.Courses.AsNoTracking().CountAsync(c => c.OwnerId == teacher.Id, ct);
        var notesWritten = await _db.TeacherNotes.AsNoTracking().CountAsync(n => n.AuthorId == teacher.Id, ct);

        return new TeacherSummaryDto
        {
            Id = teacher.Id,
            FullName = teacher.FullName,
            Email = teacher.Email,
            AvatarUrl = teacher.AvatarUrl,
            CreatedAt = teacher.CreatedAt,
            GroupsCount = groupIds.Count,
            StudentsCount = studentIds.Count,
            AvgStudentOverall = avgOverall,
            SubmissionsReviewed = reviews.Count,
            AvgResponseHours = avgHours,
            CoursesOwned = coursesOwned,
            TotalNotesWritten = notesWritten,
        };
    }

    // ===== Phase 6 — Adaptive Learning =====

    // QuizResult deduplicates per (user, content) and keeps the best score,
    // so "attempts" here is the count of distinct graded exercises in a
    // lesson — typically 1-4 per lesson. We use a low threshold so a single
    // failed exercise still flags the lesson for the teacher.
    private const int StruggleAttemptsThreshold = 1;
    private const int StruggleAccuracyThreshold = 70;
    private const int MasteryAccuracyThreshold = 90;
    private const int MasteryMaxAttempts = 4;

    /// <summary>
    /// Detects struggling lessons (5+ attempts at &lt;70% accuracy) and quickly
    /// mastered lessons (90%+ accuracy in 1-2 attempts) for a single student.
    /// Used to decorate StudentSummary and StudentDetail with adaptive flags.
    /// </summary>
    public async Task<AdaptiveStatusDto> ComputeAdaptiveStatusAsync(Guid studentId, CancellationToken ct)
    {
        var quizPerLesson = await _db.QuizResults.AsNoTracking()
            .Where(q => q.UserId == studentId && q.TotalQuestions > 0)
            .Select(q => new
            {
                q.LessonId,
                LessonTitle = q.Lesson!.Title,
                CourseTitle = q.Lesson.Module.Course.Title,
                Pct = (double)q.Score / q.TotalQuestions * 100.0,
            })
            .ToListAsync(ct);

        var speakingPerLesson = await _db.SpeakingSubmissions.AsNoTracking()
            .Where(s => s.UserId == studentId && s.FinalGrade != null)
            .Select(s => new
            {
                LessonId = s.Content.LessonId,
                LessonTitle = s.Content.Lesson.Title,
                CourseTitle = s.Content.Lesson.Module.Course.Title,
                Pct = (double)s.FinalGrade!.Value,
            })
            .ToListAsync(ct);

        var writingPerLesson = await _db.WritingSubmissions.AsNoTracking()
            .Where(s => s.UserId == studentId && s.FinalGrade != null)
            .Select(s => new
            {
                LessonId = s.Content.LessonId,
                LessonTitle = s.Content.Lesson.Title,
                CourseTitle = s.Content.Lesson.Module.Course.Title,
                Pct = (double)s.FinalGrade!.Value,
            })
            .ToListAsync(ct);

        var all = quizPerLesson.Concat(speakingPerLesson).Concat(writingPerLesson).ToList();
        if (all.Count == 0)
        {
            return new AdaptiveStatusDto { Status = "no_data", Summary = "Not enough submissions yet." };
        }

        // Roll up per lesson.
        var perLesson = all
            .GroupBy(x => new { x.LessonId, x.LessonTitle, x.CourseTitle })
            .Select(g => new
            {
                g.Key.LessonId,
                g.Key.LessonTitle,
                g.Key.CourseTitle,
                Attempts = g.Count(),
                Accuracy = g.Average(x => x.Pct),
            })
            .ToList();

        var struggling = perLesson
            .Where(l => l.Attempts >= StruggleAttemptsThreshold && l.Accuracy < StruggleAccuracyThreshold)
            .OrderBy(l => l.Accuracy)
            .Select(l => new StruggleAlertDto
            {
                LessonId = l.LessonId,
                LessonTitle = l.LessonTitle,
                CourseTitle = l.CourseTitle,
                AttemptCount = l.Attempts,
                AccuracyPct = (int)Math.Round(l.Accuracy),
                Severity = l.Accuracy < 50 ? "high" : "medium",
            })
            .ToList();

        var mastered = perLesson
            .Where(l => l.Attempts <= MasteryMaxAttempts && l.Accuracy >= MasteryAccuracyThreshold)
            .OrderByDescending(l => l.Accuracy)
            .Select(l => new MasteryFlagDto
            {
                LessonId = l.LessonId,
                LessonTitle = l.LessonTitle,
                CourseTitle = l.CourseTitle,
                AccuracyPct = (int)Math.Round(l.Accuracy),
                Attempts = l.Attempts,
            })
            .ToList();

        string status;
        string summary;
        if (struggling.Count > 0)
        {
            status = "struggling";
            var worst = struggling[0];
            summary = $"Struggling with \"{worst.LessonTitle}\" — {worst.AttemptCount} attempts, {worst.AccuracyPct}% accuracy.";
        }
        else if (mastered.Count >= 3)
        {
            status = "advanced";
            summary = $"Quickly mastered {mastered.Count} lesson(s) — ready for harder challenges.";
        }
        else
        {
            status = "on_track";
            summary = $"Performing on track across {perLesson.Count} lesson(s).";
        }

        var recommendations = BuildRecommendations(status, struggling, mastered);
        return new AdaptiveStatusDto
        {
            Status = status,
            Summary = summary,
            Struggling = struggling,
            MasteredQuickly = mastered,
            Recommendations = recommendations,
        };
    }

    private static List<AdaptiveRecommendationDto> BuildRecommendations(
        string status,
        List<StruggleAlertDto> struggling,
        List<MasteryFlagDto> mastered)
    {
        var recs = new List<AdaptiveRecommendationDto>();
        foreach (var s in struggling.Take(3))
        {
            recs.Add(new AdaptiveRecommendationDto
            {
                Kind = s.Severity == "high" ? "teacher_intervention" : "review_lesson",
                Title = s.Severity == "high"
                    ? $"Schedule a 1-on-1 on \"{s.LessonTitle}\""
                    : $"Review the explanation for \"{s.LessonTitle}\"",
                Detail = $"Student tried {s.AttemptCount} times with only {s.AccuracyPct}% accuracy. "
                       + (s.Severity == "high"
                          ? "Direct teacher attention is warranted — consider an extra session or a written note."
                          : "Suggest re-reading the grammar explanation and trying a slower-paced practice."),
                LessonId = s.LessonId,
            });
        }
        if (status == "advanced" && mastered.Count >= 3)
        {
            recs.Add(new AdaptiveRecommendationDto
            {
                Kind = "harder_practice",
                Title = "Offer harder material",
                Detail = $"Student mastered {mastered.Count} lessons quickly — assign more advanced reading/writing tasks, or move them ahead of the standard pacing.",
            });
        }
        return recs;
    }
}
