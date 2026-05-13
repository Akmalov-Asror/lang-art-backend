using LangArt.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> b)
    {
        b.HasKey(g => g.Id);
        b.Property(g => g.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(g => g.Name).IsRequired();
        b.Property(g => g.IsActive).HasDefaultValue(true);
        b.Property(g => g.StartDate).HasColumnType("date").HasDefaultValueSql("now()");
        b.Property(g => g.ScheduleDays).HasColumnType("text[]").HasDefaultValueSql("'{}'::text[]");
        b.Property(g => g.StartTime).HasColumnType("time");
        b.Property(g => g.EndTime).HasColumnType("time");
        b.Property(g => g.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(g => g.Teacher)
            .WithMany(p => p.GroupsAsTeacher)
            .HasForeignKey(g => g.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class GroupStudentConfiguration : IEntityTypeConfiguration<GroupStudent>
{
    public void Configure(EntityTypeBuilder<GroupStudent> b)
    {
        b.HasKey(gs => gs.Id);
        b.Property(gs => gs.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(gs => gs.JoinedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasIndex(gs => new { gs.GroupId, gs.StudentId }).IsUnique();

        b.HasOne(gs => gs.Group)
            .WithMany(g => g.GroupStudents)
            .HasForeignKey(gs => gs.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(gs => gs.Student)
            .WithMany(p => p.GroupStudents)
            .HasForeignKey(gs => gs.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class GroupCourseConfiguration : IEntityTypeConfiguration<GroupCourse>
{
    public void Configure(EntityTypeBuilder<GroupCourse> b)
    {
        b.HasKey(gc => new { gc.GroupId, gc.CourseId });

        b.Property(gc => gc.AssignedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(gc => gc.Group)
            .WithMany(g => g.GroupCourses)
            .HasForeignKey(gc => gc.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(gc => gc.Course)
            .WithMany(c => c.GroupCourses)
            .HasForeignKey(gc => gc.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(a => a.Date).HasColumnType("date").HasDefaultValueSql("now()");
        b.Property(a => a.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasIndex(a => new { a.GroupId, a.StudentId, a.Date }).IsUnique();

        b.HasOne(a => a.Group)
            .WithMany(g => g.Attendances)
            .HasForeignKey(a => a.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(a => a.Student)
            .WithMany(p => p.Attendances)
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(a => a.Creator)
            .WithMany(p => p.AttendancesCreated)
            .HasForeignKey(a => a.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
