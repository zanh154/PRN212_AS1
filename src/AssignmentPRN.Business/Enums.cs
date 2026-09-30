namespace AssignmentPRN.Business;

public enum ExamSessionStatus
{
    Draft,
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

public enum CandidateStatus
{
    Waiting,
    InProgress,
    Completed,
    Absent,
    Cancelled
}

public enum QuestionDifficulty
{
    Easy,
    Medium,
    Hard
}

public enum BloomLevel
{
    Remember,
    Understand,
    Apply,
    Analyze
}

public enum QuestionStatus
{
    Draft,
    PendingReview,
    Approved,
    Rejected,
    Archived
}

public enum QuestionType
{
    Main,
    FollowUp
}

public enum MaterialFileType
{
    PDF,
    DOCX,
    PPTX
}

public static class ExamSessionRules
{
    public static bool CanEdit(ExamSessionStatus status) =>
        status is ExamSessionStatus.Draft or ExamSessionStatus.Scheduled;

    public static bool CanTransition(ExamSessionStatus from, ExamSessionStatus to) => (from, to) switch
    {
        (ExamSessionStatus.Draft, ExamSessionStatus.Scheduled or ExamSessionStatus.Cancelled) => true,
        (ExamSessionStatus.Scheduled, ExamSessionStatus.InProgress or ExamSessionStatus.Cancelled) => true,
        (ExamSessionStatus.InProgress, ExamSessionStatus.Completed or ExamSessionStatus.Cancelled) => true,
        _ => false
    };

    public static bool BlocksCancellation(CandidateStatus status) => status == CandidateStatus.InProgress;

    public static CandidateStatus StatusAfterCancellation(CandidateStatus status) =>
        status == CandidateStatus.Waiting ? CandidateStatus.Cancelled : status;

    public static bool CanSit(ExamSessionStatus status) =>
        status is ExamSessionStatus.Scheduled or ExamSessionStatus.InProgress;

    public static bool IsSlotOpen(DateTime now, DateTime scheduledTime, int minutes) =>
        IsSlotOpen(now, scheduledTime, scheduledTime.AddMinutes(minutes));

    public static bool IsSlotOpen(DateTime now, DateTime scheduledTime, DateTime endTime) =>
        now >= scheduledTime && now <= endTime;

    public static readonly TimeSpan SubmitGrace = TimeSpan.FromMinutes(2);

    public static bool CanSubmit(DateTime now, DateTime scheduledTime, DateTime endTime) =>
        now >= scheduledTime && now <= endTime + SubmitGrace;
}

public static class QuestionRules
{
    public static bool IsSelectable(QuestionStatus status) => status == QuestionStatus.Approved;
}
