using Xunit;
using Domain = AssignmentPRN.Domain;
using Business = AssignmentPRN.Business;
using Data = AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.Tests;

public class SharedExamSessionRulesTests
{
    [Fact]
    public void Enum_names_and_values_match_across_facades()
    {
        Assert.Equal(Enum.GetNames<Domain.ExamSessionStatus>(), Enum.GetNames<Business.ExamSessionStatus>());
        Assert.Equal(Enum.GetNames<Domain.ExamSessionStatus>(), Enum.GetNames<Data.ExamSessionStatus>());
        Assert.Equal(Enum.GetNames<Domain.CandidateStatus>(), Enum.GetNames<Business.CandidateStatus>());
        Assert.Equal(Enum.GetNames<Domain.CandidateStatus>(), Enum.GetNames<Data.CandidateStatus>());
        foreach (var name in Enum.GetNames<Domain.ExamSessionStatus>())
        {
            Assert.Equal((int)Enum.Parse<Domain.ExamSessionStatus>(name), (int)Enum.Parse<Business.ExamSessionStatus>(name));
            Assert.Equal((int)Enum.Parse<Domain.ExamSessionStatus>(name), (int)Enum.Parse<Data.ExamSessionStatus>(name));
        }
        foreach (var name in Enum.GetNames<Domain.CandidateStatus>())
        {
            Assert.Equal((int)Enum.Parse<Domain.CandidateStatus>(name), (int)Enum.Parse<Business.CandidateStatus>(name));
            Assert.Equal((int)Enum.Parse<Domain.CandidateStatus>(name), (int)Enum.Parse<Data.CandidateStatus>(name));
        }
    }

    [Fact]
    public void Both_layers_use_the_same_transition_and_cancellation_policies()
    {
        foreach (var from in Enum.GetValues<Domain.ExamSessionStatus>())
        {
            Assert.Equal(Domain.ExamSessionRules.CanEdit(from), Business.ExamSessionRules.CanEdit((Business.ExamSessionStatus)from));
            Assert.Equal(Domain.ExamSessionRules.CanEdit(from), Data.ExamSessionRules.CanEdit((Data.ExamSessionStatus)from));
            Assert.Equal(Domain.ExamSessionRules.CanSit(from), Business.ExamSessionRules.CanSit((Business.ExamSessionStatus)from));
            Assert.Equal(Domain.ExamSessionRules.CanSit(from), Data.ExamSessionRules.CanSit((Data.ExamSessionStatus)from));
            foreach (var to in Enum.GetValues<Domain.ExamSessionStatus>())
            {
                Assert.Equal(Domain.ExamSessionRules.CanTransition(from, to), Business.ExamSessionRules.CanTransition((Business.ExamSessionStatus)from, (Business.ExamSessionStatus)to));
                Assert.Equal(Domain.ExamSessionRules.CanTransition(from, to), Data.ExamSessionRules.CanTransition((Data.ExamSessionStatus)from, (Data.ExamSessionStatus)to));
            }
        }
        foreach (var status in Enum.GetValues<Domain.CandidateStatus>())
        {
            Assert.Equal(Domain.ExamSessionRules.BlocksCancellation(status), Business.ExamSessionRules.BlocksCancellation((Business.CandidateStatus)status));
            Assert.Equal(Domain.ExamSessionRules.BlocksCancellation(status), Data.ExamSessionRules.BlocksCancellation((Data.CandidateStatus)status));
            Assert.Equal((int)Domain.ExamSessionRules.StatusAfterCancellation(status), (int)Business.ExamSessionRules.StatusAfterCancellation((Business.CandidateStatus)status));
            Assert.Equal((int)Domain.ExamSessionRules.StatusAfterCancellation(status), (int)Data.ExamSessionRules.StatusAfterCancellation((Data.CandidateStatus)status));
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
        Assert.Equal(canSit, Domain.ExamSessionRules.IsSlotOpen(now, start, 10));
        Assert.Equal(canSit, Business.ExamSessionRules.IsSlotOpen(now, start, end));
        Assert.Equal(canSit, Data.ExamSessionRules.IsSlotOpen(now, start, 10));
        Assert.Equal(canSubmit, Domain.ExamSessionRules.CanSubmit(now, start, end));
        Assert.Equal(canSubmit, Business.ExamSessionRules.CanSubmit(now, start, end));
        Assert.Equal(canSubmit, Data.ExamSessionRules.CanSubmit(now, start, end));
    }
}
