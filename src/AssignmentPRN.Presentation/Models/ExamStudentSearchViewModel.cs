using AssignmentPRN.Business;

namespace AssignmentPRN.Presentation.Models;

public sealed class ExamStudentSearchViewModel
{
    public ExamStudentSearch Filter { get; init; } = new();
    public ExamStudentSearchResult Result { get; init; } = new();
    public string? Error { get; init; }
}
