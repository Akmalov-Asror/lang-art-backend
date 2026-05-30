using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Branches.Dto;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Branches;

public class BranchesService(AppDbContext db)
{
    public async Task<List<BranchDto>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var q = db.Branches.AsQueryable();
        if (!includeInactive) q = q.Where(b => b.IsActive);
        var rows = await q.OrderBy(b => b.Name).ToListAsync(ct);
        if (rows.Count == 0) return new();

        var ids = rows.Select(r => r.Id).ToList();
        var studentCounts = await db.Profiles
            .Where(p => p.BranchId != null && ids.Contains(p.BranchId!.Value) && p.Role == Role.Student)
            .GroupBy(p => p.BranchId)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.BranchId!.Value, g => g.Count, ct);
        var teacherCounts = await db.Profiles
            .Where(p => p.BranchId != null && ids.Contains(p.BranchId!.Value) && p.Role == Role.Teacher)
            .GroupBy(p => p.BranchId)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.BranchId!.Value, g => g.Count, ct);
        var groupCounts = await db.Groups
            .Where(gr => gr.BranchId != null && ids.Contains(gr.BranchId!.Value))
            .GroupBy(gr => gr.BranchId)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.BranchId!.Value, g => g.Count, ct);

        return rows.Select(b => ToDto(b, studentCounts, teacherCounts, groupCounts)).ToList();
    }

    public async Task<BranchDto> GetAsync(Guid id, CancellationToken ct)
    {
        var b = await db.Branches.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Branch not found.");
        var studentCount = await db.Profiles.CountAsync(p => p.BranchId == id && p.Role == Role.Student, ct);
        var teacherCount = await db.Profiles.CountAsync(p => p.BranchId == id && p.Role == Role.Teacher, ct);
        var groupCount = await db.Groups.CountAsync(g => g.BranchId == id, ct);
        return new BranchDto
        {
            Id = b.Id,
            Name = b.Name,
            Code = b.Code,
            City = b.City,
            Address = b.Address,
            Phone = b.Phone,
            ManagerName = b.ManagerName,
            ManagerProfileId = b.ManagerProfileId,
            IsActive = b.IsActive,
            StudentCount = studentCount,
            TeacherCount = teacherCount,
            GroupCount = groupCount,
            CreatedAt = b.CreatedAt,
        };
    }

    public async Task<BranchDto> CreateAsync(CreateBranchRequest req, CancellationToken ct)
    {
        var code = req.Code.Trim().ToLowerInvariant();
        if (await db.Branches.AnyAsync(b => b.Code == code, ct))
            throw new ConflictException("Branch code already exists.");

        var b = new Branch
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim(),
            Code = code,
            City = req.City,
            Address = req.Address,
            Phone = req.Phone,
            ManagerName = req.ManagerName,
            ManagerProfileId = req.ManagerProfileId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Branches.Add(b);
        await db.SaveChangesAsync(ct);
        return await GetAsync(b.Id, ct);
    }

    public async Task<BranchDto> UpdateAsync(Guid id, UpdateBranchRequest req, CancellationToken ct)
    {
        var b = await db.Branches.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Branch not found.");
        if (req.Name != null) b.Name = req.Name.Trim();
        if (req.Code != null)
        {
            var code = req.Code.Trim().ToLowerInvariant();
            if (code != b.Code && await db.Branches.AnyAsync(x => x.Code == code, ct))
                throw new ConflictException("Branch code already exists.");
            b.Code = code;
        }
        if (req.City != null) b.City = req.City;
        if (req.Address != null) b.Address = req.Address;
        if (req.Phone != null) b.Phone = req.Phone;
        if (req.ManagerName != null) b.ManagerName = req.ManagerName;
        if (req.ManagerProfileId.HasValue) b.ManagerProfileId = req.ManagerProfileId;
        if (req.IsActive.HasValue) b.IsActive = req.IsActive.Value;
        b.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var rows = await db.Branches.Where(b => b.Id == id).ExecuteDeleteAsync(ct);
        if (rows == 0) throw new NotFoundException("Branch not found.");
    }

    public async Task AssignProfileAsync(Guid profileId, Guid? branchId, CancellationToken ct)
    {
        if (branchId.HasValue && !await db.Branches.AnyAsync(b => b.Id == branchId, ct))
            throw new NotFoundException("Branch not found.");
        var rows = await db.Profiles
            .Where(p => p.Id == profileId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.BranchId, branchId), ct);
        if (rows == 0) throw new NotFoundException("Profile not found.");
    }

    public async Task AssignGroupAsync(Guid groupId, Guid? branchId, CancellationToken ct)
    {
        if (branchId.HasValue && !await db.Branches.AnyAsync(b => b.Id == branchId, ct))
            throw new NotFoundException("Branch not found.");
        var rows = await db.Groups
            .Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.BranchId, branchId), ct);
        if (rows == 0) throw new NotFoundException("Group not found.");
    }

    private static BranchDto ToDto(
        Branch b,
        Dictionary<Guid, int> studentCounts,
        Dictionary<Guid, int> teacherCounts,
        Dictionary<Guid, int> groupCounts) => new()
    {
        Id = b.Id,
        Name = b.Name,
        Code = b.Code,
        City = b.City,
        Address = b.Address,
        Phone = b.Phone,
        ManagerName = b.ManagerName,
        ManagerProfileId = b.ManagerProfileId,
        IsActive = b.IsActive,
        StudentCount = studentCounts.GetValueOrDefault(b.Id, 0),
        TeacherCount = teacherCounts.GetValueOrDefault(b.Id, 0),
        GroupCount = groupCounts.GetValueOrDefault(b.Id, 0),
        CreatedAt = b.CreatedAt,
    };
}
