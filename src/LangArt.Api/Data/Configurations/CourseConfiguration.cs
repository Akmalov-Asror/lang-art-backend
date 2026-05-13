using LangArt.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(c => c.Title).IsRequired();
        b.Property(c => c.PriceMonthly).HasColumnType("decimal");
        b.Property(c => c.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(c => c.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
    }
}

public class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> b)
    {
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(m => m.Title).IsRequired();
        b.Property(m => m.OrderIndex).HasDefaultValue(0);
        b.Property(m => m.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(m => m.Course)
            .WithMany(c => c.Modules)
            .HasForeignKey(m => m.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> b)
    {
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(l => l.Title).IsRequired();
        b.Property(l => l.OrderIndex).HasDefaultValue(0);
        b.Property(l => l.IsLocked).HasDefaultValue(false);
        b.Property(l => l.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(l => l.Module)
            .WithMany(m => m.Lessons)
            .HasForeignKey(l => l.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LessonContentConfiguration : IEntityTypeConfiguration<LessonContent>
{
    public void Configure(EntityTypeBuilder<LessonContent> b)
    {
        b.HasKey(lc => lc.Id);
        b.Property(lc => lc.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(lc => lc.ContentPayload).HasColumnType("jsonb");
        b.Property(lc => lc.OrderIndex).HasDefaultValue(0);
        b.Property(lc => lc.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(lc => lc.Lesson)
            .WithMany(l => l.Contents)
            .HasForeignKey(lc => lc.LessonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LessonResourceConfiguration : IEntityTypeConfiguration<LessonResource>
{
    public void Configure(EntityTypeBuilder<LessonResource> b)
    {
        b.HasKey(lr => lr.Id);
        b.Property(lr => lr.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(lr => lr.Title).IsRequired();
        b.Property(lr => lr.FileUrl).IsRequired();
        b.Property(lr => lr.FileType).IsRequired();
        b.Property(lr => lr.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(lr => lr.Lesson)
            .WithMany(l => l.Resources)
            .HasForeignKey(lr => lr.LessonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
