using AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.DataAccess.Entities;

/// <summary>
/// One student's slot inside an exam session. The slot lasts
/// <see cref="ExamSession.TimePerStudent"/> minutes from <see cref="ScheduledTime"/>.
/// </summary>
public class ExamCandidate
{
    public int CandidateId { get; set; }

    public int ExamId { get; set; }

    /// <summary>The account being examined; a user with the Student role.</summary>
    public int StudentId { get; set; }

    /// <summary>Start of the slot. Null while the session has not been scheduled yet.</summary>
    public DateTime? ScheduledTime { get; set; }

    public CandidateStatus Status { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public ExamSession Session { get; set; } = null!;

    public User Student { get; set; } = null!;
}
