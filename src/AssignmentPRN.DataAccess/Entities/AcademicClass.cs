namespace AssignmentPRN.DataAccess.Entities;

public class AcademicClass
{
    public int ClassId { get; set; }

    public string ClassCode { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public int CourseId { get; set; }

    public int LecturerId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Course Course { get; set; } = null!;

    public User Lecturer { get; set; } = null!;

    public ICollection<ClassStudent> Students { get; set; } = new List<ClassStudent>();
}
