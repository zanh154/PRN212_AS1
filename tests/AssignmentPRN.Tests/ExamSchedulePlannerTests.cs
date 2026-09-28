using AssignmentPRN.Business;

namespace AssignmentPRN.Tests;

public sealed class ExamSchedulePlannerTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 8, 0, 0);

    [Fact]
    public void Generate_CreatesConsecutiveSlotsOfEqualLength()
    {
        var result = ExamSchedulePlanner.Generate(Start, [1, 2, 3], 20);

        Assert.Collection(result,
            first => Assert.Equal((Start, Start.AddMinutes(20)), (first.StartTime, first.EndTime)),
            second => Assert.Equal((Start.AddMinutes(20), Start.AddMinutes(40)), (second.StartTime, second.EndTime)),
            third => Assert.Equal((Start.AddMinutes(40), Start.AddMinutes(60)), (third.StartTime, third.EndTime)));
    }

    [Fact]
    public void Generate_KeepsTheOrderTheStudentsWereGivenIn()
    {
        var result = ExamSchedulePlanner.Generate(Start, [9, 4, 7], 15);

        Assert.Equal([9, 4, 7], result.Select(slot => slot.StudentId));
    }

    [Fact]
    public void Generate_RejectsEmptyStudentList()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            ExamSchedulePlanner.Generate(Start, [], 20));

        Assert.Contains("ít nhất một sinh viên", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_RejectsDuplicateStudent()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            ExamSchedulePlanner.Generate(Start, [7, 7], 30));

        Assert.Contains("một lần", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1441)]
    public void Generate_RejectsInvalidTimePerStudent(int duration)
    {
        Assert.Throws<ArgumentException>(() =>
            ExamSchedulePlanner.Generate(Start, [1], duration));
    }

    /// <summary>The message is shown to the user as-is, so it must not carry a parameter suffix.</summary>
    [Fact]
    public void Generate_ProducesUserFacingMessages()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            ExamSchedulePlanner.Generate(Start, [1], 0));

        Assert.DoesNotContain("Parameter", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Overlaps_ReturnsTrue_WhenWindowsIntersect()
    {
        Assert.True(ExamSchedulePlanner.Overlaps(
            Start,
            Start.AddMinutes(30),
            Start.AddMinutes(20),
            Start.AddMinutes(45)));
    }

    [Fact]
    public void Overlaps_ReturnsFalse_WhenWindowsTouchAtBoundary()
    {
        Assert.False(ExamSchedulePlanner.Overlaps(
            Start,
            Start.AddMinutes(30),
            Start.AddMinutes(30),
            Start.AddMinutes(60)));
    }
}
