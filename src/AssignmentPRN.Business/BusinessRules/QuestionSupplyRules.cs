using AssignmentPRN.Business.Interfaces;
namespace AssignmentPRN.Business.BusinessRules;

/// <summary>
/// Whether the question bank can cover a whole session. Every student gets their own main
/// questions and no question is dealt twice inside one session, so a session needs
/// students × main questions distinct questions. Kept free of any database call so it can
/// be tested on its own.
/// </summary>
public static class QuestionSupplyRules
{
    public static int Required(int candidateCount, int questionsPerCandidate) =>
        Math.Max(candidateCount, 0) * Math.Max(questionsPerCandidate, 0);

    /// <summary>Refuses a session the bank could not serve to the last student.</summary>
    public static void EnsureEnough(int candidateCount, int questionsPerCandidate, int available)
    {
        var required = Required(candidateCount, questionsPerCandidate);
        if (available >= required)
        {
            return;
        }

        throw new BusinessValidationException(
            $"Ngân hàng chỉ có {available} câu hỏi chính, không đủ {required} câu khác nhau "
            + $"cho {candidateCount} sinh viên × {questionsPerCandidate} câu. "
            + "Hãy bổ sung câu hỏi hoặc giảm số câu hỏi chính.");
    }
}
