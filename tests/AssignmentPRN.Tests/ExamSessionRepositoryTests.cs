using AssignmentPRN.DataAccess;
using AssignmentPRN.DataAccess.Common;
using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using AssignmentPRN.DataAccess.Repositories;
using AssignmentPRN.Business;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.Tests;

public sealed class ExamSessionRepositoryTests
{
    // Seeded ids: lecturers 10/11, students 20/21/22, course 1/2.
    private const int LecturerA = 10;
    private const int LecturerB = 11;
    private const int StudentA = 20;
    private const int StudentB = 21;

    private static readonly DateTime Start = new(2026, 10, 10, 8, 0, 0);

    [Fact]
    public async Task CreateAsync_RejectsOverlapForSameStudentAcrossSessions()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        await fixture.Repository.CreateAsync(Input("Ca thứ nhất", LecturerA, 1, Start, 30, StudentA));

        var error = await Assert.ThrowsAsync<ExamScheduleConflictException>(() =>
            fixture.Repository.CreateAsync(
                Input("Ca bị trùng", LecturerB, 2, Start.AddMinutes(10), 30, StudentA)));

        Assert.Contains("sinh viên", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_RejectsOverlapForSameLecturerAcrossSessions()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        await fixture.Repository.CreateAsync(Input("Ca thứ nhất", LecturerA, 1, Start, 30, StudentA));

        var error = await Assert.ThrowsAsync<ExamScheduleConflictException>(() =>
            fixture.Repository.CreateAsync(
                Input("Ca bị trùng", LecturerA, 2, Start.AddMinutes(10), 30, StudentB)));

        Assert.Contains("giảng viên", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_AllowsBackToBackSlotsThatOnlyTouch()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        await fixture.Repository.CreateAsync(Input("Ca thứ nhất", LecturerA, 1, Start, 30, StudentA));

        var second = await fixture.Repository.CreateAsync(
            Input("Ca kế tiếp", LecturerA, 1, Start.AddMinutes(30), 30, StudentB));

        Assert.Equal(Start.AddMinutes(30), Assert.Single(second.Candidates).ScheduledTime);
    }

    [Fact]
    public async Task CreateAsync_GeneratesOneSlotPerStudentInOrder()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();

        var session = await fixture.Repository.CreateAsync(
            Input("Hai ca liên tiếp", LecturerA, 1, Start, 30, StudentA, StudentB));

        Assert.Collection(session.Candidates,
            first => Assert.Equal(Start, first.ScheduledTime),
            second => Assert.Equal(Start.AddMinutes(30), second.ScheduledTime));
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateStudentWithinSession()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Repository.CreateAsync(
                Input("Danh sách bị trùng", LecturerA, 1, Start, 30, StudentA, StudentA)));

        Assert.Contains("bị trùng", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateFromClass_UsesEveryActiveStudentExactlyOnce()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();

        var response = await fixture.Service.CreateAsync(new ExamSessionCreateRequest
        {
            ClassId = 100,
            CourseId = 1,
            LecturerId = LecturerA,
            ExamName = "Thi theo lớp",
            StartTime = DateTime.Today.AddDays(10).AddHours(8),
            TimePerStudent = 20,
            MainQuestionCount = 3,
            MaxFollowUpCount = 2
        });

        Assert.True(response.Success, response.Error);
        Assert.Equal([StudentA, StudentB], response.Data!.Candidates.Select(item => item.StudentId));
    }

    [Fact]
    public async Task CreateFromClass_RejectsMismatchedLecturer()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();

        var response = await fixture.Service.CreateAsync(new ExamSessionCreateRequest
        {
            ClassId = 100,
            CourseId = 1,
            LecturerId = LecturerB,
            ExamName = "Sai giảng viên",
            StartTime = DateTime.Today.AddDays(10).AddHours(8),
            TimePerStudent = 20,
            MainQuestionCount = 3,
            MaxFollowUpCount = 2
        });

