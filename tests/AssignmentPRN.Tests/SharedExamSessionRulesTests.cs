using Xunit;
using Business = AssignmentPRN.Business;
using Data = AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.Tests;

/// <summary>
/// The data access layer owns the policy; the business layer only re-exposes it over its
/// own enums. These tests pin the two to each other so the facade cannot drift.
/// </summary>
public class SharedExamSessionRulesTests
{
    [Fact]
    public void Enum_names_and_values_match_across_facades()
    {
        Assert.Equal(Enum.GetNames<Data.ExamSessionStatus>(), Enum.GetNames<Business.ExamSessionStatus>());
        Assert.Equal(Enum.GetNames<Data.CandidateStatus>(), Enum.GetNames<Business.CandidateStatus>());
        foreach (var name in Enum.GetNames<Data.ExamSessionStatus>())
            Assert.Equal((int)Enum.Parse<Data.ExamSessionStatus>(name), (int)Enum.Parse<Business.ExamSessionStatus>(name));
        foreach (var name in Enum.GetNames<Data.CandidateStatus>())
            Assert.Equal((int)Enum.Parse<Data.CandidateStatus>(name), (int)Enum.Parse<Business.CandidateStatus>(name));
    }

    [Fact]
    public void Both_layers_use_the_same_transition_and_cancellation_policies()
    {
        foreach (var from in Enum.GetValues<Data.ExamSessionStatus>())
        {
            Assert.Equal(Data.ExamSessionRules.CanEdit(from), Business.ExamSessionRules.CanEdit((Business.ExamSessionStatus)from));
            Assert.Equal(Data.ExamSessionRules.CanSit(from), Business.ExamSessionRules.CanSit((Business.ExamSessionStatus)from));
            foreach (var to in Enum.GetValues<Data.ExamSessionStatus>())
                Assert.Equal(Data.ExamSessionRules.CanTransition(from, to), Business.ExamSessionRules.CanTransition((Business.ExamSessionStatus)from, (Business.ExamSessionStatus)to));
        }
        foreach (var status in Enum.GetValues<Data.CandidateStatus>())
        {
            Assert.Equal(Data.ExamSessionRules.BlocksCancellation(status), Business.ExamSessionRules.BlocksCancellation((Business.CandidateStatus)status));
            Assert.Equal((int)Data.ExamSessionRules.StatusAfterCancellation(status), (int)Business.ExamSessionRules.StatusAfterCancellation((Business.CandidateStatus)status));
        }
    }

    [Theory]
    [InlineData(-1, false, false)]
    [InlineData(0, true, true)]
    [InlineData(600, true, true)]
    [InlineData(601, false, true)]
    [InlineData(720, false, true)]
    [InlineData(721, false, false)]
    public void Slot_and_submission_boundaries_are_preserved(int seconds, bool canSit, bool canSubmit)
    {
        var start = new DateTime(2030, 1, 1, 8, 0, 0);
        var now = start.AddSeconds(seconds);
        var end = start.AddMinutes(10);
        Assert.Equal(canSit, Data.ExamSessionRules.IsSlotOpen(now, start, 10));
        Assert.Equal(canSit, Business.ExamSessionRules.IsSlotOpen(now, start, 10));
        Assert.Equal(canSit, Business.ExamSessionRules.IsSlotOpen(now, start, end));
        Assert.Equal(canSubmit, Data.ExamSessionRules.CanSubmit(now, start, end));
        Assert.Equal(canSubmit, Business.ExamSessionRules.CanSubmit(now, start, end));
        Assert.Equal(Data.ExamSessionRules.SubmitGrace, Business.ExamSessionRules.SubmitGrace);
    }
}
