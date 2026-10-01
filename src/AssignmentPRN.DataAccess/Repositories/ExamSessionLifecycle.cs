using AssignmentPRN.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

/// <summary>Called inside the transaction that changes candidate states.</summary>
internal static class ExamSessionLifecycle
{
    internal static async Task SynchronizeAsync(AivesDbContext context, int examId, DateTime now, CancellationToken ct)
    {
        var session = await context.ExamSessions.SingleAsync(x => x.ExamId == examId, ct);
        var statuses = await context.ExamCandidates.Where(x => x.ExamId == examId).Select(x => x.Status).ToListAsync(ct);
        var next = (ExamSessionStatus)Domain.ExamLifecycleRules.SessionAfterProgress(
            (Domain.ExamSessionStatus)session.Status, statuses.Select(x => (Domain.CandidateStatus)x));
        if (next == session.Status) return;
        session.Status = next;
        session.UpdatedAt = now;
        await context.SaveChangesAsync(ct);
    }
}
