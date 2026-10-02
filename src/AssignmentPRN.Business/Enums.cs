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

/// <summary>Compatibility facade; all policies live in AssignmentPRN.Domain.</summary>
public static class ExamSessionRules
{
    public static bool CanEdit(ExamSessionStatus status) =>
        AssignmentPRN.Domain.ExamSessionRules.CanEdit((AssignmentPRN.Domain.ExamSessionStatus)status);
    public static bool CanTransition(ExamSessionStatus from, ExamSessionStatus to) =>
        AssignmentPRN.Domain.ExamSessionRules.CanTransition((AssignmentPRN.Domain.ExamSessionStatus)from, (AssignmentPRN.Domain.ExamSessionStatus)to);
    public static bool BlocksCancellation(CandidateStatus status) =>
        AssignmentPRN.Domain.ExamSessionRules.BlocksCancellation((AssignmentPRN.Domain.CandidateStatus)status);
    public static CandidateStatus StatusAfterCancellation(CandidateStatus status) =>
        (CandidateStatus)AssignmentPRN.Domain.ExamSessionRules.StatusAfterCancellation((AssignmentPRN.Domain.CandidateStatus)status);
    public static bool CanSit(ExamSessionStatus status) =>
        AssignmentPRN.Domain.ExamSessionRules.CanSit((AssignmentPRN.Domain.ExamSessionStatus)status);
    public static bool IsSlotOpen(DateTime now, DateTime scheduledTime, int minutes) =>
        AssignmentPRN.Domain.ExamSessionRules.IsSlotOpen(now, scheduledTime, minutes);
    public static bool IsSlotOpen(DateTime now, DateTime scheduledTime, DateTime endTime) =>
        AssignmentPRN.Domain.ExamSessionRules.IsSlotOpen(now, scheduledTime, endTime);
    public static TimeSpan SubmitGrace => AssignmentPRN.Domain.ExamSessionRules.SubmitGrace;
    public static bool CanSubmit(DateTime now, DateTime scheduledTime, DateTime endTime) =>
        AssignmentPRN.Domain.ExamSessionRules.CanSubmit(now, scheduledTime, endTime);
}

public static class QuestionRules
{
    public static bool IsSelectable(QuestionStatus status) => status == QuestionStatus.Approved;
}
