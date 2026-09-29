using AssignmentPRN.Business;
using AssignmentPRN.DataAccess.Contracts;

namespace AssignmentPRN.Tests;

public class ExamResultResponseTests
{
    [Fact]
    public void Summary_SeparatesCorrectIncorrectAndUnansweredAnswers()
    {
        var result = new ExamResultResponse
        {
            Questions =
            [
                Question(selectedOptionId: 1, correctOptionId: 1),
                Question(selectedOptionId: 2, correctOptionId: 1),
                Question(selectedOptionId: null, correctOptionId: 1)
            ]
        };

        Assert.Equal(3, result.TotalQuestions);
        Assert.Equal(1, result.CorrectCount);
        Assert.Equal(1, result.IncorrectCount);
        Assert.Equal(1, result.UnansweredCount);
        Assert.Equal(3.3m, result.Score);
    }

    [Fact]
    public void Score_RoundsHalfAwayFromZeroToOneDecimalPlace()
    {
        var result = new ExamResultResponse
        {
            Questions =
            [
                Question(1, 1),
                Question(1, 1),
                Question(2, 1),
                Question(2, 1)
            ]
        };

        Assert.Equal(5.0m, result.Score);
    }

    private static ExamResultQuestion Question(int? selectedOptionId, int correctOptionId) => new()
    {
        SelectedOptionId = selectedOptionId,
        Options =
        [
            new ExamResultOption { OptionId = 1, Text = "A", IsCorrect = correctOptionId == 1 },
            new ExamResultOption { OptionId = 2, Text = "B", IsCorrect = correctOptionId == 2 }
        ]
    };
}
