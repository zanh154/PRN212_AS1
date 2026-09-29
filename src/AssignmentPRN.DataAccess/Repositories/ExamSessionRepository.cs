using AssignmentPRN.DataAccess.Common;
using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

public class ExamSessionRepository(AivesDbContext context) : IExamSessionRepository
{
    public async Task<ExamStudentSearchResult> SearchExamStudentsAsync(ExamStudentSearch filter,
        int? lecturerId, CancellationToken cancellationToken = default)
    {
        var sessions = context.ExamSessions.AsNoTracking()
            .Where(s => !lecturerId.HasValue || s.LecturerId == lecturerId.Value);
        var query = context.ExamCandidates.AsNoTracking()
            .Where(c => !lecturerId.HasValue || c.Session.LecturerId == lecturerId.Value);
        var term = filter.Query?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term))
            query = query.Where(c => c.Student.FullName.ToLower().Contains(term) || c.Student.Email.ToLower().Contains(term));
        if (filter.ExamId.HasValue) query = query.Where(c => c.ExamId == filter.ExamId.Value);
        if (filter.Status.HasValue) query = query.Where(c => c.Status == filter.Status.Value);
        if (filter.From.HasValue) query = query.Where(c => c.ScheduledTime >= filter.From.Value.Date);
        if (filter.To.HasValue)
        {
            var until = filter.To.Value.Date.AddDays(1);
            query = query.Where(c => c.ScheduledTime < until);
        }
        var total = await query.CountAsync(cancellationToken);
        var page = Math.Clamp(filter.Page, 1, Math.Max(1, (int)Math.Ceiling(total / 20d)));
        return new ExamStudentSearchResult
        {
            Total = total, Page = page,
            Sessions = await sessions.OrderBy(s => s.StartTime).ThenBy(s => s.ExamId)
                .Select(s => new ExamStudentSessionOption(s.ExamId, s.ExamName)).ToListAsync(cancellationToken),
            Items = await query.OrderBy(c => c.ScheduledTime).ThenBy(c => c.CandidateId)
                .Skip((page - 1) * 20).Take(20)
                .Select(c => new ExamStudentRow(c.CandidateId, c.ExamId, c.Student.FullName,
                    c.Student.Email, c.Session.ExamName, c.Session.Course.CourseName,
                    c.ScheduledTime, c.Status)).ToListAsync(cancellationToken)
        };
    }

    public async Task RemoveStudentAsync(int examId, int candidateId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var session = await context.ExamSessions.FirstOrDefaultAsync(
            item => item.ExamId == examId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiên thi.");
        if (session.Status is not (ExamSessionStatus.Draft or ExamSessionStatus.Scheduled)
            || session.StartTime <= DateTime.Now)
            throw new ArgumentException("Chỉ được xóa sinh viên khi phiên thi chưa bắt đầu.");

        var candidate = await context.ExamCandidates.FirstOrDefaultAsync(
            item => item.ExamId == examId && item.CandidateId == candidateId, cancellationToken)
            ?? throw new KeyNotFoundException("Sinh viên không còn trong phiên thi này.");
        if (candidate.Status != CandidateStatus.Waiting || candidate.StartedAt.HasValue
            || candidate.FinishedAt.HasValue)
            throw new ArgumentException("Chỉ được xóa sinh viên đang chờ thi và chưa có dữ liệu bài thi.");

        // Remove enrollment only; keep the account, other sessions and reserved exam window.
        context.ExamCandidates.Remove(candidate);
        session.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ExamSessionDetail> AddStudentAsync(int examId, string email,
        DateTime scheduledTime, CancellationToken cancellationToken = default)
    {
        // Serialize roster checks and insertion so concurrent requests cannot add duplicates.
        await using var transaction = await context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var session = await context.ExamSessions.FirstOrDefaultAsync(
            item => item.ExamId == examId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiên thi.");
        if (session.Status is not (ExamSessionStatus.Draft or ExamSessionStatus.Scheduled))
            throw new ArgumentException("Chỉ được thêm sinh viên khi phiên thi chưa bắt đầu.");
        if (session.StartTime <= DateTime.Now || scheduledTime <= DateTime.Now)
            throw new ArgumentException("Không thể thêm sinh viên vào lịch thi trong quá khứ.");
        if (session.TimePerStudent <= 0 || scheduledTime.Date != session.StartTime.Date
            || scheduledTime.AddMinutes(session.TimePerStudent).Date != session.StartTime.Date)
            throw new ArgumentException("Khung giờ phải nằm trọn trong ngày thi.");

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var student = await context.Users.SingleOrDefaultAsync(user =>
            user.Email.ToLower() == normalizedEmail && user.Status == "Active"
            && user.Role.RoleName == "Student", cancellationToken)
            ?? throw new ArgumentException("Không tìm thấy sinh viên đang hoạt động với email này.");
        if (await context.ExamCandidates.AnyAsync(item =>
            item.ExamId == examId && item.StudentId == student.UserId, cancellationToken))
            throw new ArgumentException("Sinh viên đã có trong phiên thi này.");

        var endTime = scheduledTime.AddMinutes(session.TimePerStudent);
        await EnsureNoConflictAsync(student.UserId, session.LecturerId, scheduledTime,
            endTime, null, cancellationToken);
        context.ExamCandidates.Add(new ExamCandidate
        {
            ExamId = examId, StudentId = student.UserId,
            ScheduledTime = scheduledTime, Status = CandidateStatus.Waiting
        });
        if (scheduledTime < session.StartTime) session.StartTime = scheduledTime;
        if (endTime > session.EndTime) session.EndTime = endTime;
        session.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);
        var detail = await LoadDetailAsync(examId, cancellationToken)
            ?? throw new InvalidOperationException("Không thể đọc phiên thi vừa cập nhật.");
        await transaction.CommitAsync(cancellationToken);
        return detail;
    }

    public async Task<IReadOnlyList<ExamSessionListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await context.ExamSessions
            .AsNoTracking()
            .OrderBy(session => session.StartTime)
            .ThenBy(session => session.ExamName)
            .Select(session => new ExamSessionListItem
            {
                ExamId = session.ExamId,
                ExamName = session.ExamName,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                TimePerStudent = session.TimePerStudent,
                Status = session.Status,
                LecturerName = session.Lecturer.FullName,
                CourseCode = session.Course.CourseCode,
                CourseName = session.Course.CourseName,
                CandidateCount = session.Candidates.Count
            })
            .ToListAsync(cancellationToken);
    }

    public Task<ExamSessionDetail?> GetDetailAsync(int examId, CancellationToken cancellationToken = default)
    {
        return examId <= 0
            ? Task.FromResult<ExamSessionDetail?>(null)
            : LoadDetailAsync(examId, cancellationToken);
    }

    public async Task<StudentSchedule?> GetStudentScheduleAsync(
        int studentUserId,
        CancellationToken cancellationToken = default)
    {
        var student = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserId == studentUserId, cancellationToken);
        if (student is null)
        {
            return null;
        }

        var items = await context.ExamCandidates
            .AsNoTracking()
            .Where(candidate => candidate.StudentId == studentUserId
                && candidate.ScheduledTime != null
                && candidate.Status != CandidateStatus.Cancelled
                && candidate.Session.Status != ExamSessionStatus.Cancelled)
            .OrderBy(candidate => candidate.ScheduledTime)
            .Select(candidate => new StudentScheduleItem
            {
                ExamId = candidate.ExamId,
                CandidateId = candidate.CandidateId,
                ExamName = candidate.Session.ExamName,
                CourseCode = candidate.Session.Course.CourseCode,
                CourseName = candidate.Session.Course.CourseName,
                LecturerName = candidate.Session.Lecturer.FullName,
                ScheduledTime = candidate.ScheduledTime!.Value,
                TimePerStudent = candidate.Session.TimePerStudent,
                SessionStatus = candidate.Session.Status,
                CandidateStatus = candidate.Status
            })
            .ToListAsync(cancellationToken);

        return new StudentSchedule
        {
            StudentId = student.UserId,
            StudentName = student.FullName,
            StudentEmail = student.Email,
            Items = items
        };
    }

    public async Task<ExamSessionDetail> CreateAsync(
        ExamSessionAggregateInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var duplicateStudentIds = input.Candidates
            .GroupBy(candidate => candidate.StudentId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateStudentIds.Count > 0)
        {
            throw new ArgumentException(
                $"Danh sách lịch thi có sinh viên bị trùng: {string.Join(", ", duplicateStudentIds)}.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var candidate in input.Candidates)
        {
            await EnsureNoConflictAsync(
                candidate.StudentId,
                input.LecturerId,
                candidate.ScheduledTime,
                candidate.ScheduledTime.AddMinutes(input.TimePerStudent),
                excludedCandidateId: null,
                cancellationToken);
        }

        var session = new ExamSession
        {
            CourseId = input.CourseId,
            LecturerId = input.LecturerId,
            ExamName = input.ExamName,
            Description = input.Description,
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            TimePerStudent = input.TimePerStudent,
            MainQuestionCount = input.MainQuestionCount,
            MaxFollowUpCount = input.MaxFollowUpCount,
            Status = input.Status,
            CreatedAt = DateTime.Now,
            Candidates = input.Candidates.Select(candidate => new ExamCandidate
            {
                StudentId = candidate.StudentId,
                ScheduledTime = candidate.ScheduledTime,
                Status = candidate.Status
            }).ToList()
        };

        context.ExamSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);

        var detail = await LoadDetailAsync(session.ExamId, cancellationToken)
            ?? throw new InvalidOperationException("Không thể đọc lịch thi vừa tạo.");

        await transaction.CommitAsync(cancellationToken);
        return detail;
    }

    public async Task<ExamSessionDetail> RescheduleAsync(
        int candidateId,
        DateTime scheduledTime,
        CancellationToken cancellationToken = default)
    {
        if (candidateId <= 0)
        {
            throw new ArgumentException("Không xác định được lượt thi cần đổi giờ.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var candidate = await context.ExamCandidates
            .Include(item => item.Session)
            .FirstOrDefaultAsync(item => item.CandidateId == candidateId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy lượt thi #{candidateId}.");

        var session = candidate.Session;
        var endTime = scheduledTime.AddMinutes(session.TimePerStudent);

        if (scheduledTime.Date != session.StartTime.Date)
        {
            throw new ArgumentException(
                $"Khung giờ mới phải nằm trong ngày thi {session.StartTime:dd/MM/yyyy}.");
        }

        await EnsureNoConflictAsync(
            candidate.StudentId,
            session.LecturerId,
            scheduledTime,
            endTime,
            candidate.CandidateId,
            cancellationToken);

        candidate.ScheduledTime = scheduledTime;

        // Keep the session window covering every slot after the move.
        var otherStarts = await context.ExamCandidates
            .Where(item => item.ExamId == session.ExamId
                && item.CandidateId != candidate.CandidateId
                && item.ScheduledTime != null)
            .Select(item => item.ScheduledTime!.Value)
            .ToListAsync(cancellationToken);

        var starts = otherStarts.Append(scheduledTime).ToList();
        session.StartTime = starts.Min();
        session.EndTime = starts.Max().AddMinutes(session.TimePerStudent);
        session.UpdatedAt = DateTime.Now;

        await context.SaveChangesAsync(cancellationToken);

        var detail = await LoadDetailAsync(session.ExamId, cancellationToken)
            ?? throw new InvalidOperationException("Không thể đọc lịch thi sau khi đổi giờ.");

        await transaction.CommitAsync(cancellationToken);
        return detail;
    }

    public async Task DeleteAsync(int examId, CancellationToken cancellationToken = default)
    {
        var session = await context.ExamSessions
            .FirstOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        if (session is null)
        {
            return;
        }

        context.ExamSessions.Remove(session);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Rejects a slot that overlaps an existing one for the same student or the same lecturer.
    /// Each slot lasts its own session's TimePerStudent, so the overlap test runs in memory
    /// after narrowing the rows down to the same calendar day.
    /// </summary>
    private async Task EnsureNoConflictAsync(
        int studentId,
        int lecturerId,
        DateTime startTime,
        DateTime endTime,
        int? excludedCandidateId,
        CancellationToken cancellationToken)
    {
        var dayStart = startTime.Date;
        var dayEnd = dayStart.AddDays(1);

        var sameDay = await context.ExamCandidates
            .AsNoTracking()
            .Where(candidate => candidate.ScheduledTime != null
                && candidate.ScheduledTime >= dayStart
                && candidate.ScheduledTime < dayEnd
                && candidate.Status != CandidateStatus.Cancelled
                && candidate.Session.Status != ExamSessionStatus.Cancelled
                && (!excludedCandidateId.HasValue || candidate.CandidateId != excludedCandidateId.Value)
                && (candidate.StudentId == studentId || candidate.Session.LecturerId == lecturerId))
            .Select(candidate => new
            {
                SameStudent = candidate.StudentId == studentId,
                Start = candidate.ScheduledTime!.Value,
                candidate.Session.TimePerStudent,
                StudentName = candidate.Student.FullName,
                LecturerName = candidate.Session.Lecturer.FullName
            })
            .ToListAsync(cancellationToken);

        foreach (var other in sameDay)
        {
            var otherEnd = other.Start.AddMinutes(other.TimePerStudent);

            // Two half-open intervals overlap when each starts before the other ends.
            if (startTime >= otherEnd || endTime <= other.Start)
            {
                continue;
            }

            var owner = other.SameStudent
                ? $"sinh viên {other.StudentName}"
                : $"giảng viên {other.LecturerName}";

            throw new ExamScheduleConflictException(
                $"Trùng lịch với {owner} trong khung {other.Start:HH:mm}-{otherEnd:HH:mm} ngày {other.Start:dd/MM/yyyy}.");
        }
    }

    private async Task<ExamSessionDetail?> LoadDetailAsync(int examId, CancellationToken cancellationToken)
    {
        var session = await context.ExamSessions
            .AsNoTracking()
            .Include(item => item.Course)
            .Include(item => item.Lecturer)
            .Include(item => item.Candidates)
                .ThenInclude(candidate => candidate.Student)
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.ExamId == examId, cancellationToken);

        return session is null ? null : MapDetail(session);
    }

    private static ExamSessionDetail MapDetail(ExamSession session) => new()
    {
        ExamId = session.ExamId,
        ExamName = session.ExamName,
        Description = session.Description,
        StartTime = session.StartTime,
        EndTime = session.EndTime,
        TimePerStudent = session.TimePerStudent,
        MainQuestionCount = session.MainQuestionCount,
        MaxFollowUpCount = session.MaxFollowUpCount,
        Status = session.Status,
        CreatedAt = session.CreatedAt,
        Lecturer = new PersonSummary
        {
            UserId = session.Lecturer.UserId,
            FullName = session.Lecturer.FullName,
            Email = session.Lecturer.Email
        },
        Course = new CourseSummary
        {
            CourseId = session.Course.CourseId,
            CourseCode = session.Course.CourseCode,
            CourseName = session.Course.CourseName,
            Description = session.Course.Description
        },
        Candidates = session.Candidates
            .OrderBy(candidate => candidate.ScheduledTime ?? DateTime.MaxValue)
            .Select(candidate => new ExamCandidateDetail
            {
                CandidateId = candidate.CandidateId,
                StudentId = candidate.StudentId,
                StudentName = candidate.Student.FullName,
                StudentEmail = candidate.Student.Email,
                ScheduledTime = candidate.ScheduledTime,
                Status = candidate.Status
            })
            .ToList()
    };
}
