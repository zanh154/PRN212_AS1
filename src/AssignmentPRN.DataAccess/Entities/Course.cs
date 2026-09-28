namespace AssignmentPRN.DataAccess.Entities;

public class Course
{
    public int CourseId { get; set; }

    public string CourseCode { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>The account that owns the course; a user with the Lecturer role.</summary>
    public int LecturerId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public User Lecturer { get; set; } = null!;

    public ICollection<ExamSession> ExamSessions { get; set; } = new List<ExamSession>();

    public ICollection<AcademicClass> Classes { get; set; } = new List<AcademicClass>();
}
