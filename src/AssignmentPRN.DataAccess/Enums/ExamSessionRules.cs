namespace AssignmentPRN.DataAccess.Enums;

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
