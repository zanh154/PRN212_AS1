namespace AssignmentPRN.DataAccess.Enums;

public static class ExamSessionRules
{
    public static bool CanEdit(ExamSessionStatus status) => status is ExamSessionStatus.Draft or ExamSessionStatus.Scheduled;

    public static bool CanTransition(ExamSessionStatus from, ExamSessionStatus to) => (from, to) switch
    {
        (ExamSessionStatus.Draft, ExamSessionStatus.Scheduled or ExamSessionStatus.Cancelled) => true,
        (ExamSessionStatus.Scheduled, ExamSessionStatus.InProgress or ExamSessionStatus.Cancelled) => true,
        (ExamSessionStatus.InProgress, ExamSessionStatus.Completed or ExamSessionStatus.Cancelled) => true,
        _ => false
    };
}
