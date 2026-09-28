using AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.DataAccess.Contracts;

public sealed class ExamSessionAggregateInput
{
    public int CourseId { get; init; }

    public int LecturerId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    public int TimePerStudent { get; init; }

    public int MainQuestionCount { get; init; }

    public int MaxFollowUpCount { get; init; }

    public ExamSessionStatus Status { get; init; } = ExamSessionStatus.Scheduled;

    public IReadOnlyList<ExamCandidateInput> Candidates { get; init; } = Array.Empty<ExamCandidateInput>();
}

public sealed class ExamCandidateInput
{
    public int StudentId { get; init; }

    public DateTime ScheduledTime { get; init; }

    public CandidateStatus Status { get; init; } = CandidateStatus.Waiting;
}
