using AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.Business;

/// <summary>A main question the student has just answered, as the planner needs to see it.</summary>
public sealed record FollowUpSource(
    int ExamQuestionId,
    int OrderNo,
    int? MaterialId,
    QuestionDifficulty Difficulty,
    bool IsCorrect);

/// <summary>A follow-up question in the bank that could be asked.</summary>
public sealed record FollowUpCandidate(int QuestionId, int MaterialId, QuestionDifficulty Difficulty);

/// <summary>One follow-up to deal: which bank question, digging into which main slot.</summary>
public sealed record FollowUpPick(int ParentExamQuestionId, int QuestionId);

/// <summary>
/// Decides the second round of a paper from how the first one went, kept free of any
/// database call so the rules can be tested on their own:
/// <list type="bullet">
/// <item>a follow-up stays on the topic of the main question it digs into;</item>
/// <item>a wrong or blank answer gets an easier (or equal) question, to check the basics;</item>
/// <item>a right answer gets a harder (or equal) question, to see how deep it goes;</item>
/// <item>wrong answers are served first, and no more than the session's maximum overall;</item>
/// <item>each main question gets at most one follow-up, never the same question twice.</item>
/// </list>
/// </summary>
public static class FollowUpPlanner
{
    /// <summary>
    /// Least time that must be left in the slot to open a second round. Less than this and
    /// the student could not read the questions, so the paper simply closes.
    /// </summary>
    public static readonly TimeSpan MinimumTimeLeft = TimeSpan.FromMinutes(1);

    public static bool CanOpenRound(DateTime now, DateTime slotEnd) => slotEnd - now >= MinimumTimeLeft;

    /// <param name="answered">The main questions of the paper with how they were answered.</param>
    /// <param name="pool">Approved follow-up questions of the course.</param>
    /// <param name="maxCount">The session's cap on follow-ups per student.</param>
    /// <param name="usedInSession">
    /// Follow-ups other students of the session already got. They are only avoided, not
    /// banned, so a small bank still serves everybody.
    /// </param>
    public static IReadOnlyList<FollowUpPick> Plan(
        IReadOnlyList<FollowUpSource> answered,
        IReadOnlyList<FollowUpCandidate> pool,
        int maxCount,
        IReadOnlyCollection<int>? usedInSession = null,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(answered);
        ArgumentNullException.ThrowIfNull(pool);

        if (maxCount <= 0 || answered.Count == 0 || pool.Count == 0)
        {
            return Array.Empty<FollowUpPick>();
        }

        var used = usedInSession is null ? new HashSet<int>() : new HashSet<int>(usedInSession);
        var source = random ?? Random.Shared;
        var picked = new HashSet<int>();
        var picks = new List<FollowUpPick>(maxCount);

        var queue = answered
            .OrderBy(item => item.IsCorrect)
            .ThenBy(item => item.OrderNo);

        foreach (var main in queue)
        {
            if (picks.Count == maxCount)
            {
                break;
            }

            if (main.MaterialId is not int materialId)
            {
                continue;
            }

            var questionId = PickFor(main, materialId, pool, picked, used, source);
            if (questionId is int id)
            {
                picked.Add(id);
                picks.Add(new FollowUpPick(main.ExamQuestionId, id));
            }
        }

        return picks;
    }

    /// <summary>
    /// The difficulties to try for one main question, nearest first. A wrong answer steps
    /// down towards Easy, a right one steps up towards Hard; the same level comes last.
    /// </summary>
    public static IReadOnlyList<QuestionDifficulty> DifficultyOrder(QuestionDifficulty difficulty, bool isCorrect)
    {
        var level = (int)difficulty;
        var order = new List<QuestionDifficulty>();

        if (isCorrect)
        {
            for (var next = level + 1; next <= (int)QuestionDifficulty.Hard; next++)
            {
                order.Add((QuestionDifficulty)next);
            }
        }
        else
        {
            for (var next = level - 1; next >= (int)QuestionDifficulty.Easy; next--)
            {
                order.Add((QuestionDifficulty)next);
            }
        }

        order.Add(difficulty);
        return order;
    }

    private static int? PickFor(
        FollowUpSource main,
        int materialId,
        IReadOnlyList<FollowUpCandidate> pool,
        HashSet<int> picked,
        HashSet<int> used,
        Random source)
    {
        var onTopic = pool
            .Where(item => item.MaterialId == materialId && !picked.Contains(item.QuestionId))
            .ToList();

        foreach (var difficulty in DifficultyOrder(main.Difficulty, main.IsCorrect))
        {
            var level = onTopic.Where(item => item.Difficulty == difficulty).ToList();
            if (level.Count == 0)
            {
                continue;
            }

            // Fresh questions first, so two students of one session rarely share a follow-up.
            var fresh = level.Where(item => !used.Contains(item.QuestionId)).ToList();
            var choices = fresh.Count > 0 ? fresh : level;
            return choices[source.Next(choices.Count)].QuestionId;
        }

        return null;
    }
}
