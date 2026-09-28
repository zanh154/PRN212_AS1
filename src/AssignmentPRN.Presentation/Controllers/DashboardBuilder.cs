using AssignmentPRN.Business;
using AssignmentPRN.Presentation.Models;

namespace AssignmentPRN.Presentation.Controllers;

/// <summary>Shared dashboard numbers, so Admin and Lecturer stay in step.</summary>
internal static class DashboardBuilder
{
    private const int UpcomingWindowDays = 7;

    public static async Task<StaffDashboardViewModel> BuildStaffAsync(
        IExamSessionService service,
        CancellationToken cancellationToken)
    {
        var response = await service.ListAsync(cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return new StaffDashboardViewModel { LoadError = response.Error };
        }

        var now = DateTime.Now;
        var horizon = now.AddDays(UpcomingWindowDays);
        var upcoming = response.Data
            .Where(session => session.StartTime >= now && session.StartTime <= horizon)
            .OrderBy(session => session.StartTime)
            .ToList();

        return new StaffDashboardViewModel
        {
            SessionCount = response.Data.Count,
            CandidateCount = response.Data.Sum(session => session.CandidateCount),
            UpcomingCount = upcoming.Count,
            NextSession = upcoming.FirstOrDefault()
        };
    }

    public static async Task<StudentDashboardViewModel> BuildStudentAsync(
        IExamSessionService service,
        int userId,
        CancellationToken cancellationToken)
    {
        var response = await service.GetStudentScheduleAsync(userId, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return new StudentDashboardViewModel { LoadError = response.Error };
        }

        var now = DateTime.Now;
        var upcoming = response.Data.Items
            .Where(item => item.ScheduledTime >= now)
            .OrderBy(item => item.ScheduledTime)
            .ToList();

        return new StudentDashboardViewModel
        {
            ExamCount = response.Data.Items.Count,
            UpcomingCount = upcoming.Count,
            NextExam = upcoming.FirstOrDefault()
        };
    }
}
