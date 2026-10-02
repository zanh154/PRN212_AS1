using AssignmentPRN.Business.Interfaces;
namespace AssignmentPRN.Business.BusinessRules;

/// <summary>
/// How a paper is marked out of 10. A follow-up counts for half a main question: it is
/// asked only because of a main answer, so it adjusts the grade rather than dominating it.
/// </summary>
public static class ExamScoring
{
    public const decimal MaxScore = 10m;

    public const decimal MainWeight = 1m;

    public const decimal FollowUpWeight = 0.5m;

    public static decimal Weight(bool isFollowUp) => isFollowUp ? FollowUpWeight : MainWeight;

    /// <summary>Weighted share of correct answers, scaled to 10 and rounded to one decimal.</summary>
    public static decimal Score(IEnumerable<(bool IsFollowUp, bool IsCorrect)> answers)
    {
        ArgumentNullException.ThrowIfNull(answers);

        decimal total = 0;
        decimal earned = 0;
        foreach (var (isFollowUp, isCorrect) in answers)
        {
            var weight = Weight(isFollowUp);
            total += weight;
            if (isCorrect)
            {
                earned += weight;
            }
        }

        return total == 0
            ? 0
            : Math.Round(earned * MaxScore / total, 1, MidpointRounding.AwayFromZero);
    }
}
