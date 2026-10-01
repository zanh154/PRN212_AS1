using AssignmentPRN.DataAccess;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using B = AssignmentPRN.Business;

namespace AssignmentPRN.Tests;

public class ExamDraftTests
{
    private static B.QuestionService Service(AivesDbContext db) => new(new QuestionRepository(db), new CatalogRepository(db), null!);

    [Fact]
    public async Task Draft_survives_reload_without_finishing_exam_and_can_be_changed()
    {
        await using var db = await SeedAsync();
        var id = (await db.ExamQuestions.SingleAsync()).ExamQuestionId;
        Assert.True((await Service(db).SaveDraftAsync(1, 1, new Dictionary<int, int?> { [id] = 1 })).Success);
        db.ChangeTracker.Clear();
        var room = (await Service(db).GetExamRoomAsync(1, 1)).Data!;
        Assert.Equal(1, Assert.Single(room.Questions).SelectedOptionId);
        Assert.Equal(B.CandidateStatus.InProgress, room.CandidateStatus);
        Assert.Null((await db.Answers.SingleAsync()).FinishedAt);
        Assert.False((await db.ExamQuestions.SingleAsync()).IsCompleted);
        Assert.True((await Service(db).SaveDraftAsync(1, 1, new Dictionary<int, int?> { [id] = 2 })).Success);
        Assert.Equal(2, (await db.Answers.SingleAsync()).SelectedOptionId);
        Assert.Equal(1, await db.Answers.CountAsync());
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("expired")]
    [InlineData("before")]
    [InlineData("completed")]
    [InlineData("cancelled")]
    [InlineData("question")]
    [InlineData("option")]
    public async Task Invalid_drafts_are_rejected(string scenario)
    {
        await using var db = await SeedAsync();
        var candidate = await db.ExamCandidates.SingleAsync();
        if (scenario == "expired") candidate.ScheduledTime = DateTime.Now.AddHours(-1);
        if (scenario == "before") candidate.ScheduledTime = DateTime.Now.AddHours(1);
        if (scenario == "completed") candidate.Status = CandidateStatus.Completed;
        if (scenario == "cancelled") (await db.ExamSessions.SingleAsync()).Status = ExamSessionStatus.Cancelled;
        await db.SaveChangesAsync();
        var id = (await db.ExamQuestions.SingleAsync()).ExamQuestionId;
        var result = await Service(db).SaveDraftAsync(1, scenario == "owner" ? 2 : 1,
            new Dictionary<int, int?> { [scenario == "question" ? 999 : id] = scenario == "option" ? 999 : 1 });
        Assert.False(result.Success);
        Assert.Empty(await db.Answers.ToListAsync());
    }

    [Fact]
    public async Task Submit_overrides_draft_and_late_autosave_cannot_change_result()
    {
        await using var db = await SeedAsync();
        var id = (await db.ExamQuestions.SingleAsync()).ExamQuestionId;
        Assert.True((await Service(db).SaveDraftAsync(1, 1, new Dictionary<int, int?> { [id] = 1 })).Success);
        var result = await Service(db).SubmitExamAsync(1, 1, new Dictionary<int, int?> { [id] = 2 });
        Assert.True(result.Success, result.Error);
        Assert.Equal(B.CandidateStatus.Completed, result.Data!.CandidateStatus);
        Assert.False((await Service(db).SaveDraftAsync(1, 1, new Dictionary<int, int?> { [id] = 1 })).Success);
        Assert.Equal(2, (await db.Answers.SingleAsync()).SelectedOptionId);
        Assert.NotNull((await db.Answers.SingleAsync()).FinishedAt);
    }

    [Fact]
    public async Task Follow_up_draft_cannot_change_submitted_main_round()
    {
        await using var db = await SeedAsync();
        var main = await db.ExamQuestions.SingleAsync();
        db.ExamQuestions.Add(new ExamQuestion { ExamId = 1, CandidateId = 1, QuestionId = 1,
            ParentExamQuestionId = main.ExamQuestionId, OrderNo = 2 });
        await db.SaveChangesAsync();
        Assert.False((await Service(db).SaveDraftAsync(1, 1, new Dictionary<int, int?> { [main.ExamQuestionId] = 1 })).Success);
        var followup = await db.ExamQuestions.SingleAsync(x => x.ParentExamQuestionId != null);
        Assert.True((await Service(db).SaveDraftAsync(1, 1, new Dictionary<int, int?> { [followup.ExamQuestionId] = 1 })).Success);
    }

    private static async Task<AivesDbContext> SeedAsync()
    {
        var db = await IssuedPaperEditTests.OpenAsync();
        var candidate = await db.ExamCandidates.SingleAsync();
        candidate.ScheduledTime = DateTime.Now.AddMinutes(-1);
        candidate.Status = CandidateStatus.InProgress;
        candidate.StartedAt = DateTime.Now.AddMinutes(-1);
        db.QuestionOptions.AddRange(new QuestionOption { OptionId = 1, QuestionId = 1, OptionText = "A", DisplayOrder = 1, IsCorrect = true },
            new QuestionOption { OptionId = 2, QuestionId = 1, OptionText = "B", DisplayOrder = 2 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }
}
