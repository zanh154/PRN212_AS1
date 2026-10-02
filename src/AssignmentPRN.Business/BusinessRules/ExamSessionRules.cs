using AssignmentPRN.Business.Interfaces;
using Data = AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.Business.BusinessRules;

/// <summary>Facade over the shared policy that lives in the data access layer.</summary>
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
