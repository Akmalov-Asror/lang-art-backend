using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Gamification;
using LangArt.Api.Features.Parents.Dto;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Parents;

public class ParentsService
{
    private readonly AppDbContext _db;

    public ParentsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ChildSummaryDto>> GetMyChildrenAsync(Guid parentId, CancellationToken ct)
    {
        var links = await _db.ParentChildLinks.AsNoTracking()
            .Where(l => l.ParentId == parentId)
            .Include(l => l.Child)
            .ToListAsync(ct);

        var result = new List<ChildSummaryDto>();
        foreach (var link in links)
        {
            var c = link.Child;
            var xp = await _db.UserXp.AsNoTracking()
                .Where(x => x.UserId == c.Id)
                .Select(x => (int?)x.TotalXp)
                .FirstOrDefaultAsync(ct) ?? 0;
            var streak = await _db.UserStreaks.AsNoTracking()
                .Where(x => x.UserId == c.Id)
                .Select(x => (int?)x.CurrentStreak)
                .FirstOrDefaultAsync(ct) ?? 0;
            var lessonsDone = await _db.LessonCompletions.AsNoTracking()
                .CountAsync(lc => lc.UserId == c.Id, ct);
            var attendance = await _db.Attendance.AsNoTracking()
                .Where(a => a.StudentId == c.Id)
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var present = attendance.FirstOrDefault(a => a.Status == AttendanceStatus.Present)?.Count ?? 0;
            var absent = attendance.FirstOrDefault(a => a.Status == AttendanceStatus.Absent)?.Count ?? 0;
            var lastActivity = await _db.LessonCompletions.AsNoTracking()
                .Where(lc => lc.UserId == c.Id)
                .OrderByDescending(lc => lc.CompletedAt)
                .Select(lc => (DateTime?)lc.CompletedAt)
                .FirstOrDefaultAsync(ct);

            // Simple skill rollup: average quiz/exercise score percentages.
            var quizPcts = await _db.QuizResults.AsNoTracking()
                .Where(q => q.UserId == c.Id && q.TotalQuestions > 0)
                .Select(q => 100.0 * q.Score / q.TotalQuestions)
                .ToListAsync(ct);
            var overall = quizPcts.Count == 0 ? 0 : (int)Math.Round(quizPcts.Average());

            result.Add(new ChildSummaryDto
            {
                Id = c.Id,
                FullName = c.FullName,
                Email = c.Email,
                AvatarUrl = c.AvatarUrl,
                Relationship = link.Relationship,
                TotalXp = xp,
                Level = LevelCalculator.GetLevelFromXp(xp),
                CurrentStreak = streak,
                LessonsCompleted = lessonsDone,
                AttendancePresentCount = present,
                AttendanceAbsentCount = absent,
                OverallSkill = overall,
                LastActivityDate = lastActivity,
            });
        }
        return result;
    }

    public async Task LinkChildAsync(Guid parentId, LinkChildRequest req, CancellationToken ct)
    {
        var childExists = await _db.Profiles.AnyAsync(p => p.Id == req.ChildId && p.Role == Role.Student, ct);
        if (!childExists) throw new NotFoundException("Student not found");

        var existing = await _db.ParentChildLinks
            .FirstOrDefaultAsync(l => l.ParentId == parentId && l.ChildId == req.ChildId, ct);
        if (existing is not null) return;

        _db.ParentChildLinks.Add(new ParentChildLink
        {
            ParentId = parentId,
            ChildId = req.ChildId,
            Relationship = req.Relationship,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task UnlinkChildAsync(Guid parentId, Guid childId, CancellationToken ct)
    {
        var deleted = await _db.ParentChildLinks
            .Where(l => l.ParentId == parentId && l.ChildId == childId)
            .ExecuteDeleteAsync(ct);
        if (deleted == 0) throw new NotFoundException("Link not found");
    }

    public async Task<Profile> CreateParentAccountAsync(CreateParentAccountRequest req, CancellationToken ct)
    {
        var childExists = await _db.Profiles.AnyAsync(p => p.Id == req.ChildId && p.Role == Role.Student, ct);
        if (!childExists) throw new NotFoundException("Child student not found");

        var existing = await _db.Profiles.AnyAsync(p => p.Email == req.Email, ct);
        if (existing) throw new ConflictException("A user with this email already exists");

        var parent = new Profile
        {
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 10),
            FullName = req.FullName,
            Role = Role.Parent,
            IsActive = true,
            EmailVerified = true,
        };
        _db.Profiles.Add(parent);
        await _db.SaveChangesAsync(ct);

        _db.ParentChildLinks.Add(new ParentChildLink
        {
            ParentId = parent.Id,
            ChildId = req.ChildId,
            Relationship = req.Relationship,
        });
        await _db.SaveChangesAsync(ct);

        return parent;
    }
}
