using LangArt.Api.Data.Entities;
using LangArt.Api.Features.Live;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonContent> LessonContent => Set<LessonContent>();
    public DbSet<LessonResource> LessonResources => Set<LessonResource>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupStudent> GroupStudents => Set<GroupStudent>();
    public DbSet<GroupCourse> GroupCourses => Set<GroupCourse>();
    public DbSet<Attendance> Attendance => Set<Attendance>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<LessonCompletion> LessonCompletions => Set<LessonCompletion>();
    public DbSet<QuizResult> QuizResults => Set<QuizResult>();
    public DbSet<StudentLessonAccess> StudentLessonAccess => Set<StudentLessonAccess>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<LiveSession> LiveSessions => Set<LiveSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
