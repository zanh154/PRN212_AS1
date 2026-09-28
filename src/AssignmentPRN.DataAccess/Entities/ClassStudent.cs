namespace AssignmentPRN.DataAccess.Entities;

public class ClassStudent
{
    public int ClassId { get; set; }

    public int StudentId { get; set; }

    public DateTime JoinedAt { get; set; }

    public AcademicClass Class { get; set; } = null!;

    public User Student { get; set; } = null!;
}
