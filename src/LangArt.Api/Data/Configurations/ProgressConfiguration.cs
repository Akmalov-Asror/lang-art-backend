using LangArt.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(e => e.EnrolledAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasIndex(e => new { e.UserId, e.CourseId }).IsUnique();

        b.HasOne(e => e.User)
            .WithMany(p => p.Enrollments)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.Course)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LessonCompletionConfiguration : IEntityTypeConfiguration<LessonCompletion>
{
    public void Configure(EntityTypeBuilder<LessonCompletion> b)
    {
        b.HasKey(lc => new { lc.UserId, lc.LessonId });

        b.Property(lc => lc.CompletedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(lc => lc.User)
            .WithMany(p => p.LessonCompletions)
            .HasForeignKey(lc => lc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(lc => lc.Lesson)
            .WithMany(l => l.Completions)
            .HasForeignKey(lc => lc.LessonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuizResultConfiguration : IEntityTypeConfiguration<QuizResult>
{
    public void Configure(EntityTypeBuilder<QuizResult> b)
    {
        b.HasKey(qr => qr.Id);
        b.Property(qr => qr.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(qr => qr.Score).HasColumnType("integer");
        b.Property(qr => qr.Passed).HasDefaultValue(false);
        b.Property(qr => qr.TotalQuestions).HasDefaultValue(0);
        b.Property(qr => qr.MistakesLog).HasColumnType("jsonb");
        b.Property(qr => qr.Metadata).HasColumnType("jsonb");
        b.Property(qr => qr.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(qr => qr.User)
            .WithMany(p => p.QuizResults)
            .HasForeignKey(qr => qr.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(qr => qr.Lesson)
            .WithMany(l => l.QuizResults)
            .HasForeignKey(qr => qr.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(qr => qr.Content)
            .WithMany(lc => lc.QuizResults)
            .HasForeignKey(qr => qr.ContentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class StudentLessonAccessConfiguration : IEntityTypeConfiguration<StudentLessonAccess>
{
    public void Configure(EntityTypeBuilder<StudentLessonAccess> b)
    {
        b.HasKey(sla => sla.Id);
        b.Property(sla => sla.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(sla => sla.IsUnlocked).HasDefaultValue(false);
        b.Property(sla => sla.UnlockedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasIndex(sla => new { sla.StudentId, sla.LessonId }).IsUnique();

        b.HasOne(sla => sla.Student)
            .WithMany(p => p.StudentLessonAccesses)
            .HasForeignKey(sla => sla.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(sla => sla.Lesson)
            .WithMany(l => l.StudentLessonAccesses)
            .HasForeignKey(sla => sla.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(sla => sla.Creator)
            .WithMany(p => p.StudentAccessCreated)
            .HasForeignKey(sla => sla.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(p => p.Amount).HasColumnType("decimal");
        b.Property(p => p.Currency).HasDefaultValue("USD");
        // `status` on payments is a TEXT column with CHECK, not a PG enum.
        b.Property(p => p.Status)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => (Enums.PaymentStatus)Enum.Parse(typeof(Enums.PaymentStatus), v, true))
            .HasColumnType("text");
        b.Property(p => p.PeriodStart).HasColumnType("timestamptz");
        b.Property(p => p.PeriodEnd).HasColumnType("timestamptz");
        b.Property(p => p.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(p => p.User)
            .WithMany(pr => pr.Payments)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(p => p.Course)
            .WithMany(c => c.Payments)
            .HasForeignKey(p => p.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
