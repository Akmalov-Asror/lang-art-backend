using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Users.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Users;

public class UsersService
{
    private readonly AppDbContext _db;

    public UsersService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<UserResponse>> ListAsync(Role? role)
    {
        var query = _db.Profiles.AsNoTracking().AsQueryable();
        if (role.HasValue) query = query.Where(p => p.Role == role.Value);

        var rows = await query.OrderBy(p => p.CreatedAt).ToListAsync();
        return rows.Select(ToResponse).ToList();
    }

    public async Task<UserResponse> GetByIdAsync(Guid id)
    {
        var user = await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id)
                   ?? throw new NotFoundException("User not found");
        return ToResponse(user);
    }

    public async Task<UserStatsResponse> GetStatsAsync()
    {
        var users = await _db.Profiles
            .GroupBy(p => p.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync();

        var totalCourses = await _db.Courses.CountAsync();

        return new UserStatsResponse
        {
            TotalUsers = users.Sum(u => u.Count),
            TotalStudents = users.FirstOrDefault(u => u.Role == Role.Student)?.Count ?? 0,
            TotalTeachers = users.FirstOrDefault(u => u.Role == Role.Teacher)?.Count ?? 0,
            TotalCourses = totalCourses,
        };
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest dto)
    {
        var exists = await _db.Profiles.AnyAsync(p => p.Email == dto.Email);
        if (exists) throw new ConflictException("Email already registered");

        var user = new Profile
        {
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 10),
            FullName = dto.FullName,
            Role = dto.Role ?? Role.Student,
            IsActive = dto.IsActive ?? true,
        };
        _db.Profiles.Add(user);
        await _db.SaveChangesAsync();

        return ToResponse(user);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest dto)
    {
        var user = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == id)
                   ?? throw new NotFoundException("User not found");

        if (dto.FullName is not null) user.FullName = dto.FullName;
        if (dto.Role.HasValue) user.Role = dto.Role.Value;
        if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToResponse(user);
    }

    public async Task DeleteAsync(Guid id)
    {
        var deleted = await _db.Profiles.Where(p => p.Id == id).ExecuteDeleteAsync();
        if (deleted == 0) throw new NotFoundException("User not found");
    }

    private static UserResponse ToResponse(Profile p) => new()
    {
        Id = p.Id,
        Email = p.Email,
        FullName = p.FullName,
        Role = p.Role.ToString().ToLowerInvariant(),
        IsActive = p.IsActive,
        EmailVerified = p.EmailVerified,
        LastLogin = p.LastLogin,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
    };
}
