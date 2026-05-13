using LangArt.Api.Data.Entities;
using LangArt.Api.Features.Live;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class LiveSessionConfiguration : IEntityTypeConfiguration<LiveSession>
{
    public void Configure(EntityTypeBuilder<LiveSession> b)
    {
        b.ToTable("live_sessions");

        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(s => s.StartedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(s => s.EndedAt).HasColumnType("timestamptz");
        b.Property(s => s.CurrentBlockIndex).HasDefaultValue(0);
        b.Property(s => s.EndReason);

        b.HasIndex(s => new { s.ClassroomId, s.EndedAt });
        b.HasIndex(s => new { s.TeacherId, s.StartedAt })
            .IsDescending(false, true);

        // The partial unique index that enforces "only one active session per classroom"
        // is created in raw SQL — both in schema.sql and in SeedRunner.EnsureSchemaUpgradesAsync —
        // because EF Core 8 does not yet have first-class support for partial indexes via
        // the fluent API. The index name must match between schema.sql and the upgrade runner.

        b.HasOne(s => s.Classroom)
            .WithMany()
            .HasForeignKey(s => s.ClassroomId)
            .HasPrincipalKey(g => g.Id)
            .HasConstraintName("fk_live_sessions_classroom")
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(s => s.Lesson)
            .WithMany()
            .HasForeignKey(s => s.LessonId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(s => s.Teacher)
            .WithMany()
            .HasForeignKey(s => s.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
