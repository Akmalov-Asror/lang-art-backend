using System.Text.Json;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Features.Courses.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Courses;

public class CoursesService
{
    private readonly AppDbContext _db;

    public CoursesService(AppDbContext db)
    {
        _db = db;
    }

    // ---------------- Courses ----------------

    public async Task<List<CourseResponse>> ListAsync()
    {
        var rows = await _db.Courses
            .AsNoTracking()
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
        return rows.Select(ToResponse).ToList();
    }

    public async Task<List<CourseResponse>> ListEnrolledForUserAsync(Guid userId)
    {
        // A user has access to a course if either:
        //   - they have an Enrollment for it, OR
        //   - they're a student in a group that has the course assigned.
        var enrolled = _db.Enrollments
            .Where(e => e.UserId == userId)
            .Select(e => e.Course);

        var viaGroup = _db.GroupStudents
            .Where(gs => gs.StudentId == userId)
            .SelectMany(gs => gs.Group.GroupCourses)
            .Select(gc => gc.Course);

        var union = await enrolled.Union(viaGroup)
            .Distinct()
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return union.Select(ToResponse).ToList();
    }

    public async Task<CourseWithModulesResponse> GetByIdAsync(Guid id)
    {
        var course = await _db.Courses
            .AsNoTracking()
            .Include(c => c.Modules.OrderBy(m => m.OrderIndex))
                .ThenInclude(m => m.Lessons.OrderBy(l => l.OrderIndex))
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("Course not found");

        return new CourseWithModulesResponse
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            ThumbnailUrl = course.ThumbnailUrl,
            PriceMonthly = course.PriceMonthly,
            CreatedAt = course.CreatedAt,
            UpdatedAt = course.UpdatedAt,
            Modules = course.Modules
                .OrderBy(m => m.OrderIndex)
                .Select(m => new ModuleWithLessonsResponse
                {
                    Id = m.Id,
                    CourseId = m.CourseId,
                    Title = m.Title,
                    OrderIndex = m.OrderIndex,
                    CreatedAt = m.CreatedAt,
                    Lessons = m.Lessons
                        .OrderBy(l => l.OrderIndex)
                        .Select(ToLessonResponse)
                        .ToList(),
                })
                .ToList(),
        };
    }

    public async Task<CourseResponse> CreateAsync(CreateCourseRequest dto)
    {
        var course = new Course
        {
            Title = dto.Title,
            Description = dto.Description,
            ThumbnailUrl = dto.ThumbnailUrl,
            PriceMonthly = dto.PriceMonthly,
        };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();
        return ToResponse(course);
    }

    public async Task<CourseResponse> UpdateAsync(Guid id, UpdateCourseRequest dto)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == id)
                     ?? throw new NotFoundException("Course not found");

        if (dto.Title is not null) course.Title = dto.Title;
        if (dto.Description is not null) course.Description = dto.Description;
        if (dto.ThumbnailUrl is not null) course.ThumbnailUrl = dto.ThumbnailUrl;
        if (dto.PriceMonthly.HasValue) course.PriceMonthly = dto.PriceMonthly.Value;
        course.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToResponse(course);
    }

    public async Task DeleteCourseAsync(Guid id)
    {
        var deleted = await _db.Courses.Where(c => c.Id == id).ExecuteDeleteAsync();
        if (deleted == 0) throw new NotFoundException("Course not found");
    }

    /// <summary>
    /// Duplicate a course including its full module/lesson/content/resource tree.
    /// New rows get fresh ids and the title gets a "(Copy)" suffix; enrollments,
    /// completions, payments and group assignments are NOT copied.
    /// </summary>
    public async Task<CourseResponse> DuplicateAsync(Guid sourceCourseId)
    {
        var source = await _db.Courses
            .AsNoTracking()
            .Include(c => c.Modules.OrderBy(m => m.OrderIndex))
                .ThenInclude(m => m.Lessons.OrderBy(l => l.OrderIndex))
                    .ThenInclude(l => l.Contents.OrderBy(co => co.OrderIndex))
            .Include(c => c.Modules)
                .ThenInclude(m => m.Lessons)
                    .ThenInclude(l => l.Resources)
            .FirstOrDefaultAsync(c => c.Id == sourceCourseId)
            ?? throw new NotFoundException("Course not found");

        var copy = new Course
        {
            Title = $"{source.Title} (Copy)",
            Description = source.Description,
            ThumbnailUrl = source.ThumbnailUrl,
            PriceMonthly = source.PriceMonthly,
        };
        _db.Courses.Add(copy);
        await _db.SaveChangesAsync();

        foreach (var srcModule in source.Modules.OrderBy(m => m.OrderIndex))
        {
            var newModule = new Data.Entities.Module
            {
                CourseId = copy.Id,
                Title = srcModule.Title,
                OrderIndex = srcModule.OrderIndex,
            };
            _db.Modules.Add(newModule);
            await _db.SaveChangesAsync();

            foreach (var srcLesson in srcModule.Lessons.OrderBy(l => l.OrderIndex))
            {
                var newLesson = new Lesson
                {
                    ModuleId = newModule.Id,
                    Title = srcLesson.Title,
                    OrderIndex = srcLesson.OrderIndex,
                    IsLocked = srcLesson.IsLocked,
                };
                _db.Lessons.Add(newLesson);
                await _db.SaveChangesAsync();

                foreach (var srcContent in srcLesson.Contents.OrderBy(co => co.OrderIndex))
                {
                    _db.LessonContent.Add(new LessonContent
                    {
                        LessonId = newLesson.Id,
                        Type = srcContent.Type,
                        // JsonDocument needs a fresh parse so the new row owns its own buffer.
                        ContentPayload = System.Text.Json.JsonDocument.Parse(srcContent.ContentPayload.RootElement.GetRawText()),
                        OrderIndex = srcContent.OrderIndex,
                        ExerciseType = srcContent.ExerciseType,
                    });
                }
                foreach (var srcRes in srcLesson.Resources)
                {
                    _db.LessonResources.Add(new LessonResource
                    {
                        LessonId = newLesson.Id,
                        Title = srcRes.Title,
                        FileUrl = srcRes.FileUrl,
                        FileType = srcRes.FileType,
                    });
                }
                await _db.SaveChangesAsync();
            }
        }

        return ToResponse(copy);
    }

    // ---------------- Modules ----------------

    public async Task<ModuleResponse> CreateModuleAsync(Guid courseId, CreateModuleRequest dto)
    {
        var courseExists = await _db.Courses.AnyAsync(c => c.Id == courseId);
        if (!courseExists) throw new NotFoundException("Course not found");

        int order = dto.OrderIndex ?? (await _db.Modules
            .Where(m => m.CourseId == courseId)
            .Select(m => (int?)m.OrderIndex)
            .MaxAsync() ?? -1) + 1;

        var module = new Data.Entities.Module
        {
            CourseId = courseId,
            Title = dto.Title,
            OrderIndex = order,
        };
        _db.Modules.Add(module);
        await _db.SaveChangesAsync();
        return ToModuleResponse(module);
    }

    public async Task<ModuleResponse> UpdateModuleAsync(Guid moduleId, UpdateModuleRequest dto)
    {
        var module = await _db.Modules.FirstOrDefaultAsync(m => m.Id == moduleId)
                     ?? throw new NotFoundException("Module not found");

        if (dto.Title is not null) module.Title = dto.Title;
        if (dto.OrderIndex.HasValue) module.OrderIndex = dto.OrderIndex.Value;

        await _db.SaveChangesAsync();
        return ToModuleResponse(module);
    }

    public async Task DeleteModuleAsync(Guid moduleId)
    {
        var deleted = await _db.Modules.Where(m => m.Id == moduleId).ExecuteDeleteAsync();
        if (deleted == 0) throw new NotFoundException("Module not found");
    }

    // ---------------- Lessons ----------------

    public async Task<LessonWithContentResponse> GetLessonAsync(Guid lessonId)
    {
        var lesson = await _db.Lessons
            .AsNoTracking()
            .Include(l => l.Contents)
            .Include(l => l.Resources)
            .FirstOrDefaultAsync(l => l.Id == lessonId)
            ?? throw new NotFoundException("Lesson not found");

        return new LessonWithContentResponse
        {
            Id = lesson.Id,
            ModuleId = lesson.ModuleId,
            Title = lesson.Title,
            OrderIndex = lesson.OrderIndex,
            IsLocked = lesson.IsLocked,
            CreatedAt = lesson.CreatedAt,
            Contents = lesson.Contents
                .OrderBy(c => c.OrderIndex)
                .Select(ToContentResponse)
                .ToList(),
            Resources = lesson.Resources
                .OrderBy(r => r.CreatedAt)
                .Select(ToResourceResponse)
                .ToList(),
        };
    }

    public async Task<LessonResponse> CreateLessonAsync(Guid moduleId, CreateLessonRequest dto)
    {
        var moduleExists = await _db.Modules.AnyAsync(m => m.Id == moduleId);
        if (!moduleExists) throw new NotFoundException("Module not found");

        int order = dto.OrderIndex ?? (await _db.Lessons
            .Where(l => l.ModuleId == moduleId)
            .Select(l => (int?)l.OrderIndex)
            .MaxAsync() ?? -1) + 1;

        var lesson = new Lesson
        {
            ModuleId = moduleId,
            Title = dto.Title,
            OrderIndex = order,
            IsLocked = dto.IsLocked ?? false,
        };
        _db.Lessons.Add(lesson);
        await _db.SaveChangesAsync();
        return ToLessonResponse(lesson);
    }

    public async Task<LessonResponse> UpdateLessonAsync(Guid lessonId, UpdateLessonRequest dto)
    {
        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == lessonId)
                     ?? throw new NotFoundException("Lesson not found");

        if (dto.Title is not null) lesson.Title = dto.Title;
        if (dto.OrderIndex.HasValue) lesson.OrderIndex = dto.OrderIndex.Value;
        if (dto.IsLocked.HasValue) lesson.IsLocked = dto.IsLocked.Value;

        await _db.SaveChangesAsync();
        return ToLessonResponse(lesson);
    }

    public async Task DeleteLessonAsync(Guid lessonId)
    {
        var deleted = await _db.Lessons.Where(l => l.Id == lessonId).ExecuteDeleteAsync();
        if (deleted == 0) throw new NotFoundException("Lesson not found");
    }

    // ---------------- Lesson content ----------------

    public async Task<LessonContentResponse> GetLessonContentByIdAsync(Guid contentId)
    {
        var content = await _db.LessonContent
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contentId)
            ?? throw new NotFoundException("Lesson content not found");
        return ToContentResponse(content);
    }

    public async Task<LessonContentResponse> CreateLessonContentAsync(Guid lessonId, CreateLessonContentRequest dto)
    {
        var lessonExists = await _db.Lessons.AnyAsync(l => l.Id == lessonId);
        if (!lessonExists) throw new NotFoundException("Lesson not found");

        int order = dto.OrderIndex ?? (await _db.LessonContent
            .Where(c => c.LessonId == lessonId)
            .Select(c => (int?)c.OrderIndex)
            .MaxAsync() ?? -1) + 1;

        var content = new LessonContent
        {
            LessonId = lessonId,
            Type = dto.Type,
            ContentPayload = JsonDocument.Parse(dto.ContentPayload.GetRawText()),
            OrderIndex = order,
            ExerciseType = dto.ExerciseType,
        };
        _db.LessonContent.Add(content);
        await _db.SaveChangesAsync();
        return ToContentResponse(content);
    }

    public async Task<LessonContentResponse> UpdateLessonContentAsync(Guid contentId, UpdateLessonContentRequest dto)
    {
        var content = await _db.LessonContent.FirstOrDefaultAsync(c => c.Id == contentId)
                      ?? throw new NotFoundException("Lesson content not found");

        if (dto.Type.HasValue) content.Type = dto.Type.Value;
        if (dto.OrderIndex.HasValue) content.OrderIndex = dto.OrderIndex.Value;
        if (dto.ExerciseType is not null) content.ExerciseType = dto.ExerciseType;
        if (dto.ContentPayload.HasValue)
        {
            content.ContentPayload = JsonDocument.Parse(dto.ContentPayload.Value.GetRawText());
        }

        await _db.SaveChangesAsync();
        return ToContentResponse(content);
    }

    public async Task DeleteLessonContentAsync(Guid contentId)
    {
        var deleted = await _db.LessonContent.Where(c => c.Id == contentId).ExecuteDeleteAsync();
        if (deleted == 0) throw new NotFoundException("Lesson content not found");
    }

    // ---------------- Resources ----------------

    public async Task<List<LessonResourceResponse>> GetLessonResourcesAsync(Guid lessonId)
    {
        var rows = await _db.LessonResources
            .AsNoTracking()
            .Where(r => r.LessonId == lessonId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
        return rows.Select(ToResourceResponse).ToList();
    }

    public async Task<LessonResourceResponse> CreateLessonResourceAsync(Guid lessonId, CreateLessonResourceRequest dto)
    {
        var lessonExists = await _db.Lessons.AnyAsync(l => l.Id == lessonId);
        if (!lessonExists) throw new NotFoundException("Lesson not found");

        var resource = new LessonResource
        {
            LessonId = lessonId,
            Title = dto.Title,
            FileUrl = dto.FileUrl,
            FileType = dto.FileType,
        };
        _db.LessonResources.Add(resource);
        await _db.SaveChangesAsync();
        return ToResourceResponse(resource);
    }

    public async Task DeleteLessonResourceAsync(Guid resourceId)
    {
        var deleted = await _db.LessonResources.Where(r => r.Id == resourceId).ExecuteDeleteAsync();
        if (deleted == 0) throw new NotFoundException("Resource not found");
    }

    // ---------------- Mappers ----------------

    private static CourseResponse ToResponse(Course c) => new()
    {
        Id = c.Id,
        Title = c.Title,
        Description = c.Description,
        ThumbnailUrl = c.ThumbnailUrl,
        PriceMonthly = c.PriceMonthly,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
    };

    private static ModuleResponse ToModuleResponse(Data.Entities.Module m) => new()
    {
        Id = m.Id,
        CourseId = m.CourseId,
        Title = m.Title,
        OrderIndex = m.OrderIndex,
        CreatedAt = m.CreatedAt,
    };

    private static LessonResponse ToLessonResponse(Lesson l) => new()
    {
        Id = l.Id,
        ModuleId = l.ModuleId,
        Title = l.Title,
        OrderIndex = l.OrderIndex,
        IsLocked = l.IsLocked,
        CreatedAt = l.CreatedAt,
    };

    private static LessonContentResponse ToContentResponse(LessonContent c) => new()
    {
        Id = c.Id,
        LessonId = c.LessonId,
        Type = c.Type,
        ContentPayload = c.ContentPayload.RootElement.Clone(),
        OrderIndex = c.OrderIndex,
        ExerciseType = c.ExerciseType,
        CreatedAt = c.CreatedAt,
    };

    private static LessonResourceResponse ToResourceResponse(LessonResource r) => new()
    {
        Id = r.Id,
        LessonId = r.LessonId,
        Title = r.Title,
        FileUrl = r.FileUrl,
        FileType = r.FileType,
        CreatedAt = r.CreatedAt,
    };
}
