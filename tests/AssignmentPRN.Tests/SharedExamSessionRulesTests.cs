using Xunit;
using Biz = AssignmentPRN.Business.Interfaces;
using BRules = AssignmentPRN.Business.BusinessRules;
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
        Assert.Equal(Enum.GetNames<Data.ExamSessionStatus>(), Enum.GetNames<Biz.ExamSessionStatus>());
        Assert.Equal(Enum.GetNames<Data.CandidateStatus>(), Enum.GetNames<Biz.CandidateStatus>());
        foreach (var name in Enum.GetNames<Data.ExamSessionStatus>())
            Assert.Equal((int)Enum.Parse<Data.ExamSessionStatus>(name), (int)Enum.Parse<Biz.ExamSessionStatus>(name));
        foreach (var name in Enum.GetNames<Data.CandidateStatus>())
            Assert.Equal((int)Enum.Parse<Data.CandidateStatus>(name), (int)Enum.Parse<Biz.CandidateStatus>(name));
    }

    [Fact]
    public void Both_layers_use_the_same_transition_and_cancellation_policies()
    {
        foreach (var from in Enum.GetValues<Data.ExamSessionStatus>())
        {
            Assert.Equal(Data.ExamSessionRules.CanEdit(from), BRules.ExamSessionRules.CanEdit((Biz.ExamSessionStatus)from));
            Assert.Equal(Data.ExamSessionRules.CanSit(from), BRules.ExamSessionRules.CanSit((Biz.ExamSessionStatus)from));
            foreach (var to in Enum.GetValues<Data.ExamSessionStatus>())
                Assert.Equal(Data.ExamSessionRules.CanTransition(from, to), BRules.ExamSessionRules.CanTransition((Biz.ExamSessionStatus)from, (Biz.ExamSessionStatus)to));
        }
        foreach (var status in Enum.GetValues<Data.CandidateStatus>())
        {
            Assert.Equal(Data.ExamSessionRules.BlocksCancellation(status), BRules.ExamSessionRules.BlocksCancellation((Biz.CandidateStatus)status));
            Assert.Equal((int)Data.ExamSessionRules.StatusAfterCancellation(status), (int)BRules.ExamSessionRules.StatusAfterCancellation((Biz.CandidateStatus)status));
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
        Assert.Equal(canSit, BRules.ExamSessionRules.IsSlotOpen(now, start, 10));
        Assert.Equal(canSit, BRules.ExamSessionRules.IsSlotOpen(now, start, end));
        Assert.Equal(canSubmit, Data.ExamSessionRules.CanSubmit(now, start, end));
        Assert.Equal(canSubmit, BRules.ExamSessionRules.CanSubmit(now, start, end));
        Assert.Equal(Data.ExamSessionRules.SubmitGrace, BRules.ExamSessionRules.SubmitGrace);
    }
}
