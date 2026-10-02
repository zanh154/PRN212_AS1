using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssignmentPRN.Tests;

public class ExamSessionLifecycleTests
{
    [Fact]
    public async Task First_entry_starts_session_and_repeat_entry_keeps_timestamp()
    {
        await using var db = await SeedAsync();
        var repo = new QuestionRepository(db);
        var now = DateTime.Now;
        await repo.StartCandidateAsync(1, now);
        await repo.StartCandidateAsync(1, now.AddSeconds(1));
        Assert.Equal(ExamSessionStatus.InProgress, (await db.ExamSessions.SingleAsync()).Status);
        Assert.Equal(now, (await db.ExamCandidates.SingleAsync()).StartedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Last_submission_completes_session_but_waiting_student_keeps_it_open(bool anotherWaiting)
    {
        await using var db = await SeedAsync();
        if (anotherWaiting) await AddWaitingAsync(db);
        var repo = new QuestionRepository(db);
        await repo.StartCandidateAsync(1, DateTime.Now);
        var id = (await db.ExamQuestions.SingleAsync()).ExamQuestionId;
        await repo.SubmitAnswersAsync(1, new Dictionary<int, int?> { [id] = null }, DateTime.Now, []);
        Assert.Equal(anotherWaiting ? ExamSessionStatus.InProgress : ExamSessionStatus.Completed,
            (await db.ExamSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Followup_round_keeps_session_in_progress()
    {
        await using var db = await SeedAsync();
        var repo = new QuestionRepository(db);
        await repo.StartCandidateAsync(1, DateTime.Now);
        var id = (await db.ExamQuestions.SingleAsync()).ExamQuestionId;
        await repo.SubmitAnswersAsync(1, new Dictionary<int, int?> { [id] = null }, DateTime.Now,
            [new AssignmentPRN.DataAccess.Contracts.ExamQuestionInput { CandidateId = 1, QuestionId = 1, ParentExamQuestionId = id, OrderNo = 2 }]);
        Assert.Equal(ExamSessionStatus.InProgress, (await db.ExamSessions.SingleAsync()).Status);
        Assert.Equal(CandidateStatus.InProgress, (await db.ExamCandidates.SingleAsync()).Status);
    }

    [Fact]
    public async Task Closing_all_absent_completes_scheduled_session()
    {
        await using var db = await SeedAsync();
        (await db.ExamCandidates.SingleAsync()).ScheduledTime = DateTime.Now.AddHours(-1);
        await db.SaveChangesAsync();
        await new ExamResultRepository(db).UpdateCandidateStatusesAsync(new Dictionary<int, CandidateStatus> { [1] = CandidateStatus.Absent }, DateTime.Now);
        Assert.Equal(CandidateStatus.Absent, (await db.ExamCandidates.SingleAsync()).Status);
        Assert.Equal(ExamSessionStatus.Completed, (await db.ExamSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Overdue_recheck_preserves_completed_student_and_unexpired_waiting_student()
    {
        await using var db = await SeedAsync();
        (await db.ExamCandidates.SingleAsync()).Status = CandidateStatus.Completed;
        await AddWaitingAsync(db);
        await new ExamResultRepository(db).UpdateCandidateStatusesAsync(new Dictionary<int, CandidateStatus> {
            [1] = CandidateStatus.Absent, [2] = CandidateStatus.Absent }, DateTime.Now);
        Assert.Equal(CandidateStatus.Completed, (await db.ExamCandidates.SingleAsync(x => x.CandidateId == 1)).Status);
        Assert.Equal(CandidateStatus.Waiting, (await db.ExamCandidates.SingleAsync(x => x.CandidateId == 2)).Status);
        Assert.Equal(ExamSessionStatus.InProgress, (await db.ExamSessions.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(ExamSessionStatus.Cancelled)]
    [InlineData(ExamSessionStatus.Completed)]
    [InlineData(ExamSessionStatus.Draft)]
    public async Task Closed_or_draft_session_cannot_be_started_by_candidate(ExamSessionStatus status)
    {
        await using var db = await SeedAsync();
        (await db.ExamSessions.SingleAsync()).Status = status;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => new QuestionRepository(db).StartCandidateAsync(1, DateTime.Now));
        Assert.Equal(status, (await db.ExamSessions.SingleAsync()).Status);
    }

    private static async Task AddWaitingAsync(AivesDbContext db)
    {
        db.Users.Add(new User { UserId = 2, RoleId = 1, FullName = "Waiting", Email = "waiting@example.test", PasswordHash = "x", Status = "Active" });
        db.ExamCandidates.Add(new ExamCandidate { CandidateId = 2, ExamId = 1, StudentId = 2,
            ScheduledTime = DateTime.Now.AddHours(1), Status = CandidateStatus.Waiting });
        await db.SaveChangesAsync();
    }

    private static async Task<AivesDbContext> SeedAsync()
    {
        var db = await IssuedPaperEditTests.OpenAsync();
        var now = DateTime.Now.AddMinutes(-1);
        (await db.ExamSessions.SingleAsync()).StartTime = now;
        (await db.ExamCandidates.SingleAsync()).ScheduledTime = now;
        await db.SaveChangesAsync();
        return db;
    }
}
