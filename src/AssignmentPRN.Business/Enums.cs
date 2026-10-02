using Data = AssignmentPRN.DataAccess.Enums;

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

/// <summary>Compatibility facade; all policies live in Data.</summary>
public static class ExamSessionRules
{
    public static bool CanEdit(ExamSessionStatus status) =>
        Data.ExamSessionRules.CanEdit((Data.ExamSessionStatus)status);
    public static bool CanTransition(ExamSessionStatus from, ExamSessionStatus to) =>
        Data.ExamSessionRules.CanTransition((Data.ExamSessionStatus)from, (Data.ExamSessionStatus)to);
    public static bool BlocksCancellation(CandidateStatus status) =>
        Data.ExamSessionRules.BlocksCancellation((Data.CandidateStatus)status);
    public static CandidateStatus StatusAfterCancellation(CandidateStatus status) =>
        (CandidateStatus)Data.ExamSessionRules.StatusAfterCancellation((Data.CandidateStatus)status);
    public static bool CanSit(ExamSessionStatus status) =>
        Data.ExamSessionRules.CanSit((Data.ExamSessionStatus)status);
    public static bool IsSlotOpen(DateTime now, DateTime scheduledTime, int minutes) =>
        Data.ExamSessionRules.IsSlotOpen(now, scheduledTime, minutes);
    public static bool IsSlotOpen(DateTime now, DateTime scheduledTime, DateTime endTime) =>
        Data.ExamSessionRules.IsSlotOpen(now, scheduledTime, endTime);
    public static TimeSpan SubmitGrace => Data.ExamSessionRules.SubmitGrace;
    public static bool CanSubmit(DateTime now, DateTime scheduledTime, DateTime endTime) =>
        Data.ExamSessionRules.CanSubmit(now, scheduledTime, endTime);
}

public static class QuestionRules
{
    public static bool IsSelectable(QuestionStatus status) => status == QuestionStatus.Approved;
}
