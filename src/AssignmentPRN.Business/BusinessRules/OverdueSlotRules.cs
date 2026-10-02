using AssignmentPRN.Business.Interfaces;

namespace AssignmentPRN.Business.BusinessRules;

/// <summary>A slot as the overdue check needs to see it.</summary>
public sealed record SlotState(int CandidateId, CandidateStatus Status, DateTime? ScheduledTime);

/// <summary>
/// Which slots of a session the examiner may close once their time is over, kept free of
/// any database call so it can be tested on its own:
/// <list type="bullet">
/// <item>a student still Waiting when their slot ended never showed up: Absent;</item>
/// <item>a student still In progress after the slot and its submit grace left without
/// handing in: Completed, marked on what was saved (nothing saved scores zero).</item>
/// </list>
/// Without this a session could never be completed, since completing requires that no
/// student is left Waiting or In progress.
/// </summary>
public static class OverdueSlotRules
{
    public static IReadOnlyDictionary<int, CandidateStatus> Close(
        IEnumerable<SlotState> slots,
        int minutesPerStudent,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(slots);

        var result = new Dictionary<int, CandidateStatus>();
        foreach (var slot in slots)
        {
            var next = ExamLifecycleRules.CloseOverdue(slot.Status,
                slot.ScheduledTime, minutesPerStudent, now);
            if (next.HasValue) result[slot.CandidateId] = (CandidateStatus)next.Value;
        }

        return result;
    }
}
