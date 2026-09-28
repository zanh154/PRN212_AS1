using AssignmentPRN.Business;

namespace AssignmentPRN.Presentation.Models;

/// <summary>Numbers shown on the Admin and Lecturer home pages.</summary>
public class StaffDashboardViewModel
{
    public int SessionCount { get; init; }

    public int CandidateCount { get; init; }

    /// <summary>Sessions starting between now and seven days out.</summary>
    public int UpcomingCount { get; init; }

    public ExamSessionListItemResponse? NextSession { get; init; }

    public string? LoadError { get; init; }
}

/// <summary>Numbers shown on the Student home page.</summary>
public class StudentDashboardViewModel
{
    public int ExamCount { get; init; }

    public int UpcomingCount { get; init; }

    public StudentScheduleItemResponse? NextExam { get; init; }

    public string? LoadError { get; init; }
}
