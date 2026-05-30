using LangArt.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class WordlistConfiguration : IEntityTypeConfiguration<Wordlist>
{
    public void Configure(EntityTypeBuilder<Wordlist> b)
    {
        b.HasKey(w => w.Id);
        b.Property(w => w.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(w => w.Name).IsRequired();
        b.Property(w => w.Level).IsRequired().HasDefaultValue("A1");
        b.Property(w => w.IsPublic).HasDefaultValue(false);
        b.Property(w => w.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(w => w.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(w => w.Owner)
            .WithMany()
            .HasForeignKey(w => w.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(w => w.OwnerId);
        b.HasIndex(w => new { w.IsPublic, w.Level });
    }
}

public class WordConfiguration : IEntityTypeConfiguration<Word>
{
    public void Configure(EntityTypeBuilder<Word> b)
    {
        b.HasKey(w => w.Id);
        b.Property(w => w.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(w => w.Term).IsRequired();
        b.Property(w => w.TranslationUz).IsRequired().HasDefaultValue(string.Empty);
        b.Property(w => w.TranslationRu).IsRequired().HasDefaultValue(string.Empty);
        b.Property(w => w.Definition).IsRequired().HasDefaultValue(string.Empty);
        b.Property(w => w.Position).HasDefaultValue(0);
        b.Property(w => w.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(w => w.Wordlist)
            .WithMany(wl => wl.Words)
            .HasForeignKey(w => w.WordlistId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(w => w.WordlistId);
    }
}

public class UserWordlistEntryConfiguration : IEntityTypeConfiguration<UserWordlistEntry>
{
    public void Configure(EntityTypeBuilder<UserWordlistEntry> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.Status).IsRequired().HasDefaultValue("new");
        b.Property(e => e.CorrectCount).HasDefaultValue(0);
        b.Property(e => e.IncorrectCount).HasDefaultValue(0);
        b.Property(e => e.AddedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(e => e.LastReviewedAt).HasColumnType("timestamptz");
        b.Property(e => e.Source).IsRequired().HasDefaultValue("manual");
        b.Property(e => e.AssignedDate).HasColumnType("date");

        b.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.Word)
            .WithMany()
            .HasForeignKey(e => e.WordId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(e => new { e.UserId, e.WordId }).IsUnique();
        b.HasIndex(e => new { e.UserId, e.Status });
    }
}

public class LessonContentTranslationConfiguration : IEntityTypeConfiguration<LessonContentTranslation>
{
    public void Configure(EntityTypeBuilder<LessonContentTranslation> b)
    {
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(t => t.Language).IsRequired().HasMaxLength(4);
        b.Property(t => t.BodyMarkdown).IsRequired().HasDefaultValue(string.Empty);
        b.Property(t => t.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(t => t.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(t => t.Content)
            .WithMany(c => c.Translations)
            .HasForeignKey(t => t.ContentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(t => new { t.ContentId, t.Language }).IsUnique();
    }
}

public class SpeakingSubmissionConfiguration : IEntityTypeConfiguration<SpeakingSubmission>
{
    public void Configure(EntityTypeBuilder<SpeakingSubmission> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(s => s.AudioUrl).IsRequired();
        b.Property(s => s.Status).IsRequired().HasDefaultValue("submitted");
        b.Property(s => s.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(s => s.AiGradedAt).HasColumnType("timestamptz");
        b.Property(s => s.TeacherReviewedAt).HasColumnType("timestamptz");

        b.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(s => s.Content)
            .WithMany()
            .HasForeignKey(s => s.ContentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(s => s.Teacher)
            .WithMany()
            .HasForeignKey(s => s.TeacherId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(s => new { s.UserId, s.CreatedAt });
        b.HasIndex(s => s.Status);
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(m => m.Body).IsRequired();
        b.Property(m => m.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(m => m.ReadAt).HasColumnType("timestamptz");

        b.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(m => m.Recipient)
            .WithMany()
            .HasForeignKey(m => m.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(m => new { m.SenderId, m.RecipientId, m.CreatedAt });
        b.HasIndex(m => new { m.RecipientId, m.ReadAt });
    }
}

public class ParentChildLinkConfiguration : IEntityTypeConfiguration<ParentChildLink>
{
    public void Configure(EntityTypeBuilder<ParentChildLink> b)
    {
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(l => l.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(l => l.Parent)
            .WithMany()
            .HasForeignKey(l => l.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(l => l.Child)
            .WithMany()
            .HasForeignKey(l => l.ChildId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(l => new { l.ParentId, l.ChildId }).IsUnique();
    }
}

public class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.Kind).IsRequired();
        b.Property(e => e.Title).IsRequired();
        b.Property(e => e.PassingScore).HasDefaultValue(70);
        b.Property(e => e.IsActive).HasDefaultValue(true);
        b.Property(e => e.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(e => e.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(e => e.Payload).HasColumnType("jsonb");

        b.HasOne(e => e.Course)
            .WithMany()
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(e => e.Kind);
        b.HasIndex(e => e.CourseId);
    }
}

public class ExamAttemptConfiguration : IEntityTypeConfiguration<ExamAttempt>
{
    public void Configure(EntityTypeBuilder<ExamAttempt> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(a => a.StartedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(a => a.CompletedAt).HasColumnType("timestamptz");
        b.Property(a => a.SectionScores).HasColumnType("jsonb");
        b.Property(a => a.Answers).HasColumnType("jsonb");

        b.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(a => a.Exam)
            .WithMany(e => e.Attempts)
            .HasForeignKey(a => a.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(a => new { a.UserId, a.ExamId });
    }
}

public class AchievementConfiguration : IEntityTypeConfiguration<Achievement>
{
    public void Configure(EntityTypeBuilder<Achievement> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(a => a.Category).IsRequired();
        b.Property(a => a.AwardedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(a => new { a.Category, a.PeriodYear, a.PeriodMonth }).IsUnique();
        b.HasIndex(a => a.UserId);
    }
}

public class TeacherNoteConfiguration : IEntityTypeConfiguration<TeacherNote>
{
    public void Configure(EntityTypeBuilder<TeacherNote> b)
    {
        b.HasKey(n => n.Id);
        b.Property(n => n.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(n => n.Body).IsRequired();
        b.Property(n => n.Kind).IsRequired().HasDefaultValue("observation");
        b.Property(n => n.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(n => n.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(n => n.Student)
            .WithMany()
            .HasForeignKey(n => n.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(n => n.Author)
            .WithMany()
            .HasForeignKey(n => n.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(n => new { n.StudentId, n.CreatedAt });
    }
}

public class WritingSubmissionConfiguration : IEntityTypeConfiguration<WritingSubmission>
{
    public void Configure(EntityTypeBuilder<WritingSubmission> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(s => s.Text).IsRequired();
        b.Property(s => s.Status).IsRequired().HasDefaultValue("submitted");
        b.Property(s => s.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(s => s.AiGradedAt).HasColumnType("timestamptz");
        b.Property(s => s.TeacherReviewedAt).HasColumnType("timestamptz");

        b.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(s => s.Content)
            .WithMany()
            .HasForeignKey(s => s.ContentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(s => s.Teacher)
            .WithMany()
            .HasForeignKey(s => s.TeacherId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(s => new { s.UserId, s.CreatedAt });
        b.HasIndex(s => s.Status);
    }
}
