using AssignmentPRN.Business;
using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Fixture = AssignmentPRN.Tests.ExamSessionRepositoryTests.RepositoryFixture;

namespace AssignmentPRN.Tests;

public class ModuleTwoTests
{
    private static DateTime Start => DateTime.Today.AddDays(10).AddHours(8);
    private static CourseService Courses(Fixture f) => new(new CourseRepository(f.Context), new CatalogRepository(f.Context));
    private static ExamSessionCreateRequest CreateRequest() => new() {
        ClassId = 100, CourseId = 1, LecturerId = 10, ExamName = "Phiên kiểm thử",
        StartTime = Start, TimePerStudent = 20, MainQuestionCount = 3, MaxFollowUpCount = 2
    };
    private static ExamSessionUpdateInput EditRequest(int id) => new() {
        ExamId = id, CourseId = 1, ExamName = "Đã sửa", StartTime = Start.AddHours(1),
        TimePerStudent = 30, MainQuestionCount = 4, MaxFollowUpCount = 1
    };

    [Fact]
    public async Task CourseCrud_NormalizesCode_UpdatesAndDeletesUnusedCourse()
    {
        await using var f = await Fixture.CreateAsync();
        var service = Courses(f);
        var created = await service.SaveAsync(new(0, " new101 ", "Môn mới", null, 10, true), null);
        Assert.True(created.Success, created.Error);
        Assert.Equal("NEW101", created.Data!.CourseCode);
        var id = created.Data.CourseId;
        var updated = await service.SaveAsync(new(id, "NEW101", "Tên đã sửa", "Mô tả", 10, false), null);
        Assert.True(updated.Success, updated.Error);
        Assert.False((await service.GetAsync(id, null)).Data!.IsActive);
        Assert.True((await service.DeleteAsync(id, null)).Success);
        Assert.Null(await f.Context.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.CourseId == id));
    }

    [Fact]
    public async Task CourseValidation_RejectsDuplicateCodeAndUsedCourseDeletion()
    {
        await using var f = await Fixture.CreateAsync();
        var service = Courses(f);
        Assert.False((await service.SaveAsync(new(0, "prn1", "Duplicate", null, 10, true), null)).Success);
        Assert.False((await service.DeleteAsync(1, null)).Success);
        Assert.NotNull(await f.Context.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.CourseId == 1));
    }

    [Fact]
    public async Task LecturerCannotEditOrDeleteAnotherLecturersCourse()
    {
        await using var f = await Fixture.CreateAsync();
        Assert.False((await Courses(f).SaveAsync(new(1, "PRN1", "Changed", null, 11, true), 11)).Success);
        Assert.False((await Courses(f).DeleteAsync(1, 11)).Success);
    }

    [Fact]
    public async Task UpdateSession_RebuildsSlotsAndPreservesCandidateIds()
    {
        await using var f = await Fixture.CreateAsync();
        var created = (await f.Service.CreateAsync(CreateRequest())).Data!;
        var updated = await f.Service.UpdateAsync(EditRequest(created.ExamId), 10);
        Assert.True(updated.Success, updated.Error);
        Assert.Equal(created.Candidates.Select(x => x.CandidateId), updated.Data!.Candidates.Select(x => x.CandidateId));
        Assert.Equal(Start.AddHours(1), updated.Data.Candidates[0].ScheduledTime);
        Assert.Equal(Start.AddMinutes(90), updated.Data.Candidates[1].ScheduledTime);
        Assert.Equal(Start.AddHours(2), updated.Data.EndTime);
        Assert.Equal("Đã sửa", updated.Data.ExamName);
    }

    [Fact]
    public async Task UpdateSession_RejectsConflictWithoutChangingStoredSchedule()
    {
        await using var f = await Fixture.CreateAsync();
        var created = (await f.Service.CreateAsync(CreateRequest())).Data!;
        await f.Repository.CreateAsync(new ExamSessionAggregateInput {
            CourseId = 2, LecturerId = 11, ExamName = "Lịch khác", StartTime = Start.AddHours(1),
            EndTime = Start.AddHours(2), TimePerStudent = 60, Status = ExamSessionStatus.Scheduled,
            Candidates = [new ExamCandidateInput { StudentId = 20, ScheduledTime = Start.AddHours(1), Status = CandidateStatus.Waiting }]
        });
        var result = await f.Service.UpdateAsync(EditRequest(created.ExamId), 10);
        Assert.False(result.Success);
        Assert.Contains("Trùng lịch", result.Error);
        var stored = (await f.Repository.GetDetailAsync(created.ExamId))!;
        Assert.Equal(created.ExamName, stored.ExamName);
        Assert.Equal(created.StartTime, stored.StartTime);
        Assert.Equal(created.Candidates.Select(x => x.ScheduledTime), stored.Candidates.Select(x => x.ScheduledTime));
    }

    [Fact]
    public async Task MetadataOnlyEdit_PreservesIndividuallyRescheduledSlots()
    {
        await using var f = await Fixture.CreateAsync();
        var created = (await f.Service.CreateAsync(CreateRequest())).Data!;
        var moved = await f.Repository.RescheduleAsync(created.Candidates[1].CandidateId, Start.AddHours(3));
        var input = EditRequest(created.ExamId);
        input.StartTime = moved.StartTime;
        input.TimePerStudent = moved.TimePerStudent;
        var result = await f.Service.UpdateAsync(input, 10);
        Assert.True(result.Success, result.Error);
        Assert.Equal(moved.Candidates.Select(x => x.ScheduledTime), result.Data!.Candidates.Select(x => x.ScheduledTime));
        Assert.Equal(moved.EndTime, result.Data.EndTime);
    }

    [Fact]
    public async Task InactiveCourse_IsRejectedForNewSessionsButExistingSessionCanKeepIt()
    {
        await using var f = await Fixture.CreateAsync();
        var created = (await f.Service.CreateAsync(CreateRequest())).Data!;
        Assert.True((await Courses(f).SaveAsync(new(1, "PRN1", "Môn 1", null, 10, false), null)).Success);
        Assert.False((await f.Service.CreateAsync(CreateRequest())).Success);
        Assert.True((await f.Service.UpdateAsync(EditRequest(created.ExamId), 10)).Success);
    }

    [Fact]
    public async Task DeleteUnusedScheduledSession_RemovesItsCandidates()
    {
        await using var f = await Fixture.CreateAsync();
        var created = (await f.Service.CreateAsync(CreateRequest())).Data!;
        Assert.True((await f.Service.DeleteAsync(created.ExamId)).Success);
        Assert.Null(await f.Repository.GetDetailAsync(created.ExamId));
        Assert.False(await f.Context.ExamCandidates.AnyAsync(x => x.ExamId == created.ExamId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public async Task UpdateSession_RejectsInvalidDuration(int duration)
    {
        await using var f = await Fixture.CreateAsync();
        var created = (await f.Service.CreateAsync(CreateRequest())).Data!;
        var input = EditRequest(created.ExamId);
        input.TimePerStudent = duration;
        Assert.False((await f.Service.UpdateAsync(input, 10)).Success);
    }

    [Fact]
    public async Task UpdateSession_RejectsPastTimeOvernightAndOtherLecturer()
    {
        await using var f = await Fixture.CreateAsync();
        var created = (await f.Service.CreateAsync(CreateRequest())).Data!;
        Assert.False((await f.Service.UpdateAsync(EditRequest(created.ExamId), 11)).Success);
        var input = EditRequest(created.ExamId);
        input.StartTime = DateTime.Today.AddDays(-1);
        Assert.False((await f.Service.UpdateAsync(input, 10)).Success);
        input.StartTime = Start.Date.AddHours(23).AddMinutes(30);
        Assert.False((await f.Service.UpdateAsync(input, 10)).Success);
    }

    [Fact]
    public async Task CancelledSession_CannotBeReopenedEditedOrDeleted()
    {
        await using var f = await Fixture.CreateAsync();
        var created = (await f.Service.CreateAsync(CreateRequest())).Data!;
        Assert.False((await f.Service.ChangeStatusAsync(created.ExamId, ExamSessionStatus.Cancelled, 11)).Success);
        Assert.True((await f.Service.ChangeStatusAsync(created.ExamId, ExamSessionStatus.Cancelled, 10)).Success);
        Assert.False((await f.Service.ChangeStatusAsync(created.ExamId, ExamSessionStatus.Scheduled, 10)).Success);
        Assert.False((await f.Service.UpdateAsync(EditRequest(created.ExamId), 10)).Success);
        Assert.False((await f.Service.DeleteAsync(created.ExamId)).Success);
        Assert.Empty((await f.Repository.GetStudentScheduleAsync(20))!.Items);
    }

    [Fact]
    public async Task Status_RejectsEarlyStartAndCompletionWithWaitingCandidates()
    {
        await using var f = await Fixture.CreateAsync();
        var created = (await f.Service.CreateAsync(CreateRequest())).Data!;
        Assert.False((await f.Service.ChangeStatusAsync(created.ExamId, ExamSessionStatus.InProgress, 10)).Success);
        var session = await f.Context.ExamSessions.FindAsync(created.ExamId);
        session!.StartTime = DateTime.Now.AddMinutes(-10);
        await f.Context.SaveChangesAsync();
        Assert.True((await f.Service.ChangeStatusAsync(created.ExamId, ExamSessionStatus.InProgress, 10)).Success);
        Assert.False((await f.Service.ChangeStatusAsync(created.ExamId, ExamSessionStatus.Completed, 10)).Success);
        foreach (var candidate in session.Candidates) candidate.Status = CandidateStatus.Completed;
        await f.Context.SaveChangesAsync();
        Assert.True((await f.Service.ChangeStatusAsync(created.ExamId, ExamSessionStatus.Completed, 10)).Success);
    }
}