        Assert.False(response.Success);
        Assert.Contains("không thuộc giảng viên", response.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateFromClass_RejectsAnExistingStudentConflict()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var start = DateTime.Today.AddDays(10).AddHours(8);
        await fixture.Repository.CreateAsync(Input("Lịch có sẵn", LecturerB, 2, start, 30, StudentA));

        var response = await fixture.Service.CreateAsync(new ExamSessionCreateRequest
        {
            ClassId = 100,
            CourseId = 1,
            LecturerId = LecturerA,
            ExamName = "Thi theo lớp bị trùng",
            StartTime = start.AddMinutes(10),
            TimePerStudent = 20,
            MainQuestionCount = 3,
            MaxFollowUpCount = 2
        });

        Assert.False(response.Success);
        Assert.Contains("Trùng lịch với sinh viên", response.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RescheduleAsync_RejectsLecturerOverlapAndKeepsOriginalSlot()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var session = await fixture.Repository.CreateAsync(
            Input("Hai ca liên tiếp", LecturerA, 1, Start, 30, StudentA, StudentB));
        var second = session.Candidates.Single(item => item.StudentId == StudentB);

        await Assert.ThrowsAsync<ExamScheduleConflictException>(() =>
            fixture.Repository.RescheduleAsync(second.CandidateId, Start.AddMinutes(15)));

        var persisted = await fixture.Repository.GetDetailAsync(session.ExamId);
        Assert.Equal(
            Start.AddMinutes(30),
            persisted!.Candidates.Single(item => item.CandidateId == second.CandidateId).ScheduledTime);
    }

    [Fact]
    public async Task RescheduleAsync_RejectsSlotOnAnotherDay()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var session = await fixture.Repository.CreateAsync(Input("Ca duy nhất", LecturerA, 1, Start, 30, StudentA));
        var candidate = Assert.Single(session.Candidates);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Repository.RescheduleAsync(candidate.CandidateId, Start.AddDays(1)));
    }

    [Fact]
    public async Task RescheduleAsync_MovesTheSlotAndWidensTheSessionWindow()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var session = await fixture.Repository.CreateAsync(
            Input("Hai ca liên tiếp", LecturerA, 1, Start, 30, StudentA, StudentB));
        var second = session.Candidates.Single(item => item.StudentId == StudentB);

        var moved = Start.AddHours(3);
        var updated = await fixture.Repository.RescheduleAsync(second.CandidateId, moved);

        Assert.Equal(moved, updated.Candidates.Single(item => item.CandidateId == second.CandidateId).ScheduledTime);
        Assert.Equal(moved.AddMinutes(30), updated.EndTime);
    }

    [Fact]
    public async Task GetStudentScheduleAsync_ReturnsOnlyThatStudentsSlots()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        await fixture.Repository.CreateAsync(
            Input("Hai ca liên tiếp", LecturerA, 1, Start, 30, StudentA, StudentB));

        var schedule = await fixture.Repository.GetStudentScheduleAsync(StudentA);

        Assert.NotNull(schedule);
        var item = Assert.Single(schedule.Items);
        Assert.Equal(Start, item.ScheduledTime);
        Assert.Equal(30, item.TimePerStudent);
    }

    [Fact]
    public async Task GetStudentScheduleAsync_ReturnsNullForUnknownAccount()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();

        Assert.Null(await fixture.Repository.GetStudentScheduleAsync(9999));
    }

    private static ExamSessionAggregateInput Input(
        string examName,
        int lecturerId,
        int courseId,
        DateTime start,
        int timePerStudent,
        params int[] studentIds) => new()
        {
            CourseId = courseId,
            LecturerId = lecturerId,
            ExamName = examName,
            StartTime = start,
            EndTime = start.AddMinutes(timePerStudent * studentIds.Length),
            TimePerStudent = timePerStudent,
            MainQuestionCount = 3,
            MaxFollowUpCount = 2,
            Status = ExamSessionStatus.Scheduled,
            Candidates = studentIds
                .Select((id, index) => new ExamCandidateInput
                {
                    StudentId = id,
                    ScheduledTime = start.AddMinutes(timePerStudent * index),
                    Status = CandidateStatus.Waiting
                })
                .ToList()
        };

    /// <summary>
    /// A throwaway database per test. SQLite runs in memory but still executes real SQL
    /// and supports the transactions the repository opens.
    /// </summary>
    internal sealed class RepositoryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly AivesDbContext _context;

        private RepositoryFixture(SqliteConnection connection, AivesDbContext context)
        {
            _connection = connection;
            _context = context;
            Repository = new ExamSessionRepository(context);
            Service = new ExamSessionService(Repository, new CatalogRepository(context));
        }

        public ExamSessionRepository Repository { get; }

        public ExamSessionService Service { get; }
        public AivesDbContext Context => _context;

        public static async Task<RepositoryFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AivesDbContext>().UseSqlite(connection).Options;
            var context = new AivesDbContext(options);
            await context.Database.EnsureCreatedAsync();

            context.Roles.AddRange(
                new Role { RoleId = 2, RoleName = "Lecturer" },
                new Role { RoleId = 3, RoleName = "Student" });

            context.Users.AddRange(
                NewUser(LecturerA, 2, "Giảng viên A"),
                NewUser(LecturerB, 2, "Giảng viên B"),
                NewUser(StudentA, 3, "Sinh viên A"),
                NewUser(StudentB, 3, "Sinh viên B"),
                NewUser(22, 3, "Sinh viên C"));

            context.Courses.AddRange(
                NewCourse(1, "PRN1", "Môn 1"),
                NewCourse(2, "PRN2", "Môn 2"));

            context.AcademicClasses.Add(new AcademicClass
            {
                ClassId = 100,
                ClassCode = "SE1801",
                ClassName = "Lớp SE1801",
                CourseId = 1,
                LecturerId = LecturerA,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1),
                Students =
                [
                    new ClassStudent { StudentId = StudentA, JoinedAt = new DateTime(2026, 1, 1) },
                    new ClassStudent { StudentId = StudentB, JoinedAt = new DateTime(2026, 1, 1) }
                ]
            });

            await context.SaveChangesAsync();
            return new RepositoryFixture(connection, context);
        }

        private static User NewUser(int id, int roleId, string name) => new()
        {
            UserId = id,
            RoleId = roleId,
            FullName = name,
            Email = $"user{id}@example.test",
            PasswordHash = "x",
            Status = "Active",
            CreatedAt = new DateTime(2026, 1, 1)
        };

        private static Course NewCourse(int id, string code, string name) => new()
        {
            CourseId = id,
            CourseCode = code,
            CourseName = name,
            LecturerId = LecturerA,
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1)
        };

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
