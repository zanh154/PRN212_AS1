namespace AssignmentPRN.Domain;

/// <summary>Mirrors the MySQL enum on exam_sessions.status.</summary>
public enum ExamSessionStatus
{
    Draft,
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

/// <summary>Mirrors the MySQL enum on exam_candidates.status.</summary>
public enum CandidateStatus
{
    Waiting,
    InProgress,
    Completed,
    Absent,
    Cancelled
}
