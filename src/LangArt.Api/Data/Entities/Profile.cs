using LangArt.Api.Data.Enums;

namespace LangArt.Api.Data.Entities;

public class Profile
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.Student;
    public bool IsActive { get; set; } = true;
    public bool EmailVerified { get; set; }
    public DateTime? LastLogin { get; set; }
    public string? AvatarUrl { get; set; }
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpires { get; set; }
    /// <summary>Base32-encoded TOTP secret for admin 2FA. Stubbed for now (no full UX flow).</summary>
    public string? TotpSecret { get; set; }
    public bool TotpEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<LessonCompletion> LessonCompletions { get; set; } = new List<LessonCompletion>();
    public ICollection<QuizResult> QuizResults { get; set; } = new List<QuizResult>();
    public ICollection<StudentLessonAccess> StudentLessonAccesses { get; set; } = new List<StudentLessonAccess>();
    public ICollection<Group> GroupsAsTeacher { get; set; } = new List<Group>();
    public ICollection<GroupStudent> GroupStudents { get; set; } = new List<GroupStudent>();
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<Attendance> AttendancesCreated { get; set; } = new List<Attendance>();
    public ICollection<StudentLessonAccess> StudentAccessCreated { get; set; } = new List<StudentLessonAccess>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
