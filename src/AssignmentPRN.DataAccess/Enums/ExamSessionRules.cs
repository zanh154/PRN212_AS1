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

    /// <summary>
    /// How early a student may open their slot. A few minutes of slack keeps a clock that
    /// is slightly out, or a student who is simply ready, from being turned away.
    /// </summary>
    public static readonly TimeSpan EarlyEntry = TimeSpan.FromMinutes(15);

    /// <summary>A session whose slots a student may sit; a draft has not been published yet.</summary>
    public static bool CanSit(ExamSessionStatus status) =>
        status is ExamSessionStatus.Scheduled or ExamSessionStatus.InProgress;

    /// <summary>
    /// Whether <paramref name="now"/> falls inside the slot that starts at
    /// <paramref name="scheduledTime"/> and lasts <paramref name="minutes"/>.
    /// </summary>
    public static bool IsSlotOpen(DateTime now, DateTime scheduledTime, int minutes) =>
        IsSlotOpen(now, scheduledTime, scheduledTime.AddMinutes(minutes));

    /// <summary>Same window, for callers that already hold the end of the slot.</summary>
    public static bool IsSlotOpen(DateTime now, DateTime scheduledTime, DateTime endTime) =>
        now >= scheduledTime - EarlyEntry && now <= endTime;
}
