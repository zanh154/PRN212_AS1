using AssignmentPRN.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

/// <summary>Called inside the transaction that changes candidate states.</summary>
internal static class ExamSessionLifecycle
{
    internal static async Task SynchronizeAsync(AivesDbContext context, int examId, DateTime now, CancellationToken ct)
    {
        var session = await context.ExamSessions.SingleAsync(x => x.ExamId == examId, ct);
        if (!ExamSessionRules.CanSit(session.Status)) return;
        var statuses = await context.ExamCandidates.Where(x => x.ExamId == examId).Select(x => x.Status).ToListAsync(ct);
        if (statuses.Count == 0) return;
        var next = statuses.All(x => x is CandidateStatus.Completed or CandidateStatus.Absent or CandidateStatus.Cancelled)
            ? ExamSessionStatus.Completed
            : statuses.Any(x => x is CandidateStatus.InProgress or CandidateStatus.Completed)
                ? ExamSessionStatus.InProgress : session.Status;
        if (next == session.Status) return;
        session.Status = next;
        session.UpdatedAt = now;
        await context.SaveChangesAsync(ct);
    }
}
