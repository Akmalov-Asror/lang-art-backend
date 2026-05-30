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

    // Gamification (Sprint 1)
    public DbSet<UserXp> UserXp => Set<UserXp>();
    public DbSet<UserStreak> UserStreaks => Set<UserStreak>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<XpLedger> XpLedger => Set<XpLedger>();

    // Push notifications (Sprint 1, Phase B)
    public DbSet<Features.Notifications.Push.PushSubscription> PushSubscriptions => Set<Features.Notifications.Push.PushSubscription>();

    // Vocabulary (Phase 1)
    public DbSet<Wordlist> Wordlists => Set<Wordlist>();
    public DbSet<Word> Words => Set<Word>();
    public DbSet<UserWordlistEntry> UserWordlistEntries => Set<UserWordlistEntry>();
    public DbSet<WisdomImportJob> WisdomImportJobs => Set<WisdomImportJob>();

    // Finance / Buxgalteriya
    public DbSet<FinanceCategory> FinanceCategories => Set<FinanceCategory>();
    public DbSet<FinanceTransaction> FinanceTransactions => Set<FinanceTransaction>();

    // Multilingual content (Phase 2)
    public DbSet<LessonContentTranslation> LessonContentTranslations => Set<LessonContentTranslation>();

    // Speaking submissions (Phase 3)
    public DbSet<SpeakingSubmission> SpeakingSubmissions => Set<SpeakingSubmission>();

    // Writing submissions (Phase 7)
    public DbSet<WritingSubmission> WritingSubmissions => Set<WritingSubmission>();

    // Teacher analytics (Phase 8)
    public DbSet<TeacherNote> TeacherNotes => Set<TeacherNote>();

    // Achievements (Phase 11)
    public DbSet<Achievement> Achievements => Set<Achievement>();

    // Exams (Phase 12)
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();

    // Parent portal (Phase 14)
    public DbSet<ParentChildLink> ParentChildLinks => Set<ParentChildLink>();

    // Messaging (Phase 16)
    public DbSet<Message> Messages => Set<Message>();

    // CRM / Lead Funnel (Phase 18)
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadActivity> LeadActivities => Set<LeadActivity>();

    // Multibranch (Phase 19)
    public DbSet<Branch> Branches => Set<Branch>();

    // Extra-curricular events (Phase 20)
    public DbSet<ClubEvent> ClubEvents => Set<ClubEvent>();
    public DbSet<ClubEventRsvp> ClubEventRsvps => Set<ClubEventRsvp>();

    // Referrals (Phase 21)
    public DbSet<Referral> Referrals => Set<Referral>();

    // Legal documents (Phase 22)
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<LegalAcceptance> LegalAcceptances => Set<LegalAcceptance>();

    // LA Dollar currency (Phase 4)
    public DbSet<UserLaDollarBalance> UserLaDollarBalances => Set<UserLaDollarBalance>();
    public DbSet<LaDollarLedger> LaDollarLedger => Set<LaDollarLedger>();
    public DbSet<LaDollarStoreItem> LaDollarStoreItems => Set<LaDollarStoreItem>();
    public DbSet<LaDollarPurchase> LaDollarPurchases => Set<LaDollarPurchase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
